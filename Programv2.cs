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
using Primer_proyecto_04_02;

namespace proyect
{
    class Program
    {
        static int ConnCount = 0;
        // Set the SSID & Password to your local Wifi network
        const string MYSSID = "moto1480";
        const string MYPASSWORD = "tepercino";

        static bool WifiConnected = false;
        //const int I2cBus = 1;
        static Aht10 aht10;
        static Ssd1306 oled;
        static int I2cBus = 0;

        public static void i2conf(int bus, int sda, int sdb)
        {
            Configuration.SetPinFunction(sda, DeviceFunction.I2C1_DATA);
            Configuration.SetPinFunction(sdb, DeviceFunction.I2C1_CLOCK);
            I2cBus = bus;
        }

        //public static Aht10 ahtconf()
        //{
        //    I2cConnectionSettings i2cSettings = new(I2cBus, Aht10.DefaultI2cAddress);
        //    I2cDevice i2cDevice = I2cDevice.Create(i2cSettings);
        //    Aht10 aht10 = new Aht10(i2cDevice);
        //    return aht10;
        //}

        //public static Ssd1306 oledconf()
        //{
        //    I2cConnectionSettings i2cSettings = new I2cConnectionSettings(I2cBus, Ssd1306.DefaultI2cAddress);
        //    I2cDevice i2cDevice = I2cDevice.Create(i2cSettings);
        //    using Ssd1306 device = new Ssd1306(i2cDevice, Ssd13xx.DisplayResolution.OLED128x64);
        //    device.ClearScreen();
        //    device.Font = new BasicFont();
        //    return device;
        //}
        static void Main(string[] args)
        {
            i2conf(1, 21, 22);
            //aht10=ahtconf();
            I2cConnectionSettings i2cSettings = new(I2cBus, Aht10.DefaultI2cAddress);
            I2cDevice i2cDevice = I2cDevice.Create(i2cSettings);
            Aht10 aht10 = new Aht10(i2cDevice);

            //oledconf();
            I2cConnectionSettings i2cSettings_ = new I2cConnectionSettings(I2cBus, Ssd1306.DefaultI2cAddress);
            I2cDevice i2cDevice_ = I2cDevice.Create(i2cSettings_);
            using Ssd1306 oled = new Ssd1306(i2cDevice, Ssd13xx.DisplayResolution.OLED128x64);
            oled.ClearScreen();
            oled.Font = new BasicFont();

            Console.WriteLine($"{aht10.GetTemperature().DegreesCelsius:F1}°C, {aht10.GetHumidity().Percent:F0}%");
            string textTemp = $"temp: {aht10.GetTemperature().DegreesCelsius:F1}C";
            string textHum = $"hum: {aht10.GetHumidity().Percent:F0}%";
            string tiempo = $":{DateTime.UtcNow}";
            Debug.WriteLine(tiempo.Length.ToString());
            string fecha = tiempo.Substring(1, 11);
            string hora = tiempo.Substring(12, 8);
            Debug.WriteLine(hora);
            Thread.Sleep(1000);
            //oled.Write(0, 0, textTemp, 1, false);
            //oled.Write(0, 1, textHum, 1, false);
            //oled.Write(0, 2, "fecha-hora", 1, false);
            //oled.Write(0, 3, fecha, 1, false);
            //oled.Write(0, 4, hora, 1, false);
            //oled.Display();
            //Thread.Sleep(1000);
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
                        Debug.WriteLine("starting Wi-Fi scan");
                        wifi.ScanAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failure starting a scan operation: {ex}");
                    }

                    Thread.Sleep(30000);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("message:" + ex.Message);
                Debug.WriteLine("stack:" + ex.StackTrace);
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
                textTemp = $"temp: {aht10.GetTemperature().DegreesCelsius:F1}C";
                textHum = $"hum: {aht10.GetHumidity().Percent:F0}%";
                tiempo = $":{DateTime.UtcNow}";
                fecha = tiempo.Substring(1, 11);
                hora = tiempo.Substring(12, 8);
                oled.Write(0, 0, textTemp, 1, false);
                oled.Write(0, 1, textHum, 1, false);
                oled.Write(0, 3, fecha, 1, false);
                oled.Write(0, 4, hora, 1, false);
                oled.Display();
                Thread.Sleep(10);
                // -----------------------------------------------------------




            }
            //   while (true)
            //    {
            //Debug.WriteLine("system time is: " + DateTime.UtcNow);
            //Console.WriteLine($"{sensor.GetTemperature().DegreesCelsius:F1}°C, {sensor.GetHumidity().Percent:F0}%");
            //string textTemp = $"temp: {sensor.GetTemperature().DegreesCelsius:F1}C";
            //string textHum = $"hum: {sensor.GetHumidity().Percent:F0}%";
            //string tiempo = $":{DateTime.UtcNow}";
            //Debug.WriteLine(tiempo.Length.ToString());
            //string fecha = tiempo.Substring(1, 11);
            //string hora = tiempo.Substring(12, 8);
            //Debug.WriteLine(hora);
            //Thread.Sleep(1000);
            //oled.Font = new BasicFont();
            //oled.Write(0, 0, textTemp, 1, false);
            //oled.Write(0, 1, textHum, 1, false);
            //oled.Write(0, 2, "fecha-hora", 1, false);
            //oled.Write(0, 3, fecha, 1, false);
            //oled.Write(0, 4, hora, 1, false);
            //oled.Display();
            //Thread.Sleep(1000);
            // }


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

        /// <summary>
        /// Event handler for when Wifi scan completes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Wifi_AvailableNetworksChanged(WifiAdapter sender, object e)
        {
            Debug.WriteLine("Wifi_AvailableNetworksChanged - get report");

            // Get Report of all scanned Wifi networks
            WifiNetworkReport report = sender.NetworkReport;

            // Enumerate though networks looking for our network
            foreach (WifiAvailableNetwork net in report.AvailableNetworks)
            {
                // Show all networks found
                Debug.WriteLine($"Net SSID :{net.Ssid},  BSSID : {net.Bsid},  rssi : {net.NetworkRssiInDecibelMilliwatts.ToString()},  signal : {net.SignalBars.ToString()}");

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
                        Debug.WriteLine("Connected to Wifi network");
                        WifiConnected = true;
                        break;
                    }
                    else
                    {
                        Debug.WriteLine($"Error {result.ConnectionStatus.ToString()} connecting o Wifi network");
                    }
                }
            }
        }
    }
}
