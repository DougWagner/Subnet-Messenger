using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

namespace Subnet_Messenger
{
    /// <summary>
    /// Interaction logic for ServerRunWindow.xaml
    /// </summary>
    public partial class ServerRunWindow : Window
    {
        private string _name;
        private int _port;
        private AsyncServer _server;

        public ServerRunWindow(int port, string name)
        {
            _port = port;
            _name = name;
            InitializeComponent();
        }

        public void Init()
        {
            string hostName = Dns.GetHostName();
            IPHostEntry hostEntry = Dns.GetHostEntry(hostName);

            IPAddress ipv4 = GetIPv4(hostEntry);
            if (ipv4 == null)
            {
                MessageBox.Show("Could not get IP address.");
                Close();
                return;
            }
            _server = new AsyncServer(_port, ipv4, _name, this);
            _server.StartServer();
        }

        private IPAddress GetIPv4(IPHostEntry host)
        {
            foreach (IPAddress address in host.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    return address;
                }
            }
            return null;
        }

        private void KickUser_Click(object sender, RoutedEventArgs e)
        {
            if (UserList.SelectedItem == null)
            {
                MessageBox.Show("No user selected.", "Error");
                return;
            }
            if (MessageBox.Show(string.Format("Are you sure you want to kick User: {0}", UserList.SelectedValue.ToString()), "Choose wisely, someone's life depends on it", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _server.RemoveUser(UserList.SelectedValue.ToString());
            }
        }

        private void CloseServer_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ServerRunWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _server.Close();
        }

        private void ServerConsole_TextChanged(object sender, TextChangedEventArgs e)
        {
            ServerConsole.ScrollToEnd();
        }
    }
}
