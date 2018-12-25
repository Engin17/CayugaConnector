using Microsoft.Win32;
using Newtonsoft.Json;
using SeeTec.SDK;
using SeeTecConnector.ServiceReference1;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using Path = System.IO.Path;

namespace SeeTecConnector
{
    #region Web service classes

    public class HeartbeatRequest
    {
        public string SiteNo { get; set; }
        public string IPAddress { get; set; }
        public string MACAddress { get; set; }
    }
    public class AlarmRequest
    {
        public string SiteNo { get; set; }
        public string IPAddress { get; set; }
        public string MACAddress { get; set; }
        public string ChannelNo { get; set; }
        public string ChannelName { get; set; }
        public string AlarmType { get; set; }
        public string AlarmDetails { get; set; }
        public string DateTime { get; set; }
        public string Comments { get; set; }
    }
    public class RecorderInfoRequest
    {
        public string SiteNo { get; set; }
        public string IPAddress { get; set; }
        public string MACAddress { get; set; }
        public string SerialNo { get; set; }
        public string FirmwareVersion { get; set; }
        public string ModelName { get; set; }
        public int NoOfChannel { get; set; }
    }

    #endregion

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static ConfigurationWindow configurationWindow;
        private static StartWindow startWindow;

        private static string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private static string configFolderPath = Path.Combine(applicationPath, "Configuration");
        public static string configFilePath = Path.Combine(configFolderPath, "Configuration.xml");
        public static string logFolderPath = Path.Combine(applicationPath, "Log");
        public static string logFilePath = Path.Combine(logFolderPath, "Log.txt");

        private static Guid ConnectedInstallationID;
        private static SDKMethodResult<Guid> InstallationIDResult;
        private static ReceiverServiceResponseClient client = null;
        private static string installationName;
        private static HashSet<long> NotReachableCameras = new HashSet<long>();
        private static HashSet<long> DMwithFailure = new HashSet<long>();
        private static bool isRecordingServerOfflineAlreadyDetected;

        private static bool isConnectedToSeeTec = false;
        private static string cayugaVersion = "Unknown";
        private static string macAddress = "Unknown";

        public static string hostIP;
        public static int hostPort;
        public static string username;
        public static string profile;
        public static string password;
        public static string inr;
        public static int heartbeat;
        public static int recorderInfo;


        public MainWindow()
        {
            SDKVideoManagerFactory.SetDispatcherThread();
            SDKVideoManagerFactory.SetMainWindowHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);

            InitializeComponent();

            MainWindow.startWindow = new StartWindow();
            MainWindow.startWindow.Show();

            this.PrepareStart();

            this.ConnectToCayuga();

            this.SetHostIPGUI();

            MainWindow.startWindow.Close();
        }

        /// <summary>
        /// Set Host IP for the UI after reading the configuration XML
        /// </summary>
        private void SetHostIPGUI()
        {
            if (File.Exists(MainWindow.configFilePath))
            {
                // Load the configuration xml and read out the connection status to SeeTec 
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(MainWindow.configFilePath);

                XmlNodeList elemListIPHost = xmlDoc.GetElementsByTagName("HostIP");

                for (int i = 0; i < elemListIPHost.Count; i++)
                {
                    if (Dispatcher.CheckAccess())
                    {
                        labelConnection.Content = elemListIPHost[i].InnerXml;
                    }
                    else
                    {
                        Dispatcher.Invoke(() => { labelConnection.Content = elemListIPHost[i].InnerXml; });
                    }
                }
            }
        }

        /// <summary>
        /// Get informations for the connector
        /// Start to send periodically heartbeat and recorder info to the Videoguard web service
        /// </summary>
        private void StartConnector()
        {
            MainWindow.cayugaVersion = this.GetVersion();

            MainWindow.macAddress = this.GetMac();

            MainWindow.client = this.GetClient();

            this.StartSendHeart();

            this.StartSendRecordInfoRequest();
        }

        /// <summary>
        /// Read configuration from XML and open configuration window
        /// </summary>
        private void MenuItemConfiguration_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.configurationWindow = new ConfigurationWindow();

            this.ReadConfiguration();

            MainWindow.configurationWindow.ShowDialog();
        }

        /// <summary>
        /// Close SeeTec Connector
        /// </summary>
        private void MenuItemCloseMainWindow_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Try reconnect to Cayuga server
        /// </summary>
        private void ButtonReconnect_Click(object sender, RoutedEventArgs e)
        {
            Thread threadTryReconnect = new Thread(() =>
            {
                if (Dispatcher.CheckAccess())
                {
                    btnReconnect.IsEnabled = false;
                }
                else
                {
                    Dispatcher.Invoke(() => { btnReconnect.IsEnabled = false; });
                }

                this.LogUI("Try connecting to Cayuga server.");

                this.PrepareStart();

                this.SetHostIPGUI();

                this.ConnectToCayuga();
            });
            threadTryReconnect.Start();
        }

        private void ButtonCheckVideoguard_Click(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// Open Log folder
        /// </summary>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(MainWindow.logFolderPath))
                {
                    // Open log folder with windows explorer
                    Process.Start(MainWindow.logFolderPath);
                }
            }
            catch (Win32Exception)
            {

            }
        }

        /// <summary>
        /// Load configuration XML for the Connector. If not exists then create the configuration XML.
        /// </summary>
        private void PrepareStart()
        {
            try
            {
                if (Dispatcher.CheckAccess())
                {
                    tblockAssembly.Text = this.GetRunningVersion();
                }
                else
                {
                    Dispatcher.Invoke(() => { tblockAssembly.Text = this.GetRunningVersion(); });
                }

                // Check if the log directory exists. If not create directory
                if (!Directory.Exists(logFolderPath))
                {
                    try
                    {
                        Directory.CreateDirectory(logFolderPath);

                        File.Create(logFilePath);
                    }
                    catch (Exception ex)
                    {
                        using (StreamWriter w = File.AppendText(logFilePath))
                        {
                            Log(ex.Message + " (PrepareStart)", w);
                        }
                    }
                }
                else
                {
                    if (!File.Exists(logFilePath))
                    {
                        File.Create(logFilePath);
                    }
                }

                // Check if connector xml configuration file exists
                if (File.Exists(configFilePath))
                {
                    // Load the configuration xml and read out the configured IP address and port for the clientSocket
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(configFilePath);

                    XmlNodeList elemListIPHost = xmlDoc.GetElementsByTagName("HostIP");

                    for (int i = 0; i < elemListIPHost.Count; i++)
                    {
                        string hostIP;
                        hostIP = elemListIPHost[i].InnerXml;

                        MainWindow.hostIP = hostIP;
                    }

                    XmlNodeList elemListPortHost = xmlDoc.GetElementsByTagName("HostPort");

                    for (int i = 0; i < elemListPortHost.Count; i++)
                    {
                        string hostPort;
                        hostPort = elemListPortHost[i].InnerXml;

                        MainWindow.hostPort = Convert.ToInt32(hostPort);
                    }

                    XmlNodeList elemListUser = xmlDoc.GetElementsByTagName("User");

                    for (int i = 0; i < elemListUser.Count; i++)
                    {
                        string user;
                        user = elemListUser[i].InnerXml;

                        MainWindow.username = user;
                    }

                    XmlNodeList elemListPassword = xmlDoc.GetElementsByTagName("Password");

                    for (int i = 0; i < elemListPassword.Count; i++)
                    {
                        string password;
                        password = elemListPassword[i].InnerXml;

                        MainWindow.password = password;
                    }

                    XmlNodeList elemListProfile = xmlDoc.GetElementsByTagName("Profile");

                    for (int i = 0; i < elemListProfile.Count; i++)
                    {
                        string profile;
                        profile = elemListProfile[i].InnerXml;

                        MainWindow.profile = profile;
                    }

                    XmlNodeList elemListINR = xmlDoc.GetElementsByTagName("INR");

                    for (int i = 0; i < elemListINR.Count; i++)
                    {
                        string inr;
                        inr = elemListINR[i].InnerXml;

                        MainWindow.inr = inr;
                    }

                    XmlNodeList elemListHeartbeat = xmlDoc.GetElementsByTagName("Heartbeat");

                    for (int i = 0; i < elemListHeartbeat.Count; i++)
                    {
                        string heartbeat;
                        heartbeat = elemListHeartbeat[i].InnerXml;

                        MainWindow.heartbeat = Convert.ToInt32(heartbeat);
                    }

                    XmlNodeList elemListRecorderInfo = xmlDoc.GetElementsByTagName("RecorderInfo");

                    for (int i = 0; i < elemListRecorderInfo.Count; i++)
                    {
                        string recorderInfo;
                        recorderInfo = elemListRecorderInfo[i].InnerXml;

                        MainWindow.recorderInfo = Convert.ToInt32(recorderInfo);
                    }

                    using (StreamWriter w = File.AppendText(logFilePath))
                    {
                        Log("Connector configuration successfully loaded", w);
                    }
                }

                // Create connector xml configuration file because its not created yet or its deleted
                else
                {
                    // Settings for the xml
                    XmlWriterSettings settings = new XmlWriterSettings
                    {
                        Encoding = Encoding.UTF8,
                        ConformanceLevel = ConformanceLevel.Document,
                        OmitXmlDeclaration = false,
                        CloseOutput = true,
                        Indent = true,
                        IndentChars = "  ",
                        NewLineHandling = NewLineHandling.Replace
                    };

                    // Check if the configuration directory exists. If not create directory
                    if (!Directory.Exists(configFolderPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(configFolderPath);
                        }
                        catch (Exception ex)
                        {
                            using (StreamWriter w = File.AppendText(logFilePath))
                            {
                                Log(ex.Message + " (PrepareStart)", w);
                            }
                        }
                    }

                    // Create connector xml configuration file
                    using (XmlWriter writer = XmlWriter.Create(configFilePath, settings))
                    {
                        writer.WriteStartDocument();
                        writer.WriteStartElement("Configuration");

                        writer.WriteStartElement("HostIP");
                        writer.WriteValue("localhost");
                        writer.WriteEndElement();

                        writer.WriteStartElement("HostPort");
                        writer.WriteValue("60000");
                        writer.WriteEndElement();

                        writer.WriteStartElement("User");
                        writer.WriteValue("");
                        writer.WriteEndElement();

                        writer.WriteStartElement("Password");
                        writer.WriteValue("");
                        writer.WriteEndElement();

                        writer.WriteStartElement("Profile");
                        writer.WriteValue("");
                        writer.WriteEndElement();

                        writer.WriteStartElement("INR");
                        writer.WriteValue("");
                        writer.WriteEndElement();

                        writer.WriteStartElement("Heartbeat");
                        writer.WriteValue("300000");
                        writer.WriteEndElement();

                        writer.WriteStartElement("RecorderInfo");
                        writer.WriteValue("300000");
                        writer.WriteEndElement();

                        writer.WriteStartElement("ConnectionSeeTec");
                        writer.WriteValue("Unknown");
                        writer.WriteEndElement();

                        writer.WriteStartElement("ConnectionVideoguard");
                        writer.WriteValue("Unknown");
                        writer.WriteEndElement();

                        writer.WriteEndDocument();
                    }

                    using (StreamWriter w = File.AppendText(logFilePath))
                    {
                        Log("Connector configuration successfully loaded", w);
                    }
                }
            }
            catch (Exception ex)
            {
                using (StreamWriter w = File.AppendText(logFilePath))
                {
                    Log(ex.Message + " (PrepareStart) ", w);
                }
            }
        }

        /// (summary)
        /// Write a line to the log file.  For simplicity, assume no race conditions.
        /// Modified to also display the line in the GUI if it is running.
        /// (/summary)
        /// (param name="state")(/param)
        private void ConnectToCayuga()
        {
            try
            {
                using (StreamWriter w = File.AppendText(logFilePath))
                {
                    Log("Connecting to Cayuga server.", w);
                }

                //Login
                SDKConnectionInfo connectionInfo = new SDKConnectionInfo();

                connectionInfo.Host = MainWindow.hostIP;
                connectionInfo.Port = MainWindow.hostPort;
                connectionInfo.UserName = MainWindow.username;
                connectionInfo.Password = MainWindow.password;
                connectionInfo.Profile = MainWindow.profile;

                SDKVideoManager videoManager = SDKVideoManagerFactory.GetManager();
                MainWindow.InstallationIDResult = videoManager.Connect(connectionInfo);

                if (MainWindow.InstallationIDResult.Result == SDKErrorCode.OK)
                {
                    MainWindow.isConnectedToSeeTec = true;

                    Thread startConnectorThread = new Thread(() =>
                    {
                        StartConnector();
                    });
                    startConnectorThread.Start();

                    if (Dispatcher.CheckAccess())
                    {
                        btnReconnect.IsEnabled = false;
                        btnCheckVideoguard.IsEnabled = true;
                    }
                    else
                    {
                        Dispatcher.Invoke(() => { btnReconnect.IsEnabled = false; btnCheckVideoguard.IsEnabled = true;});
                    }

                    ConnectedInstallationID = MainWindow.InstallationIDResult.ReturnValue;
                    this.CountCameras();
                    videoManager.VideoManagementEvent += OnvideoManager_VideoManagement;
                    videoManager.Disconnected += OnDisconnectToSeeTecServer;
                    videoManager.Reconnected += OnReconnectToSeeTecServer;
                    MainWindow.installationName = videoManager.GetShortInstallationName(ConnectedInstallationID);

                    if (Dispatcher.CheckAccess())
                    {
                        labelStatusSeeTec.Content = "Connected";
                        labelStatusSeeTec.Foreground = Brushes.Green;
                    }
                    else
                    {
                        Dispatcher.Invoke(() => { labelStatusSeeTec.Content = "Connected"; labelStatusSeeTec.Foreground = Brushes.Green; });
                    }

                    this.LogUI("Successfully connected to Cayuga server.");

                    using (StreamWriter w = File.AppendText(logFilePath))
                    {
                        Log("Successfully connected to Cayuga server.", w);
                    }
                }
                else
                {
                    MainWindow.isConnectedToSeeTec = false;

                    if (Dispatcher.CheckAccess())
                    {
                        labelStatusSeeTec.Content = "Not Connected";
                        labelStatusSeeTec.Foreground = Brushes.Red;
                    }
                    else
                    {
                        Dispatcher.Invoke(() => { labelStatusSeeTec.Content = "Not Connected"; labelStatusSeeTec.Foreground = Brushes.Red; });
                    }

                    if (Dispatcher.CheckAccess())
                    {
                        btnReconnect.IsEnabled = true;
                        btnCheckVideoguard.IsEnabled = false;
                    }
                    else
                    {
                        Dispatcher.Invoke(() => { btnReconnect.IsEnabled = true; btnCheckVideoguard.IsEnabled = false;});
                    }

                    this.LogUI("Cannot connect to Cayuga server.Problem: " + MainWindow.InstallationIDResult.Result);
                    this.LogUI("Connection problem. Check Connector configuration and if the Cayuga Server is running.");

                    using (StreamWriter w = File.AppendText(logFilePath))
                    {
                        Log("Cannot connect to Cayuga server. Problem: " + MainWindow.InstallationIDResult.Result, w);
                        Log("Connection problem. Check Connector configuration and if the Cayuga Server is running.", w);
                    }
                }
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (ConnectToCayuga)");

                using (StreamWriter w = File.AppendText(logFilePath))
                {
                    Log(ex.Message + " (ConnectToCayuga)", w);
                }
            }
        }

        /// <summary>
        /// Disconnected event from SeeTec SDK
        /// </summary>
        private void OnDisconnectToSeeTecServer(Guid installationID)
        {
            MainWindow.isConnectedToSeeTec = false;

            this.LogUI("Disconnected to SeeTec. Waiting until Core is running again...");

            using (StreamWriter w = File.AppendText(logFilePath))
            {
                Log("Disconnected to SeeTec. Waiting until Core is running again...", w);
            }

            if (Dispatcher.CheckAccess())
            {
                labelStatusSeeTec.Content = "Not Connected";
                labelStatusSeeTec.Foreground = Brushes.Red;
            }
            else
            {
                Dispatcher.Invoke(() => { labelStatusSeeTec.Content = "Not Connected"; labelStatusSeeTec.Foreground = Brushes.Red; });
            }
        }

        /// <summary>
        /// Reconnected event from SeeTec SDK
        /// </summary>
        private void OnReconnectToSeeTecServer(Guid installationID)
        {
            MainWindow.isConnectedToSeeTec = true;

            this.LogUI("Reconnected to SeeTec.");

            using (StreamWriter w = File.AppendText(logFilePath))
            {
                Log("Reconnected to SeeTec.", w);
            }

            if (Dispatcher.CheckAccess())
            {
                labelStatusSeeTec.Content = "Connected";
                labelStatusSeeTec.Foreground = Brushes.Green;
            }
            else
            {
                Dispatcher.Invoke(() => { labelStatusSeeTec.Content = "Connected"; labelStatusSeeTec.Foreground = Brushes.Green; });
            }
        }

        /// <summary>
        /// Video management events from SeeTec SDK
        /// </summary>
        private void OnvideoManager_VideoManagement(SDKEvent evt)
        {
            SDKVideoManager videoManager = SDKVideoManagerFactory.GetManager();
            var timestamp = evt.TimeStamp;
            var sourceID = evt.SourceID;
            var entity = videoManager.GetEntity(ConnectedInstallationID, sourceID);
            var cause = videoManager.GetEntity(ConnectedInstallationID, evt.CauseID);

            if (evt.EventType == SDKEventType.CMVideoSourceNotAvailable) //VideoLoss, only defined for "cable not connected"
            {
                SendAlarm("1", timestamp, entity.Name, "Video Loss");
                NotReachableCameras.Add(sourceID);
            }
            else if (evt.EventType == SDKEventType.AlarmRecordingStart)
            {
                SendAlarm("9", timestamp, entity.Name, "Alarm Recording Start");
            }
            else if (evt.EventType == SDKEventType.AlarmRecordingStop)
            {
                SendAlarm("10", timestamp, entity.Name, "Alarm Recording Stop");
            }
            else if (evt.EventType == SDKEventType.AlarmTriggered)
            {
                SendAlarm("7", timestamp, entity.Name, "Alarm scenario started");
            }
            else if (evt.EventType == SDKEventType.EntityStatusChanged)
            {
                if (videoManager.IsSubType(SDKEntityType.VideoSource, entity.EntityType) &&
                    entity.Status == 0 &&
                    NotReachableCameras.Contains(sourceID))
                {
                    SendAlarm("2", timestamp, entity.Name, "Video Reconnect");
                    NotReachableCameras.Remove(sourceID);

                }
                else if (entity.EntityType == SDKEntityType.RuntimeDM && entity.Status == 0 && DMwithFailure.Contains(sourceID)) //only when DM was offline at first
                {
                    isRecordingServerOfflineAlreadyDetected = false;
                    SendAlarm("5", timestamp, entity.Name, "Recording Server Online");
                    DMwithFailure.Remove(sourceID);
                }
            }
            else if (evt.EventType == SDKEventType.MDBZoneAlmostFull)
            {
                SendAlarm("3", timestamp, entity.Name, "MDS zone is almost full");
            }
            else if (evt.EventType == SDKEventType.REInvalidStatus || evt.EventType == SDKEventType.CMCannotStart || evt.EventType == SDKEventType.EntityDeregistered)
            {
                if (entity.EntityType == SDKEntityType.RuntimeDM && !isRecordingServerOfflineAlreadyDetected)
                {
                    isRecordingServerOfflineAlreadyDetected = true;
                    SendAlarm("6", timestamp, entity.Name, "Recording Server Offline");
                    DMwithFailure.Add(sourceID);
                }
            }
        }

        /// <summary>
        /// Send heartbeat to Videoguard if connected to SeeTec
        /// </summary>
        private void StartSendHeart()
        {
            Thread threadHeartBeat = new Thread(() =>
            {
                while (true)
                {
                    if (MainWindow.isConnectedToSeeTec)
                    {
                        SendHeartBeat();
                    }
                    Thread.Sleep(MainWindow.heartbeat);
                }
            });
            threadHeartBeat.Start();
        }

        private void StartSendRecordInfoRequest()
        {
            Thread threadRecorderInfo = new Thread(() =>
            {
                while (true)
                {
                    if (MainWindow.isConnectedToSeeTec)
                    {
                        RecorderInfo();
                    }
                    Thread.Sleep(MainWindow.recorderInfo);
                }
            });
            threadRecorderInfo.Start();
        }

        private int CountCameras()
        {
            try
            {
                SDKVideoManager videoManager = SDKVideoManagerFactory.GetManager();
                SDKEntity root = videoManager.GetRootEntity(ConnectedInstallationID);
                Stack<SDKEntity> searchStack = new Stack<SDKEntity>();
                searchStack.Push(root);
                List<SDKEntity> videoSources = new List<SDKEntity>();
                while (searchStack.Count > 0)
                {
                    SDKEntity currentEntity = searchStack.Pop();
                    if (videoManager.IsSubType(SDKEntityType.VideoSource, currentEntity.EntityType))
                    {
                        videoSources.Add(currentEntity);
                    }
                    foreach (long childID in currentEntity.ChildrenIDs)
                    {
                        searchStack.Push(videoManager.GetEntity(currentEntity.InstallationID, childID));
                    }
                }
                return videoSources.Count;

            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (CountCameras)");

                using (StreamWriter w = File.AppendText(logFilePath))
                {
                    Log(ex.Message + " (CountCameras) ", w);
                }

                return -1;
            }
        }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        public static extern int SendARP(int destIp, int srcIP, byte[] macAddr, ref int physicalAddrLen);
        private string GetMac()
        {
            try
            {
                IPAddress dst = IPAddress.Parse(MainWindow.hostIP); // the destination IP address
                byte[] macAddr = new byte[6];
                int macAddrLen = macAddr.Length;

                if (SendARP(BitConverter.ToInt32(dst.GetAddressBytes(), 0), 0, macAddr, ref macAddrLen) != 0)
                    Console.WriteLine("SendARP failed.");

                string[] str = new string[macAddrLen];
                for (int i = 0; i < macAddrLen; i++)
                    str[i] = macAddr[i].ToString("x2");

                return (string.Join(":", str).ToUpper());
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (GetMac)");

                using (StreamWriter w = File.AppendText(logFilePath))
                {
                    Log(ex.Message + " (GetMac) ", w);
                }

                return "Unknown";
            }
        }

        public string GetVersion()
        {
            try
            {
                return (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\SeeTec\Install", "LastSeeTecVersion", string.Empty);
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (GetVersion)");

                using (StreamWriter w = File.AppendText(logFilePath))
                {
                    Log(ex.Message + " (GetVersion) ", w);
                }

                return "Unknown";
            }
        }

        private ReceiverServiceResponseClient GetClient()
        {
            if (client == null)
            {
                client = new ReceiverServiceResponseClient();
            }

            return client;
        }

        private void SendAlarm(string alarmType, DateTime timestamp, string name, string details)
        {
            try
            {
                var request = new AlarmRequest
                {
                    SiteNo = installationName.Substring(0, 6),
                    IPAddress = MainWindow.hostIP,
                    MACAddress = MainWindow.macAddress,
                    ChannelNo = "",
                    ChannelName = name,
                    AlarmType = alarmType,
                    AlarmDetails = details,
                    DateTime = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    Comments = ""
                };

                var requests = new List<AlarmRequest> { request };
                var json = JsonConvert.SerializeObject(requests);
                Console.WriteLine("Sending Alarm: " + json);
                string s = client.SendAlarm(json);
                Console.WriteLine("Alarm response: " + s);
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (SendAlarm)");

                using (StreamWriter w = File.AppendText(logFilePath))
                {
                    Log(ex.Message + " (SendAlarm) ", w);
                }
            }
        }

        private void SendHeartBeat()
        {
            try
            {
                File.AppendAllText(@"C:\Users\engin.aslan\Desktop\test.txt", "Heartbeat send");
                var request = new HeartbeatRequest
                {
                    SiteNo = installationName.Substring(0, 6),
                    IPAddress = MainWindow.hostIP,
                    MACAddress = MainWindow.macAddress
                };

                var requests = new List<HeartbeatRequest> { request };
                var json = JsonConvert.SerializeObject(requests);
                Console.WriteLine("Sending HeartBeat: " + json);
                string s = client.SendHeartBeat(json);
                Console.WriteLine("HeartBeat response: " + s);
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (SendHeartBeat)");

                using (StreamWriter w = File.AppendText(logFilePath))
                {
                    Log(ex.Message + " (SendHeartBeat) ", w);
                }
            }
        }

        public void RecorderInfo()
        {
            try
            {
                var request = new RecorderInfoRequest
                {
                    SiteNo = installationName.Substring(0, 6),
                    IPAddress = MainWindow.hostIP,
                    MACAddress = MainWindow.macAddress,
                    SerialNo = "605523", //Platzhalter
                    FirmwareVersion = MainWindow.cayugaVersion,
                    ModelName = "SeeTec Cayuga",
                    NoOfChannel = CountCameras()
                };
                var requests = new List<RecorderInfoRequest> { request };
                var json = JsonConvert.SerializeObject(requests);
                Console.WriteLine("Recorder Information: " + json);
                string s = client.SendRecorderInfo(json);
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (RecorderInfo)");

                using (StreamWriter w = File.AppendText(logFilePath))
                {
                    Log(ex.Message + " (RecorderInfo) ", w);
                }
            }
        }

        private void ReadConfiguration()
        {
            if (File.Exists(MainWindow.configFilePath))
            {
                // Load the configuration xml and read out the configured IP address and port for the clientSocket
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(MainWindow.configFilePath);

                XmlNodeList elemListIPHost = xmlDoc.GetElementsByTagName("HostIP");

                for (int i = 0; i < elemListIPHost.Count; i++)
                {
                    string hostIP;
                    hostIP = elemListIPHost[i].InnerXml;
                    configurationWindow.tbHostIP.Text = hostIP;
                }

                XmlNodeList elemListPortHost = xmlDoc.GetElementsByTagName("HostPort");

                for (int i = 0; i < elemListPortHost.Count; i++)
                {
                    string hostPort;
                    hostPort = elemListPortHost[i].InnerXml;
                    configurationWindow.tbHostPort.Text = hostPort;
                }

                XmlNodeList elemListUser = xmlDoc.GetElementsByTagName("User");

                for (int i = 0; i < elemListUser.Count; i++)
                {
                    string user;
                    user = elemListUser[i].InnerXml;
                    configurationWindow.tbUser.Text = user;
                }

                XmlNodeList elemListPassword = xmlDoc.GetElementsByTagName("Password");

                for (int i = 0; i < elemListPassword.Count; i++)
                {
                    string password;
                    password = elemListPassword[i].InnerXml;
                    configurationWindow.tbPassword.Text = password;
                }

                XmlNodeList elemListProfile = xmlDoc.GetElementsByTagName("Profile");

                for (int i = 0; i < elemListProfile.Count; i++)
                {
                    string profile;
                    profile = elemListProfile[i].InnerXml;
                    configurationWindow.tbProfile.Text = profile;
                }

                XmlNodeList elemListINR = xmlDoc.GetElementsByTagName("INR");

                for (int i = 0; i < elemListINR.Count; i++)
                {
                    string inr;
                    inr = elemListINR[i].InnerXml;
                    configurationWindow.tbINR.Text = inr;
                }

                XmlNodeList elemListHeartbeat = xmlDoc.GetElementsByTagName("Heartbeat");

                for (int i = 0; i < elemListHeartbeat.Count; i++)
                {
                    string heartbeat;
                    heartbeat = elemListHeartbeat[i].InnerXml;
                    configurationWindow.tbHeartbeat.Text = heartbeat;
                }

                XmlNodeList elemListRecorderInfo = xmlDoc.GetElementsByTagName("RecorderInfo");

                for (int i = 0; i < elemListRecorderInfo.Count; i++)
                {
                    string recorderInfo;
                    recorderInfo = elemListRecorderInfo[i].InnerXml;
                    configurationWindow.tbRecorderInfo.Text = recorderInfo;
                }
            }
        }

        /// <summary>
        /// Method to get the current Assembly information
        /// </summary>
        private string GetRunningVersion()
        {
            try
            {
                String s = "v" + Assembly.GetExecutingAssembly().GetName().Version;
                return s.Substring(0, 6);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return "xxx";
            }

        }

        public static void Log(string logMessage, TextWriter w)
        {
            w.WriteLine("{0} {1}", DateTime.Now.ToString(), logMessage);
        }

        public void LogUI(string logMessega)
        {
            if (Dispatcher.CheckAccess())
            {
                listBox1.Items.Add(DateTime.Now.ToString() + " " + logMessega);
                listBox1.SelectedIndex = listBox1.Items.Count - 1;
                listBox1.ScrollIntoView(listBox1.SelectedItem);
            }
            else
            {
                Dispatcher.Invoke(() => { listBox1.Items.Add(DateTime.Now.ToString() + " " + logMessega); });
                Dispatcher.Invoke(() => { listBox1.SelectedIndex = listBox1.Items.Count - 1; });
                Dispatcher.Invoke(() => { listBox1.ScrollIntoView(listBox1.SelectedItem); });
            }
        }
       
    }
}

