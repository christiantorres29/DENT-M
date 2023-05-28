
using System;
using System.Diagnostics;
using System.Threading;
using System.Collections;
using nanoFramework.Hardware.Esp32;
using System.Device.I2c;
using Iot.Device.Ahtxx;
using Iot.Device.Ssd13xx.Samples;
using System.Device.Wifi;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using Iot.Device.Ssd13xx;
using System.Device.Gpio;
using Primer_proyecto_04_02;
using Iot.Device.Rtc;
using System.IO;
using Windows.Storage;
using nanoFramework.Json;
using JsonConfigurationStore;
using nanoFramework.Runtime.Native;
using nanoFramework.Networking;
using Iot.Device.DhcpServer;
using WifiAP;

namespace proyect
{
    class Program
    {
        static object Sync = new object();
        static GpioPin Button;
        static GpioController gpioControl;
        static GpioPin led;
        static bool UpDown = false;
        static bool accp = false;
        // Set the default SSID & Password to your local Wifi network
        //static string SSID = "weathersense";
        //static string PASSWORD = "tepercino";
        static string SSID = "Torres";
        static string PASSWORD = "CLAROI16219";
        static bool saved=false;
        static string ssid=null;
        static string password=null;

        static ConfigurationStore configurationStore = new ConfigurationStore();
        static JsonConfiguration json_store = new JsonConfiguration();
        static bool WifiConnected = false;
        static Aht10 aht10;
        static Ssd1306 oled;
        static Ds1307 rtc;
        static int I2cBus = 1;

        public static void i2conf(int bus, int sda, int sdb)
        {
            Configuration.SetPinFunction(sda, DeviceFunction.I2C1_DATA);
            Configuration.SetPinFunction(sdb, DeviceFunction.I2C1_CLOCK);
            I2cBus = bus;
        }
        public static void aht10_conf()
        {
            I2cConnectionSettings i2cSettings = new(I2cBus, Aht10.DefaultI2cAddress);
            I2cDevice i2cDevice = I2cDevice.Create(i2cSettings);
            Aht10 aht10_ = new Aht10(i2cDevice);
            aht10 = aht10_;
        }
        public static void oled_conf()
        {
            I2cConnectionSettings i2cSettings_ = new I2cConnectionSettings(I2cBus, Ssd1306.DefaultI2cAddress);
            I2cDevice i2cDevice_ = I2cDevice.Create(i2cSettings_);
            Ssd1306 oled_ = new Ssd1306(i2cDevice_, Ssd13xx.DisplayResolution.OLED128x64);
            oled = oled_;
            oled.ClearScreen();
            oled.Font = new BasicFont();
        }
        public static void rtc_conf()
        {
            I2cConnectionSettings settings = new I2cConnectionSettings(1, Ds1307.DefaultI2cAddress);
            I2cDevice device = I2cDevice.Create(settings);
            Ds1307 rtc_ = new Ds1307(device);
            //rtc_.DateTime = new DateTime(2023, 5, 19, 4, 38, 30);
            rtc = rtc_;
        }
        static void Main(string[] args)
        {
            gpioControl = new GpioController();
            Button = gpioControl.OpenPin(19, PinMode.InputPullDown);
            led = gpioControl.OpenPin(2, PinMode.Output);
            Button.DebounceTimeout = new TimeSpan(0, 0, 0, 0, 50);
            gpioControl.RegisterCallbackForPinValueChangedEvent(19, PinEventTypes.Rising, Button_ValueChanged);

            i2conf(1, 21, 22);
            aht10_conf();
            oled_conf();
            rtc_conf();
            //new Thread(basic_data_display).Start();
            led.Write(PinValue.High);
            Thread.Sleep(3000);
            led.Write(PinValue.Low);

            if (Button.Read() == PinValue.High)
            {
                accp = true;

                if (!Wireless80211.IsEnabled())
                {
                    Wireless80211.Disable();
                    if (WirelessAP.Setup() == false)
                    {
                        // Reboot device to Activate Access Point on restart
                        Console.WriteLine($"Setup Soft AP, Rebooting device");
                        Power.RebootDevice();
                    }
                    var dhcpserver = new DhcpServer
                    {
                        CaptivePortalUrl = $"http://{WirelessAP.SoftApIP}"
                    };
                    var dhcpInitResult = dhcpserver.Start(IPAddress.Parse(WirelessAP.SoftApIP), new IPAddress(new byte[] { 255, 255, 255, 0 }));
                    if (!dhcpInitResult)
                    {
                        Debug.WriteLine($"Error initializing DHCP server.");
                    }
                    Debug.WriteLine($"Running Soft AP, waiting for client to connect");
                    Debug.WriteLine($"Soft AP IP address :{WirelessAP.GetIP()}");
                    oled.Write(0, 1, $"ACCES POINT MODE :", 1, false);
                    oled.Write(0, 2, $"Network: ", 1, false);
                    oled.Write(0, 3, $"nano_8853C0", 1, false);
                    oled.Write(0, 4, $"Type this IP", 1, false);
                    oled.Write(0, 5, $"{WirelessAP.GetIP()}", 1, false);
                    oled.Display();
                    //WifiNetworkHelper.Disconnect();// , catch reboot, if (!ap)//i
                }
                HttpListener Listener = new HttpListener("http", 80);
                Listener.Start();



                while (true)
                {
                    HttpListenerContext Request = Listener.GetContext();
                    new Thread(() => ProcessRequest(Request)).Start();
                    if (saved) { break; }
                    Thread.Sleep(10);
                }
                Power.RebootDevice();
            }
            else if (Button.Read() == PinValue.Low)
            {
                accp = false;
                oled.ClearScreen();

                while (true)
                {

                    try
                    {
                        if (configurationStore.IsConfigFileExisting)
                        {
                            JsonConfiguration storage = configurationStore.GetConfig();
                            SSID = storage.SSID;
                            PASSWORD = storage.PASSWORD;
                        }
                        else
                        {
                            Console.WriteLine("By default");
                        }
                        //Wireless80211.Configure(SSID, PASSWORD);
                        
                        // Get the first WiFI Adapter
                        WifiAdapter wifi = WifiAdapter.FindAllAdapters()[0];
                        // Set up the AvailableNetworksChanged event to pick up when scan has completed
                        wifi.AvailableNetworksChanged += Wifi_AvailableNetworksChanged;
                        NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
                        NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;

                        // give it some time to perform the initial "connect"
                        // trying to scan while the device is still in the connect procedure will throw an exception
                        Thread.Sleep(10_000);

                        // Loop forever scanning every 30 seconds
                        while (!WifiConnected)
                        {
                            try
                            {
                                Console.WriteLine("starting Wi-Fi scan");
                                oled.ClearScreen();
                                oled.Write(0,0, "starting Wi-Fi scan",1,false);
                                oled.Display();
                                wifi.ScanAsync();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failure starting a scan operation: {ex}");
                                oled.ClearScreen();
                                oled.Write(0, 0, "Failure starting a scan operation", 1, false);
                                oled.Display();
                            }

                            Thread.Sleep(10000);
                        }
                    
                    }

                    catch (Exception ex)
                    {
                        Console.WriteLine("message:" + ex.Message);
                        Console.WriteLine("stack:" + ex.StackTrace);
                    }
                    

                    HttpListener Listener = new HttpListener("http", 80);
                    Listener.Start();

                    oled.ClearScreen();
                    new Thread(basic_data_display).Start();
                    HttpListenerContext Request = Listener.GetContext();
                    new Thread(() => ProcessRequest(Request)).Start();
                    while (true)
                    {

                    }
                    
                }
            
            }

        }

        private static void ProcessRequest(HttpListenerContext Context)
        {
            try
            {
                if (Context != null)
                {
                    if (Context.Request != null)
                    {
                        Console.WriteLine(Context.Request.RawUrl);
                        Console.WriteLine(Context.Request.HttpMethod);
                        foreach (string item in Context.Request.Headers.AllKeys)
                        {
                            Console.WriteLine(item + " : " + Context.Request.Headers[item]);
                        }
                        if (Context.Request.HttpMethod == "GET")
                        {
                            switch (Context.Request.RawUrl)
                            {
                                case "/":
                                    if (Context.Response != null)
                                    {
                                        string Respuesta = Resource1.GetString(Resource1.StringResources.PaginaHome);
                                        if (accp)
                                        {
                                            Respuesta = Resource1.GetString(Resource1.StringResources.get_ssid2);
                                        }
                                        if (Respuesta.Contains("h%"))
                                        {
                                            int Index = Respuesta.IndexOf("h%");
                                            string s = Respuesta.Substring(0, Index);
                                            s += $"{aht10.GetHumidity().Percent:F0}";
                                            s += Respuesta.Substring(Index+1 );
                                            Respuesta = s;
                                        }
                                        if (Respuesta.Contains("tC"))
                                        {
                                            int Index2 = Respuesta.IndexOf("tC");
                                            string s2 = Respuesta.Substring(0, Index2);
                                            s2 += $"{aht10.GetTemperature().DegreesCelsius:F1}";
                                            s2 += Respuesta.Substring(Index2 + 1);
                                            Respuesta = s2;
                                        }
                                        if (Respuesta.Contains("time_"))
                                        {
                                            int Index3 = Respuesta.IndexOf("time_");
                                            string s3 = Respuesta.Substring(0, Index3);
                                            s3 += DateTime.UtcNow.AddHours(-5).ToString();
                                            s3 += Respuesta.Substring(Index3 + 5);
                                            Respuesta = s3;
                                        }
                                        byte[] Buffer = UTF8Encoding.UTF8.GetBytes(Respuesta);
                                        Context.Response.ContentLength64 = Buffer.Length;
                                        Context.Response.OutputStream.Write(Buffer, 0, Buffer.Length);
                                        Context.Response.Close();
                                        byte[] Buffer2 = UTF8Encoding.UTF8.GetBytes(Respuesta);
                                        Context.Response.ContentLength64 = Buffer2.Length;
                                        Context.Response.OutputStream.Write(Buffer2, 0, Buffer2.Length);
                                        Context.Response.Close();
                                        byte[] Buffer3 = UTF8Encoding.UTF8.GetBytes(Respuesta);
                                        Context.Response.ContentLength64 = Buffer3.Length;
                                        Context.Response.OutputStream.Write(Buffer3, 0, Buffer3.Length);
                                        Context.Response.Close();
                                    }
                                    break;
                                case "/favicon.ico":
                                    if (Context.Response != null)
                                    {
                                        byte[] Buffer = Resource1.GetBytes(Resource1.BinaryResources.favicon);
                                        Context.Response.ContentLength64 = Buffer.Length;
                                        Context.Response.OutputStream.Write(Buffer, 0, Buffer.Length);
                                        Context.Response.Close();
                                    }
                                    break;
                                default:
                                    if (Context.Response != null)
                                    {
                                        Context.Response.ContentLength64 = 0;
                                        Context.Response.Close();
                                    }
                                    break;
                            }
                        }
                        if (Context.Request.HttpMethod == "POST")
                        {
                            // Pick up POST parameters from Input Stream
                            Hashtable hashPars = ParseParamsFromStream(Context.Request.InputStream);
                            ssid = (string)hashPars["ssid"];
                            password = (string)hashPars["password"];
                            json_store.SSID = ssid;
                            json_store.PASSWORD = password;
                            saved = configurationStore.WriteConfig(json_store);
                            if (saved)
                            {
                                oled.ClearScreen();
                                oled.Write(0, 1, "data saved", 1, false);
                                oled.Write(0, 2, "Network: ", 1, false);
                                oled.Write(0, 3, "-> "+ssid, 1, false);
                                oled.Display();
                                Console.WriteLine(ssid);
                                Console.WriteLine("data stored");
                                Thread.Sleep(1000);
                            }
                            Debug.WriteLine($"Wireless parameters SSID:{ssid} PASSWORD:{password}");

                            //string message = "<p>New settings saved.</p><p>Rebooting device to put into normal mode</p>";

                        }
                    }

                    Context.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        private static void Button_ValueChanged(object sender, PinValueChangedEventArgs e)
        {
            //Console.WriteLine("Button Value Change Event: " + e.ChangeType.ToString());
            lock (Sync)
            {
                if (UpDown)
                {
                    UpDown = false;
                    led.Write(PinValue.Low); 
                }
                else
                {
                    led.Write(PinValue.High);
                    UpDown = true;

                }
            }
        }
        private static void ProcessRequest(Socket Connection, int Count)
        {
            try
            {
                if (Connection != null)
                {
                    Console.WriteLine(DateTime.UtcNow.ToString() + " | New Socket Connection from: " + Connection.RemoteEndPoint.ToString() + ", Local: " + Connection.LocalEndPoint.ToString());
                    Connection.Send(UTF8Encoding.UTF8.GetBytes("Hola Mundo\n"));
                    //Thread.Sleep(1000);
                    Connection.Close();
                    Console.WriteLine("Socket de Conexion " + Count + " Finalizado");
                    Thread.Sleep(10000);
                    Console.WriteLine("Hilo de Conexion " + Count + " Finalizado");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        private static void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            if (e.IsAvailable)
            {
                Console.WriteLine("Network Connection Ready");
            }
            else
            {
                Console.WriteLine("Network Connection Lost");
            }
        }
        private static void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            NetworkInterface[] Interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface intf in Interfaces)
            {
                Console.WriteLine("Interface: " + intf.NetworkInterfaceType + ", IP Address: " + intf.IPv4Address.ToString());
            }
            string ip = Interfaces[0].IPv4Address.ToString();
            oled.ClearScreen();
            oled.Write(0, 5, ip, 1, false);
            oled.Display();
        }
        private static void Wifi_AvailableNetworksChanged(WifiAdapter sender, object e)
        {
            Console.WriteLine("Wifi_AvailableNetworksChanged - get report");
            
            // Get Report of all scanned Wifi networks
            WifiNetworkReport report = sender.NetworkReport;

            // Enumerate though networks looking for our network
            foreach (WifiAvailableNetwork net in report.AvailableNetworks)
            {
                // Show all networks found
                Console.WriteLine($"Net SSID :{net.Ssid},  BSSID : {net.Bsid},  rssi : {net.NetworkRssiInDecibelMilliwatts.ToString()},  signal : {net.SignalBars.ToString()}");

                // If its our Network then try to connect
                if (net.Ssid == SSID)
                {
                    // Disconnect in case we are already connected
                    sender.Disconnect();

                    // Connect to network
                    WifiConnectionResult result = sender.Connect(net, WifiReconnectionKind.Automatic,PASSWORD);

                    // Display status
                    if (result.ConnectionStatus == WifiConnectionStatus.Success)
                    {
                        Console.WriteLine("Connected to Wifi network");
                        WifiConnected = true;
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"Error {result.ConnectionStatus.ToString()} connecting o Wifi network");
                    }
                }
            }
        }
        static void basic_data_display()
        {
            while (true)
            {
                string textTemp = $"temp: {aht10.GetTemperature().DegreesCelsius:F1}C";
                string textHum = $"hum: {aht10.GetHumidity().Percent:F0}%";
                DateTime dt = rtc.DateTime;
                string time = dt.ToString("yyyy/MM/dd HH:mm:ss");
                string fecha = time.Substring(0, 11);
                string hora = time.Substring(11, 8);
                oled.Write(0, 0, textTemp, 1, false);
                oled.Write(0, 1, textHum, 1, false);
                oled.Write(0, 2, "fecha-hora", 1, false);
                oled.Write(0, 3, fecha, 1, false);
                oled.Write(0, 4, hora, 1, false);
                oled.Display();
                Thread.Sleep(10);
            }
        }
        static Hashtable ParseParamsFromStream(Stream inputStream)
        {
            byte[] buffer = new byte[inputStream.Length];
            inputStream.Read(buffer, 0, (int)inputStream.Length);

            return ParseParams(System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length));
        }
        static Hashtable ParseParams(string rawParams)
        {
            Hashtable hash = new Hashtable();

            string[] parPairs = rawParams.Split('&');
            foreach (string pair in parPairs)
            {
                string[] nameValue = pair.Split('=');
                hash.Add(nameValue[0], nameValue[1]);
            }

            return hash;
        }

    }

}



