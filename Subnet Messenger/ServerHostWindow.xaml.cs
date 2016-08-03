using System;
using System.Collections.Generic;
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

namespace Subnet_Messenger
{
    /// <summary>
    /// Interaction logic for ServerHostWindow.xaml
    /// </summary>
    public partial class ServerHostWindow : Window
    {
        bool isHosting = false;
        public ServerHostWindow()
        {
            InitializeComponent();
        }

        private void HostButton_Click(object sender, RoutedEventArgs e)
        {
            int port;
            if (ServerNameInput.Text == "")
            {
                MessageBox.Show("Server does not have a name.", "Error");
                return;
            }
            if (Int32.TryParse(PortInput.Text, out port))
            {
                if (port <= 1024 || port > 65535)
                {
                    MessageBox.Show("Invalid port number.", "Error");
                    return;
                }
            }
            else
            {
                MessageBox.Show("Invalid port entry.", "Error");
                return;
            }
            var RunWindow = new ServerRunWindow(port, ServerNameInput.Text);
            RunWindow.Show();
            RunWindow.Init();
            isHosting = true;
            Close();
        }

        private void ServerHostWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!isHosting)
            {
                Dispatcher.InvokeShutdown();
            }
        }
    }
}
