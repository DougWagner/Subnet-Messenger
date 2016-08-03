using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
    /// Interaction logic for ServerViewWindow.xaml
    /// </summary>
    public partial class ServerViewWindow : Window
    {
        private UdpBroadcaster broadcaster;
        private IPAddress selectedAddress;
        private bool _found = false;
        public bool Found
        {
            get
            {
                return _found;
            }
        }
        public IPAddress SelectedAddress
        {
            get
            {
                return selectedAddress;
            }
        }
        private string selectedName;
        public string SelectedName
        {
            get
            {
                return selectedName;
            }
        }
        public ServerViewWindow()
        {
            broadcaster = new UdpBroadcaster(this);
            InitializeComponent();
        }

        private void ServerViewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _found = broadcaster.Broadcast();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            int idx = ServerList.SelectedIndex;
            if (idx == -1)
            {
                MessageBox.Show("Please select a server to connect to.");
                return;
            }
            selectedAddress = broadcaster.Servers[idx].IP;
            selectedName = broadcaster.Servers[idx].Name;
            Close();
        }
    }

    class UdpBroadcaster
    {
        private ServerViewWindow _window;
        private IPAddress _broadcastAddr = null;
        private List<ServerInfoPair> _servers = new List<ServerInfoPair>();
        public UdpBroadcaster(ServerViewWindow window)
        {
            _window = window;
        }

        public bool Broadcast()
        {
            GetBroadcastAddress();
            if (_broadcastAddr == null)
            {
                MessageBox.Show("Could not obtain broadcast address");
                _window.Close();
                return false;
            }
            UdpScanner scanner = new UdpScanner(_broadcastAddr);
            List<byte[]> responses = scanner.Scan();
            foreach (byte[] response in responses)
            {
                ProcessResponse(response);
            }
            if (_servers.Count == 0)
            {
                MessageBox.Show("No servers found.");
                _window.Close();
                return false;
            }
            foreach (ServerInfoPair server in _servers)
            {
                _window.ServerList.Items.Add(string.Format("{0}: {1}", server.Name, server.IP.ToString()));
            }
            return true;
        }

        private void GetBroadcastAddress()
        {
            try
            {
                List<IPAddress> addresses = new List<IPAddress>(GetAddresses()); // index 0 is local address, index 1 is subnet mask.
                _broadcastAddr = CalculateBroadcast(addresses[0], addresses[1]);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
                return;
            }
        }

        private IEnumerable<IPAddress> GetAddresses()
        {
            List<NetworkInterface> netInterfaceList = new List<NetworkInterface>(GetValidInterfaces());
            NetworkInterface selectedInterface = null;
            if (netInterfaceList.Count == 1)
            {
                selectedInterface = netInterfaceList[0];
            }
            else if (netInterfaceList.Count > 1)
            {
                NetInterfacePickerWindow window = new NetInterfacePickerWindow(netInterfaceList);
                window.ShowDialog();
                selectedInterface = window.SelectedInterface;
                if (selectedInterface == null)
                {
                    throw new Exception("Network interface is null");
                }
            }
            return GetAddressesFromInterface(selectedInterface);
        }

        private IEnumerable<IPAddress> GetAddressesFromInterface(NetworkInterface netInterface)
        {
            IPInterfaceProperties properties = netInterface.GetIPProperties();
            foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    yield return unicast.Address;
                    yield return unicast.IPv4Mask;
                }
            }
        }

        private IEnumerable<NetworkInterface> GetValidInterfaces()
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface i in interfaces)
            {
                if (i.OperationalStatus == OperationalStatus.Up && i.NetworkInterfaceType != NetworkInterfaceType.Loopback && i.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                {
                    yield return i;
                }
            }
        }

        private IPAddress CalculateBroadcast(IPAddress address, IPAddress subnet)
        {
            byte[] addressBytes = address.GetAddressBytes();
            byte[] subnetBytes = subnet.GetAddressBytes();
            if (addressBytes.Length != subnetBytes.Length)
            {
                throw new Exception("Address and Mask lengths are not equal");
            }
            byte[] broadcastBytes = new byte[addressBytes.Length];
            for (int i = 0; i < addressBytes.Length; i++)
            {
                broadcastBytes[i] = (byte)(addressBytes[i] | (subnetBytes[i] ^ 255)); // here's where the magic happens.
            }
            return new IPAddress(broadcastBytes);
        }

        private void ProcessResponse(byte[] response)
        {
            // wheeeeeeeee linq
            var ipQuery = response.Take(4);
            var sizeQuery = response.Skip(4).Take(4);
            byte[] ipBytes = new byte[4];
            byte[] sizeBytes = new byte[4];
            int i = 0;
            foreach (byte b in ipQuery)
            {
                ipBytes[i++] = b;
            }
            i = 0;
            foreach (byte b in sizeQuery)
            {
                sizeBytes[i++] = b;
            }
            IPAddress address = new IPAddress(ipBytes);
            int size = BitConverter.ToInt32(sizeBytes, 0);
            var nameQuery = response.Skip(8).Take(size);
            byte[] nameBytes = new byte[size];
            i = 0;
            foreach (byte b in nameQuery)
            {
                nameBytes[i++] = b;
            }
            string name = Encoding.Unicode.GetString(nameBytes);
            // Add IPaddress and name to list
            _servers.Add(new ServerInfoPair(name, address));
        }

        public List<ServerInfoPair> Servers
        {
            get
            {
                return _servers;
            }
        }
    }

    class UdpScanner
    {
        private Socket _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint _endpoint;
        public UdpScanner(IPAddress addr)
        {
            _endpoint = new IPEndPoint(addr, 42424);
        }

        public List<byte[]> Scan()
        {
            byte[] datagram = Encoding.Unicode.GetBytes("::REQUEST::");
            _udpSocket.SendTo(datagram, _endpoint);
            List<byte[]> responses = new List<byte[]>();
            _udpSocket.ReceiveTimeout = 1000;
            while (true)
            {
                try
                {
                    byte[] inDatagram = new byte[256];
                    _udpSocket.Receive(inDatagram);
                    responses.Add(inDatagram);
                }
                catch
                {
                    break;
                }
            }
            return responses;
        }
    }

    class ServerInfoPair
    {
        private string _name;
        private IPAddress _ip;

        public ServerInfoPair(string name, IPAddress ip)
        {
            _name = name;
            _ip = ip;
        }

        public string Name
        {
            get
            {
                return _name;
            }
        }

        public IPAddress IP
        {
            get
            {
                return _ip;
            }
        }
    }
}
