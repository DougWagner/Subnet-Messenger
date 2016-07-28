using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
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
    /// Interaction logic for NetInterfacePickerWindow.xaml
    /// </summary>
    public partial class NetInterfacePickerWindow : Window
    {
        private List<NetworkInterface> _netInterfaceList;
        private NetworkInterface _selectedInterface = null;
        public NetInterfacePickerWindow(List<NetworkInterface> iList)
        {
            InitializeComponent();
            _netInterfaceList = iList;
        }

        private void NetInterfacePickerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (NetworkInterface i in _netInterfaceList)
            {
                NetInterfaceBox.Items.Add(string.Format("{0}: {1}", i.NetworkInterfaceType, i.Name));
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (NetInterfaceBox.SelectedIndex == -1)
            {
                MessageBox.Show("You have not selected an interface.", "Error");
                return;
            }
            _selectedInterface = _netInterfaceList[NetInterfaceBox.SelectedIndex];
            Close();
        }

        public NetworkInterface SelectedInterface
        {
            get
            {
                return _selectedInterface;
            }
        }
    }
}

