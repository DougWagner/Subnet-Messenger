using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Subnet_Messenger
{
    class AsyncServer
    {
        private string _name;
        private int _port;
        private IPAddress _ip;
        private ServerRunWindow _window;
        private bool _run;
        private CancellationTokenSource closingSource;
        private CancellationToken closingToken;
        private UdpListener _udp;
        TcpListener listener;
        UserList clients = new UserList();

        public AsyncServer(int port, IPAddress ip, string name, ServerRunWindow window)
        {
            _port = port;
            _ip = ip;
            _name = name;
            _window = window;
            closingSource = new CancellationTokenSource();
            closingToken = closingSource.Token;
        }

        public async void StartServer()
        {
            _run = true;
            listener = new TcpListener(_ip, _port); // Initialize TcpListener.
            listener.Start(); // Start listener.
            _window.ServerConsole.AppendText(string.Format("Server: {0} is running on IP: {1} Port: {2}\r\n", _name, _ip.ToString(), _port));
            _udp = new UdpListener(_ip, _port, _name, _window);
            _udp.Start();
            _window.ServerConsole.AppendText("Server is listening for Udp broadcasts.\r\n");
            while (_run)
            {
                try
                {
                    TcpClient client = await listener.AcceptTcpClientAsync(); // Connect to client. 
                    string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                    _window.ServerConsole.AppendText(string.Format("User connected from IP address {0}\r\n", clientIP));
                    HandleConnections(client);
                }
                catch (Exception e)
                {
                    _window.ServerConsole.AppendText(e.Message + "\r\n");
                }
            }
        }

        public async void RemoveUser(string user)
        {
            User kickedUser = clients.GetUserByName(user);
            if (kickedUser == null)
            {
                _window.ServerConsole.AppendText(string.Format("ERROR: No user named \"{0}\" in server list", user));
                return;
            }
            string kickMessage = "You have been kicked from the server. Next time, don't be a dick.";
            byte[] bytes = Encoding.Unicode.GetBytes(kickMessage);
            await SendReturnMessageOne(kickedUser, bytes, 1);
            byte[] cancelFlag = new byte[1];
            cancelFlag[0] = 4;
            await kickedUser.Stream.WriteAsync(cancelFlag, 0, cancelFlag.Length);
            kickedUser.Stream.Close();
            kickedUser.Client.Close();
            clients.RemoveUser(kickedUser);
            kickMessage = string.Format("{0} has been kicked from the server for being a dick.", user);
            bytes = Encoding.Unicode.GetBytes(kickMessage);
            await SendReturnMessageAll(bytes, 1);
            byte[] userBytes = Encoding.Unicode.GetBytes(user);
            await SendReturnMessageAll(userBytes, 3);
            _window.UserList.Items.Remove(user);
        }

        public void Close()
        {
            _run = false;
            byte[] cancelFlag = new byte[1];
            cancelFlag[0] = 4;
            foreach (User user in clients)
            {
                user.Stream.Write(cancelFlag, 0, cancelFlag.Length);
                user.Stream.Close();
                user.Client.Close();
            }
            listener.Stop();
            _udp.Stop();
            closingSource.Cancel();
            clients.RemoveAll();
        }

        private async void HandleConnections(TcpClient client)
        {
            NetworkStream stream = client.GetStream(); // Obtain network stream from client.
            User user = new User(client, stream); // Client and Stream are stored so they can be closed when client application is terminated.
            while (_run)
            {
                try
                {
                    /* Multiple instances of WaitForMessages(pair) will be running on different 
                       task threads when multiple clients are connected to the server due to the
                       asynchronous nature of the server. */
                    await WaitForMessages(user); // Wait for new messages from current client.
                }
                catch (Exception e)
                {
                    _window.ServerConsole.AppendText(e.Message + "\r\n");
                    user.Stream.Close(); // Close the stream.
                    user.Client.Close(); // Close the client.
                    string disconnectingUser = user.Name; // Obtain username of disconnected user.
                    _window.UserList.Items.Remove(disconnectingUser);
                    string message = string.Format("{0}: {1} disconnected from the network", DateTime.Now.ToShortTimeString(), disconnectingUser);
                    byte[] messageBytes = Encoding.Unicode.GetBytes(message); // Encode disconnect message to byte array.
                    clients.RemoveUser(user); // Remove client from list of clients.
                    await SendReturnMessageAll(messageBytes, 1); // Send standard disconnect message to clients.
                    byte[] userBytes = Encoding.Unicode.GetBytes(disconnectingUser);
                    await SendReturnMessageAll(userBytes, 3); // Send User List disconnect message to clients.
                    break;
                }
            }
        }

        private async Task WaitForMessages(User user)
        {
            byte[] messageFlag = new byte[1];
            // Read just the first byte from stream to determine type of message.
            int x = await user.Stream.ReadAsync(messageFlag, 0, messageFlag.Length, closingToken);
            switch (messageFlag[0])
            {
                case 1: // Initial connection to server from client.
                    await HandleInitialConnection(user);
                    break;
                case 2: // Standard chat message from client.
                    await HandleMessage(user);
                    break;
                case 3: // Private chat message from client.
                    await HandlePrivateMessage(user);
                    break;
                default: // Invalid signal.
                    _window.ServerConsole.AppendText("Invalid Message Flag\r\n");
                    break;
            }
        }

        private async Task HandleInitialConnection(User user)
        {
            string message = await ReadMessageBytes(user);
            user.SetName(message);
            byte[] username = Encoding.Unicode.GetBytes(message);
            await SendReturnMessageAll(username, 2); // Send message to update User list in clients, excluding new client.
            foreach (var client in clients) // Send all currently connected usernames to newly connected client.
            {
                byte[] clientName = Encoding.Unicode.GetBytes(client.Name);
                await SendReturnMessageOne(user, clientName, 2);
            }
            await SendReturnMessageOne(user, username, 2); // Needed because connecting client is not added to client list yet.
            clients.AddUser(user);
            _window.UserList.Items.Add(user.Name);
            string returnMessage = string.Format("{0}: User {1} connected!", DateTime.Now.ToShortTimeString(), message);
            byte[] returnBytes = Encoding.Unicode.GetBytes(returnMessage);
            await SendReturnMessageAll(returnBytes, 1); // Send standard message to clients to show new user connected.
        }

        private async Task HandleMessage(User user)
        {
            string message = await ReadMessageBytes(user);
            string sendingUser = user.Name;
            string returnMessage = string.Format("{0} {1}: {2}", DateTime.Now.ToShortTimeString(), sendingUser, message);
            byte[] returnBytes = Encoding.Unicode.GetBytes(returnMessage);
            await SendReturnMessageAll(returnBytes, 1); // Send message to all users.
        }

        private async Task HandlePrivateMessage(User user)
        {
            string message = await ReadMessageBytes(user);
            string sendingUsername = user.Name;
            string recievingUsername = await ReadMessageBytes(user);
            User recievingUser = clients.GetUserByName(recievingUsername);
            string returnMessageToRecieving = string.Format("{0}: Private Message From {1}: {2}", DateTime.Now.ToShortTimeString(), sendingUsername, message);
            string returnMessageToSending = string.Format("{0}: Private Message To {1}: {2}", DateTime.Now.ToShortTimeString(), recievingUsername, message);
            byte[] returnMessageToRecievingBytes = Encoding.Unicode.GetBytes(returnMessageToRecieving);
            byte[] returnMessageToSendingBytes = Encoding.Unicode.GetBytes(returnMessageToSending);
            await SendReturnMessageOne(recievingUser, returnMessageToRecievingBytes, 1);
            await SendReturnMessageOne(user, returnMessageToSendingBytes, 1);
        }

        private async Task SendReturnMessageAll(byte[] message, byte flag)
        {
            /* Index 0 in sendMessage reserved for message type flag, and indices 1-4 are reserved for
               the size of the string being sent. */
            byte[] sendMessage = new byte[message.Length + 5];
            sendMessage[0] = flag;
            byte[] messageSize = BitConverter.GetBytes(message.Length);
            messageSize.CopyTo(sendMessage, 1);
            message.CopyTo(sendMessage, 5);
            foreach (var user in clients)
            {
                await user.Stream.WriteAsync(sendMessage, 0, sendMessage.Length);
            }
        }

        private async Task SendReturnMessageOne(User user, byte[] message, byte flag)
        {
            /* Index 0 in sendMessage reserved for message type flag, and indices 1-4 are reserved for
               the size of the string being sent. */
            byte[] sendMessage = new byte[message.Length + 5];
            sendMessage[0] = flag;
            byte[] messageSize = BitConverter.GetBytes(message.Length);
            messageSize.CopyTo(sendMessage, 1);
            message.CopyTo(sendMessage, 5);
            await user.Stream.WriteAsync(sendMessage, 0, sendMessage.Length);
        }

        private async Task<string> ReadMessageBytes(User user)
        {
            int x, size;
            byte[] sizeBytes = new byte[4];
            // Read 4 bytes to determine message size.
            x = await user.Stream.ReadAsync(sizeBytes, 0, sizeBytes.Length, closingToken);
            size = BitConverter.ToInt32(sizeBytes, 0);
            byte[] buffer = new byte[size];
            string message = null;
            x = await user.Stream.ReadAsync(buffer, 0, buffer.Length, closingToken);
            message += Encoding.Unicode.GetString(buffer);
            return message;
        }
    }

    class UdpListener
    {
        private IPAddress _address;
        private int _port;
        private string _name;
        //private IPEndPoint _endpoint;
        private UdpClient _listener;
        private bool _run = true;
        private ServerRunWindow _window;
        byte[] _response;

        public UdpListener(IPAddress address, int port, string name, ServerRunWindow window)
        {
            _address = address;
            _port = port;
            _name = name;
            _listener = new UdpClient(_port);
            _window = window;
            _response = GenerateResponse();
        }

        public async void Start()
        {
            try
            {
                while (_run)
                {
                    UdpReceiveResult incomingBroadcast = await _listener.ReceiveAsync();
                    string broadcastString = Encoding.Unicode.GetString(incomingBroadcast.Buffer);
                    _window.ServerConsole.AppendText(broadcastString + "\r\n");
                    _listener.Send(_response, _response.Length, incomingBroadcast.RemoteEndPoint);
                    _window.ServerConsole.AppendText("Response sent.\r\n");
                }
            }
            catch (Exception e)
            {
                _window.ServerConsole.AppendText(e.Message + "\r\n");
            }
        }

        public void Stop()
        {
            _run = false;
            _listener.Close();
        }

        private byte[] GenerateResponse()
        {
            byte[] addressBytes = _address.GetAddressBytes();
            byte[] nameBytes = Encoding.Unicode.GetBytes(_name);
            byte[] nameLength = BitConverter.GetBytes(nameBytes.Length);
            byte[] response = new byte[addressBytes.Length + nameBytes.Length + nameLength.Length];
            addressBytes.CopyTo(response, 0);
            nameLength.CopyTo(response, addressBytes.Length);
            nameBytes.CopyTo(response, addressBytes.Length + nameLength.Length);
            return response;
        }
    }

    class User
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private string _name;
        private bool _nameSet = false;

        public User(TcpClient client, NetworkStream stream)
        {
            _client = client;
            _stream = stream;
        }

        public void SetName(string name)
        {
            if (!_nameSet)
            {
                _name = name;
                _nameSet = true;
            }
        }

        public TcpClient Client
        {
            get
            {
                return _client;
            }
        }

        public NetworkStream Stream
        {
            get
            {
                return _stream;
            }
        }

        public string Name
        {
            get
            {
                return _name;
            }
        }
    }

    class UserList : IEnumerable<User> // Implements IENumerable to allow foreach loop through items
    {
        private List<User> _users = new List<User>();

        public User GetUserByName(string name)
        {
            foreach (User user in _users)
            {
                if (user.Name == name)
                    return user;
            }
            return null;
        }

        public bool UsernameExistsInList(string name)
        {
            foreach (User user in _users)
            {
                if (user.Name == name)
                    return true;
            }
            return false;
        }

        public void AddUser(User user)
        {
            _users.Add(user);
        }

        public void RemoveUser(User user)
        {
            _users.Remove(user);
        }

        public void RemoveAll()
        {
            _users.Clear();
        }

        public IEnumerator<User> GetEnumerator()
        {
            return ((IEnumerable<User>)_users).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<User>)_users).GetEnumerator();
        }
    }
}
