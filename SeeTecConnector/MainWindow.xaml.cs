using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using System.Security.Cryptography;
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
        public static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static ConfigurationWindow configurationWindow;
        private static StartWindow startWindow;

        private static string applicationPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private static string configFolderPath = Path.Combine(applicationPath, "Configuration");
        public static string configFilePath = Path.Combine(configFolderPath, "Configuration.xml");
        public static string logFolderPath = Path.Combine(applicationPath, "Logs");
        public static string logFilePath = Path.Combine(logFolderPath, "Log.txt");

        private static Guid ConnectedInstallationID;
        private static SDKMethodResult<Guid> InstallationIDResult;
        private static ReceiverServiceResponseClient client = null;
        private static string installationName;
        private static HashSet<long> NotReachableCameras = new HashSet<long>();
        private static HashSet<long> DMandMDSwithFailure = new HashSet<long>();

        public static bool isConnectedToSeeTec = false;
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

        static byte[] bytes = ASCIIEncoding.ASCII.GetBytes("CryptKey"); // Needed for Password security (Length should be 8)


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

                logger.Info("------------------------------------------------------------------------------");
                logger.Info("Start SeeTec Connector");

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

                        // Decrypt password from XML to use it in the application
                        MainWindow.password = MainWindow.Decrypt(password);
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

                        MainWindow.heartbeat = Convert.ToInt32(heartbeat) * 1000; // seconds in milliseconds
                    }

                    XmlNodeList elemListRecorderInfo = xmlDoc.GetElementsByTagName("RecorderInfo");

                    for (int i = 0; i < elemListRecorderInfo.Count; i++)
                    {
                        string recorderInfo;
                        recorderInfo = elemListRecorderInfo[i].InnerXml;

                        MainWindow.recorderInfo = Convert.ToInt32(recorderInfo) * 1000; // seconds in milliseconds
                    }
                    logger.Info("Connector configuration successfully loaded");
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
                            logger.Error(ex.Message + " (PrepareStart)");
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
                        writer.WriteValue("300");
                        writer.WriteEndElement();

                        writer.WriteStartElement("RecorderInfo");
                        writer.WriteValue("300");
                        writer.WriteEndElement();

                        writer.WriteStartElement("ConnectionSeeTec");
                        writer.WriteValue("Unknown");
                        writer.WriteEndElement();

                        writer.WriteStartElement("ConnectionVideoguard");
                        writer.WriteValue("Unknown");
                        writer.WriteEndElement();

                        writer.WriteEndDocument();
                    }
                    logger.Info("Connector configuration successfully loaded");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message + " (PrepareStart) ");
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
                logger.Info("Connecting to Cayuga server.");

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
                        Dispatcher.Invoke(() => { btnReconnect.IsEnabled = false; btnCheckVideoguard.IsEnabled = true; });
                    }

                    MainWindow.ConnectedInstallationID = MainWindow.InstallationIDResult.ReturnValue;
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

                    logger.Info("Successfully connected to Cayuga server.");
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
                        Dispatcher.Invoke(() => { btnReconnect.IsEnabled = true; btnCheckVideoguard.IsEnabled = false; });
                    }

                    this.LogUI("Cannot connect to Cayuga server.Problem: " + MainWindow.InstallationIDResult.Result);
                    this.LogUI("Connection problem. Check Connector configuration and if the Cayuga Server is running.");

                    logger.Warn("Cannot connect to Cayuga server. Problem: " + MainWindow.InstallationIDResult.Result);
                    logger.Warn("Connection problem. Check Connector configuration and if the Cayuga Server is running.");
                }
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (ConnectToCayuga)");

                logger.Error(ex.Message + " (ConnectToCayuga)");
            }
        }

        /// <summary>
        /// Disconnected event from SeeTec SDK
        /// </summary>
        private void OnDisconnectToSeeTecServer(Guid installationID)
        {
            MainWindow.isConnectedToSeeTec = false;

            this.LogUI("Disconnected to SeeTec. Waiting until Core is running again...");

            logger.Warn("Disconnected to SeeTec. Waiting until Core is running again...");

            if (Dispatcher.CheckAccess())
            {
                labelStatusSeeTec.Content = "Not Connected";
                labelStatusSeeTec.Foreground = Brushes.Red;

                btnCheckVideoguard.IsEnabled = false;
            }
            else
            {
                Dispatcher.Invoke(() => { labelStatusSeeTec.Content = "Not Connected"; labelStatusSeeTec.Foreground = Brushes.Red; btnCheckVideoguard.IsEnabled = false; });
            }
        }

        /// <summary>
        /// Reconnected event from SeeTec SDK
        /// </summary>
        private void OnReconnectToSeeTecServer(Guid installationID)
        {
            MainWindow.isConnectedToSeeTec = true;

            this.LogUI("Reconnected to SeeTec.");

            logger.Info("Reconnected to SeeTec.");

            if (Dispatcher.CheckAccess())
            {
                labelStatusSeeTec.Content = "Connected";
                labelStatusSeeTec.Foreground = Brushes.Green;

                btnCheckVideoguard.IsEnabled = true;
            }
            else
            {
                Dispatcher.Invoke(() => { labelStatusSeeTec.Content = "Connected"; labelStatusSeeTec.Foreground = Brushes.Green; btnCheckVideoguard.IsEnabled = true; });
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
            var entity = videoManager.GetEntity(MainWindow.ConnectedInstallationID, sourceID);
            var cause = videoManager.GetEntity(MainWindow.ConnectedInstallationID, evt.CauseID);

            if (evt.EventType == SDKEventType.CMVideoSourceNotAvailable) //VideoLoss, only defined for "cable not connected"
            {
                this.SendAlarm("1", timestamp, entity.Name, "Video Loss");
                MainWindow.NotReachableCameras.Add(sourceID);
            }
            else if (evt.EventType == SDKEventType.AlarmRecordingStart)
            {
                this.SendAlarm("9", timestamp, entity.Name, "Alarm Recording Start");
            }
            else if (evt.EventType == SDKEventType.AlarmRecordingStop)
            {
                this.SendAlarm("10", timestamp, entity.Name, "Alarm Recording Stop");
            }
            else if (evt.EventType == SDKEventType.AlarmTriggered)
            {
                this.SendAlarm("7", timestamp, entity.Name, "Alarm scenario started");
            }
            else if (evt.EventType == SDKEventType.EntityStatusChanged)
            {
                if (videoManager.IsSubType(SDKEntityType.VideoSource, entity.EntityType) &&
                    entity.Status == 0 &&
                    MainWindow.NotReachableCameras.Contains(sourceID))
                {
                    this.SendAlarm("2", timestamp, entity.Name, "Video Reconnect");
                    MainWindow.NotReachableCameras.Remove(sourceID);

                }
                else if (entity.EntityType == SDKEntityType.RuntimeDM && entity.Status == 0 && DMandMDSwithFailure.Contains(sourceID)) //only when DM was offline at first
                {
                    this.SendAlarm("5", timestamp, entity.Name, "Recording Server Online (DM)");
                    MainWindow.DMandMDSwithFailure.Remove(sourceID);
                }
                else if (entity.EntityType == SDKEntityType.RuntimeMDB && entity.Status == 0 && DMandMDSwithFailure.Contains(sourceID)) //only when DM was offline at first
                {
                    this.SendAlarm("5", timestamp, entity.Name, "Recording Server Online (MDS)");
                    MainWindow.DMandMDSwithFailure.Remove(sourceID);
                }
            }
            else if (evt.EventType == SDKEventType.MDBZoneAlmostFull)
            {
                this.SendAlarm("3", timestamp, entity.Name, "MDS zone is almost full");
            }
            else if (evt.EventType == SDKEventType.REInvalidStatus || evt.EventType == SDKEventType.CMCannotStart || evt.EventType == SDKEventType.EntityDeregistered || evt.EventType == SDKEventType.MDBCannotStartMDS)
            {
                if (entity.EntityType == SDKEntityType.RuntimeDM && !DMandMDSwithFailure.Contains(sourceID))
                {
                    this.SendAlarm("6", timestamp, entity.Name, "Recording Server Offline (DM)");
                    MainWindow.DMandMDSwithFailure.Add(sourceID);
                }
                else if (entity.EntityType == SDKEntityType.RuntimeMDB && !DMandMDSwithFailure.Contains(sourceID))
                {
                    this.SendAlarm("6", timestamp, entity.Name, "Recording Server Offline (MDS)");
                    MainWindow.DMandMDSwithFailure.Add(sourceID);
                }
            }
            
        }

        /// <summary>
        /// Send heartbeat to Videoguard periodically if connected to SeeTec
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

        /// <summary>
        /// Send recorder info to Videoguard periodically if connected to SeeTec
        /// </summary>
        private void StartSendRecordInfoRequest()
        {
            Thread threadRecorderInfo = new Thread(() =>
            {
                while (true)
                {
                    if (MainWindow.isConnectedToSeeTec)
                    {
                        SendRecorderInfo();
                    }
                    Thread.Sleep(MainWindow.recorderInfo);
                }
            });
            threadRecorderInfo.Start();
        }

        /// <summary>
        /// Count camera number in SeeTec system
        /// </summary>
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
                logger.Info("Camera number in the system is: " + videoSources.Count);

                return videoSources.Count;
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (CountCameras)");

                logger.Error(ex.Message + " (CountCameras) ");

                return -1;
            }
        }

        /// <summary>
        /// DllImport is needed for SendARP
        /// SendARP is needed to get the Mac address
        /// </summary>
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        public static extern int SendARP(int destIp, int srcIP, byte[] macAddr, ref int physicalAddrLen);
        private string GetMac()
        {
            try
            {
                string macAddress;

                IPAddress dst = IPAddress.Parse(MainWindow.hostIP); // the destination IP address
                byte[] macAddr = new byte[6];
                int macAddrLen = macAddr.Length;

                if (MainWindow.SendARP(BitConverter.ToInt32(dst.GetAddressBytes(), 0), 0, macAddr, ref macAddrLen) != 0)
                {
                    logger.Warn("Send ARP failed. Cannot obtain MAC address.");
                }

                string[] str = new string[macAddrLen];
                for (int i = 0; i < macAddrLen; i++)
                {
                    str[i] = macAddr[i].ToString("x2");
                }

                macAddress = string.Join(":", str).ToUpper();

                logger.Info("The MAC address is : " + macAddress);

                return macAddress;
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (GetMac)");

                logger.Error(ex.Message + " (GetMac) ");

                return "Unknown";
            }
        }

        public string GetVersion()
        {
            try
            {
                string cayugaVersion;

                cayugaVersion = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\SeeTec\Install", "LastSeeTecVersion", string.Empty);

                logger.Info("Cayuga version is: " + cayugaVersion);

                return cayugaVersion;
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (GetVersion)");

                logger.Error(ex.Message + " (GetVersion) ");

                return "Unknown";
            }
        }

        /// <summary>
        /// Get Videoguard web service client
        /// </summary>
        private ReceiverServiceResponseClient GetClient()
        {
            if (client == null)
            {
                client = new ReceiverServiceResponseClient();
            }

            return client;
        }

        /// <summary>
        /// Send alarm to Videoguard
        /// </summary>
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

                this.LogUI("Sending Alarm: " + json);
                logger.Info("Sending Alarm: " + json);

                string s = client.SendAlarm(json);


                this.LogUI("Alarm response: " + s);
                logger.Info("Alarm response: " + s);
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (SendAlarm)");

                logger.Error(ex.Message + " (SendAlarm) ");
            }
        }

        /// <summary>
        /// Send heartbeat to Videoguard
        /// </summary>
        private void SendHeartBeat()
        {
            try
            {
                var request = new HeartbeatRequest
                {
                    SiteNo = installationName.Substring(0, 6),
                    IPAddress = MainWindow.hostIP,
                    MACAddress = MainWindow.macAddress
                };

                var requests = new List<HeartbeatRequest> { request };
                var json = JsonConvert.SerializeObject(requests);

                this.LogUI("Sending HeartBeat: " + json);
                logger.Info("Sending HeartBeat: " + json);

                string s = client.SendHeartBeat(json);

                JObject jObj = JObject.Parse(s);
                JToken jTok = jObj.First;

                if (jTok.First.ToString() == "success")
                {
                    if (Dispatcher.CheckAccess())
                    {
                        labelStatusVideoguard.Content = "Connected";
                        labelStatusVideoguard.Foreground = Brushes.Green;
                    }
                    else
                    {
                        Dispatcher.Invoke(() => { labelStatusVideoguard.Content = "Connected"; labelStatusVideoguard.Foreground = Brushes.Green; });
                    }
                }
                else if (jTok.First.ToString() == "failure")
                {
                    if (Dispatcher.CheckAccess())
                    {
                        labelStatusVideoguard.Content = "Connected";
                        labelStatusVideoguard.Foreground = Brushes.Green;
                    }
                    else
                    {
                        Dispatcher.Invoke(() => { labelStatusVideoguard.Content = "Connected"; labelStatusVideoguard.Foreground = Brushes.Green; });
                    }
                }
                else
                {
                    if (Dispatcher.CheckAccess())
                    {
                        labelStatusVideoguard.Content = "Check logs";
                        labelStatusVideoguard.Foreground = Brushes.Red;
                    }
                    else
                    {
                        Dispatcher.Invoke(() => { labelStatusVideoguard.Content = "Check logs"; labelStatusVideoguard.Foreground = Brushes.Red; });
                    }
                }

                this.LogUI("HeartBeat response: " + s);
                logger.Info("HeartBeat response: " + s);
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (SendHeartBeat)");

                logger.Error(ex.Message + " (SendHeartBeat) ");
            }
        }

        /// <summary>
        /// Send recorder info to Videoguard
        /// </summary>
        private void SendRecorderInfo()
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
                    NoOfChannel = this.CountCameras()
                };

                var requests = new List<RecorderInfoRequest> { request };
                var json = JsonConvert.SerializeObject(requests);

                this.LogUI("Sending recorder Information: " + json);
                logger.Info("Sending recorder Information: " + json);

                string s = client.SendRecorderInfo(json);

                this.LogUI("Recorder info response: " + s);
                logger.Info("Recorder info response: " + s);
            }
            catch (Exception ex)
            {
                this.LogUI(ex.Message + " (SendRecorderInfo)");

                logger.Error(ex.Message + " (SendRecorderInfo) ");
            }
        }

        /// <summary>
        /// Read configuration for the Connector from XML
        /// </summary>
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

                    // Decrypt password from XML file
                    configurationWindow.pbPassword.Password = MainWindow.Decrypt(password);
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
                logger.Error(ex.Message + " (GetRunningVersion) ");

                return "Unknown";
            }

        }

        /// <summary>
        /// Encrypt Password for XML
        /// </summary>
        public static string Encrypt(string originalString)
        {
            try
            {
                if (String.IsNullOrEmpty(originalString))
                {
                    throw new ArgumentNullException("The string which needs to be encrypted can not be null.");
                }

                DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider();
                MemoryStream memoryStream = new MemoryStream();
                CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoProvider.CreateEncryptor(bytes, bytes), CryptoStreamMode.Write);

                StreamWriter writer = new StreamWriter(cryptoStream);
                writer.Write(originalString);
                writer.Flush();
                cryptoStream.FlushFinalBlock();
                writer.Flush();

                return Convert.ToBase64String(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message + " (Encrypt) ");
                return "";
            }
        }

        /// <summary>
        /// Decrypt Password for SeeTecConnector
        /// </summary>
        public static string Decrypt(string cryptedString)
        {
            try
            {
                if (String.IsNullOrEmpty(cryptedString))
                {
                    throw new ArgumentNullException("The string which needs to be decrypted can not be null.");
                }

                DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider();
                MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(cryptedString));
                CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoProvider.CreateDecryptor(bytes, bytes), CryptoStreamMode.Read);
                StreamReader reader = new StreamReader(cryptoStream);

                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message + " (Decrypt) ");
                return "";
            }
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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Do you really want to close the application?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
            else
            {
                logger.Info("Application is closed by user");

                if (ConnectedInstallationID != Guid.Empty)
                {
                    SDKVideoManagerFactory.GetManager().CloseConnection(ConnectedInstallationID);
                }

                SDKVideoManagerFactory.GetManager().Dispose();
                Application.Current.Shutdown();
                Environment.Exit(0);
            }


        }
    }
}

