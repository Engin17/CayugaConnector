using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml;

namespace SeeTecConnector
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

                for (int i = 0; i < elemListIPHost.Count; i++)
                {
                    elemListIPHost[i].InnerXml = MainWindow.configurationWindow.tbHostIP.Text;
                }

                XmlNodeList elemListPortHost = xmlDoc.GetElementsByTagName("HostPort");

                for (int i = 0; i < elemListPortHost.Count; i++)
                {
                    elemListPortHost[i].InnerXml = MainWindow.configurationWindow.tbHostPort.Text;
                }

                XmlNodeList elemListUser = xmlDoc.GetElementsByTagName("User");

                for (int i = 0; i < elemListUser.Count; i++)
                {
                    elemListUser[i].InnerXml = MainWindow.configurationWindow.tbUser.Text;
                }

                XmlNodeList elemListPassword = xmlDoc.GetElementsByTagName("Password");

                for (int i = 0; i < elemListPassword.Count; i++)
                {
                    elemListPassword[i].InnerXml = MainWindow.configurationWindow.tbPassword.Text;
                }

                XmlNodeList elemListProfile = xmlDoc.GetElementsByTagName("Profile");

                for (int i = 0; i < elemListProfile.Count; i++)
                {
                    elemListProfile[i].InnerXml = MainWindow.configurationWindow.tbProfile.Text;
                }

                XmlNodeList elemListINR = xmlDoc.GetElementsByTagName("INR");

                for (int i = 0; i < elemListINR.Count; i++)
                {
                    elemListINR[i].InnerXml = MainWindow.configurationWindow.tbINR.Text;
                    MainWindow.inr = MainWindow.configurationWindow.tbINR.Text;
                }
            
                XmlNodeList elemListHeartbeat = xmlDoc.GetElementsByTagName("Heartbeat");

                for (int i = 0; i < elemListHeartbeat.Count; i++)
                {
                    elemListHeartbeat[i].InnerXml = MainWindow.configurationWindow.tbHeartbeat.Text;
                    MainWindow.heartbeat = Convert.ToInt32(MainWindow.configurationWindow.tbHeartbeat.Text) * 1000; // seconds in milliseconds
                }

                XmlNodeList elemListRecorderInfo = xmlDoc.GetElementsByTagName("SendRecorderInfo");

                for (int i = 0; i < elemListRecorderInfo.Count; i++)
                {
                    elemListRecorderInfo[i].InnerXml = MainWindow.configurationWindow.tbRecorderInfo.Text;
                    MainWindow.recorderInfo = Convert.ToInt32(MainWindow.configurationWindow.tbRecorderInfo.Text) * 1000; // seconds in milliseconds
                }

                xmlDoc.Save(MainWindow.configFilePath);
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
