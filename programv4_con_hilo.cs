using System;
using System.Diagnostics;
using System.Threading;
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

namespace proyect
{
    class Program
    {
        //static GpioPin Button;
        //static GpioController gpioControl;
        static int ConnCount = 0;
        // Set the SSID & Password to your local Wifi network
        const string MYSSID = "Familia_Rivera";
        const string MYPASSWORD = "Juanito2001";

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
            //gpioControl = new GpioController();
            //Button = gpioControl.OpenPin(0, PinMode.InputPullUp);
            //Button.DebounceTimeout = new TimeSpan(0, 0, 0, 0, 100);
            ////Button.ValueChanged += Button_ValueChanged;
            //gpioControl.RegisterCallbackForPinValueChangedEvent(0, PinEventTypes.Falling, Button_ValueChanged);

            i2conf(1, 21, 22);
            aht10_conf();
            oled_conf();
            rtc_conf();

            new Thread(display_oled).Start();

            try
            {

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
                        wifi.ScanAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failure starting a scan operation: {ex}");
                    }

                    Thread.Sleep(30000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("message:" + ex.Message);
                Console.WriteLine("stack:" + ex.StackTrace);
            }

            HttpListener Listener = new HttpListener("http", 80);
            Listener.Start();
            Socket Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint EndPort = new IPEndPoint(IPAddress.Any, 1234);
            Server.Bind(EndPort);
            Server.Listen(10);
            HttpListenerContext Request = Listener.GetContext();
            new Thread(() => ProcessRequest(Request)).Start();

            while (true)
            {

                Socket Connection = Server.Accept();
                ConnCount++;
                new Thread(() => ProcessRequest(Connection, ConnCount)).Start();
                if (Connection != null)
                {
                    Console.WriteLine(DateTime.UtcNow.ToString() + " | New Socket Connection from: " + Connection.RemoteEndPoint.ToString() + ", Local: " + Connection.LocalEndPoint.ToString());
                    Connection.Send(UTF8Encoding.UTF8.GetBytes("Hola Mundo"));
                    Thread.Sleep(1000);
                    Connection.Close();
                }
                // -----------------------------------------------------------

                // ---------------------------------------------------------
            }

        }

        private static void Button_ValueChanged(object sender, PinValueChangedEventArgs e)
        {
            Console.WriteLine("Button Value Change Event: " + e.ChangeType.ToString());
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


                                        //string Respuesta = "<HTML><BODY>Hola Mundo, Respuesta HTTP desde NanoFrameWork " + DateTime.UtcNow.ToString() + ".</BODY></HTML>";
                                        string Respuesta = Resource1.GetString(Resource1.StringResources.PaginaHome);
                                        if (Respuesta.Contains("1"))
                                        {
                                            int Index = Respuesta.IndexOf("1");
                                            string s = Respuesta.Substring(0, Index);
                                            s += DateTime.UtcNow.ToString();
                                            s += Respuesta.Substring(Index + 3);
                                            Respuesta = s;
                                        }
                                        byte[] Buffer = UTF8Encoding.UTF8.GetBytes(Respuesta);
                                        Context.Response.ContentLength64 = Buffer.Length;
                                        Context.Response.OutputStream.Write(Buffer, 0, Buffer.Length);
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
                    }

                    Context.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
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
                if (net.Ssid == MYSSID)
                {
                    // Disconnect in case we are already connected
                    sender.Disconnect();

                    // Connect to network
                    WifiConnectionResult result = sender.Connect(net, WifiReconnectionKind.Automatic, MYPASSWORD);

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
        static void display_oled()
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
    }

}

