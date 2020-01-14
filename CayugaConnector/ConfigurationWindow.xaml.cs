using System;
using System.IO;
using System.Windows;
using System.Xml;

namespace CayugaConnector
{
    /// <summary>
    /// Interaction logic for Configuration.xaml
    /// </summary>
    public partial class ConfigurationWindow : Window
    {
        public ConfigurationWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Save configuration button
        /// </summary>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (File.Exists(MainWindow.configFilePath))
            {
                // Load the configuration xml and read out the configured IP address and port for the clientSocket
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(MainWindow.configFilePath);

                XmlNodeList elemListIPHost = xmlDoc.GetElementsByTagName("HostIP");
                elemListIPHost[0].InnerXml = MainWindow.configurationWindow.tbHostIP.Text;

                XmlNodeList elemListPortHost = xmlDoc.GetElementsByTagName("HostPort");
                elemListPortHost[0].InnerXml = MainWindow.configurationWindow.tbHostPort.Text;

                XmlNodeList elemListUser = xmlDoc.GetElementsByTagName("User");
                elemListUser[0].InnerXml = MainWindow.configurationWindow.tbUser.Text;

                XmlNodeList elemListPassword = xmlDoc.GetElementsByTagName("Password");
                elemListPassword[0].InnerXml = MainWindow.Encrypt(MainWindow.configurationWindow.pbPassword.Password);

                XmlNodeList elemListProfile = xmlDoc.GetElementsByTagName("Profile");
                elemListProfile[0].InnerXml = MainWindow.configurationWindow.tbProfile.Text;

                XmlNodeList elemListINR = xmlDoc.GetElementsByTagName("INR");
                elemListINR[0].InnerXml = MainWindow.configurationWindow.tbINR.Text;

                XmlNodeList elemListHeartbeat = xmlDoc.GetElementsByTagName("Heartbeat");

                try
                {
                    elemListHeartbeat[0].InnerXml = MainWindow.configurationWindow.tbHeartbeat.Text;
                    MainWindow.heartbeat = Convert.ToInt32(MainWindow.configurationWindow.tbHeartbeat.Text) * 1000; // seconds in milliseconds
                }
                catch (OverflowException ex)
                {
                    MainWindow.logger.Error(ex.Message);
                    MainWindow.logger.Warn("Wrong value for Hearbeat");
                    MainWindow.logger.Warn("Set maximum value for Heartbeat");

                    elemListHeartbeat[0].InnerXml = (int.MaxValue / 1000).ToString();
                    MainWindow.heartbeat = int.MaxValue / 1000;
                }

                XmlNodeList elemListRecorderInfo = xmlDoc.GetElementsByTagName("RecorderInfo");

                try
                {
                    elemListRecorderInfo[0].InnerXml = MainWindow.configurationWindow.tbRecorderInfo.Text;
                    MainWindow.recorderInfo = Convert.ToInt32(MainWindow.configurationWindow.tbRecorderInfo.Text) * 1000; // seconds in milliseconds

                }
                catch (OverflowException ex)
                {
                    MainWindow.logger.Error(ex.Message);
                    MainWindow.logger.Warn("Wrong value for Recorder Info");
                    MainWindow.logger.Warn("Set maximum value for Recorder Info");

                    elemListRecorderInfo[0].InnerXml = (int.MaxValue / 1000).ToString();
                    MainWindow.recorderInfo = int.MaxValue / 1000;
                }

                XmlNodeList webserviceServer = xmlDoc.GetElementsByTagName("WebserviceServer");
                webserviceServer[0].InnerXml = MainWindow.configurationWindow.tbWebServer.Text;

                XmlNodeList webservicePort = xmlDoc.GetElementsByTagName("WebservicePort");
                webservicePort[0].InnerXml = MainWindow.configurationWindow.tbWebPort.Text;

                xmlDoc.Save(MainWindow.configFilePath);

                if (MainWindow.isConnectedToCayuga)
                {
                    MessageBox.Show("Configuration successfully saved. \nPlease restart application to take effect.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Configuration successfully saved. \nPlease click on reconnect (Connect to Cayuga server).", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            Close();
        }

        /// <summary>
        /// Cancel configuration button
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
