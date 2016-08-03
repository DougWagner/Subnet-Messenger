using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Windows.Threading;
using System.Net;
using System.Net.Sockets;

namespace Subnet_Messenger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TcpClient client = null;
        private NetworkStream stream = null;
        private CancellationTokenSource cancelSource;
        private CancellationToken cancelToken;
        private bool connected = false;
        //private List<Thread> windowThreads = new List<Thread>();
        Thread ServerThread = null;

        public MainWindow()
        {
            InitializeComponent();
            cancelSource = new CancellationTokenSource();
            cancelToken = cancelSource.Token;
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsernameInput.Text == "")
            {
                ChatBox.AppendText("Please enter a username before connecting.\r\n");
                return;
            }
            ServerViewWindow window = new ServerViewWindow();
            window.ShowDialog();
            if (window.Found)
            {
                Connect(window.SelectedAddress);
            }
        }

        private void HostButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServerThread != null && ServerThread.IsAlive)
            {
                MessageBox.Show("A server is already hosted on this machine.", "Error");
                return;
            }
            ServerThread = new Thread(new ThreadStart(HostServerThread));
            ServerThread.SetApartmentState(ApartmentState.STA);
            ServerThread.IsBackground = true;
            ServerThread.Start();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (SendTextBox.Text.Length == 0)
            {
                return; // Do nothing.
                /* Is it really possible to do nothing? By "doing nothing", you are actually not doing nothing, 
                 * because by doing nothing, you are doing something, and that something is nothing. So you are 
                 * still doing something while doing nothing, making it impossible to truly do nothing.
                 */
            }
            if ((bool)SendToAll.IsChecked)
            {
                await SendMessage(SendTextBox.Text, 2);
            }
            else if ((bool)SendToOne.IsChecked)
            {
                if (Users.SelectedItem == null)
                {
                    ChatBox.AppendText("No currently selected user.\r\n");
                    SendTextBox.Text = "";
                    return;
                }
                await SendMessage(SendTextBox.Text, 3);
                await SendMessage(Users.SelectedValue.ToString(), 0);
            }
            SendTextBox.Text = "";
        }

        private async void Connect(IPAddress address)
        {
            if (address == null)
            {
                MessageBox.Show("IPAddress is null for some reason.", "Error");
                return;
            }
            if (cancelSource.IsCancellationRequested)
            {
                cancelSource = new CancellationTokenSource();
                cancelToken = cancelSource.Token;
            }
            try
            {
                //IPAddress address = IPAddress.Parse(IPInput.Text);
                client = new TcpClient();
                await client.ConnectAsync(address, 42424); // Connect to server at specified address.
                stream = client.GetStream(); // Obtain network stream.
                await SendMessage(UsernameInput.Text, 1); // Send initial connection message.
            }
            catch (Exception ex)
            {
                ChatBox.AppendText(ex.Message + "\r\n");
                return;
            }
            EnableChatControls();
            connected = true;
            while (connected)
            {
                try
                {
                    await WaitForMessages();
                }
                catch (Exception ex)
                {
                    ChatBox.AppendText(ex.Message + "\r\n");
                    DisableChatControls();
                    break;
                }
            }
        }

        private async Task SendMessage(string message, byte flag)
        {
            byte[] stringBytes = Encoding.Unicode.GetBytes(message);
            byte[] sendMessage;
            byte[] messageSize = BitConverter.GetBytes(stringBytes.Length);
            if (flag == 0)
            {
                sendMessage = new byte[stringBytes.Length + 4];
                messageSize.CopyTo(sendMessage, 0);
                stringBytes.CopyTo(sendMessage, 4);
            }
            else
            {
                sendMessage = new byte[stringBytes.Length + 5];
                sendMessage[0] = flag;
                messageSize.CopyTo(sendMessage, 1);
                stringBytes.CopyTo(sendMessage, 5);
            }
            await stream.WriteAsync(sendMessage, 0, sendMessage.Length);
        }

        private void EnableChatControls()
        {
            //IPInput.IsEnabled = false;
            UsernameInput.IsEnabled = false;
            ConnectButton.IsEnabled = false;
            ConnectButton.IsDefault = false;
            SendButton.IsEnabled = true;
            SendButton.IsDefault = true;
            SendTextBox.IsEnabled = true;
        }

        private void DisableChatControls()
        {
            SendButton.IsEnabled = false;
            SendButton.IsDefault = false;
            SendTextBox.IsEnabled = false;
            //IPInput.IsEnabled = true;
            UsernameInput.IsEnabled = true;
            ConnectButton.IsEnabled = true;
            ConnectButton.IsDefault = true;
            Users.Items.Clear();
        }

        private async Task WaitForMessages()
        {
            byte[] messageFlag = new byte[1];
            int x = await stream.ReadAsync(messageFlag, 0, messageFlag.Length, cancelToken);
            switch (messageFlag[0])
            {
                case 1: // Standard message for ChatText box.
                    await ProcessStandardMessage();
                    break;
                case 2: // Username message for Users list.
                    await ProcessUsernameMessage();
                    break;
                case 3: // Username disconnect message for Users list.
                    await ProcessUserDisconnectMessage();
                    break;
                case 4:
                    ProcessServerClose();
                    break;
                default: // Invalid flag.
                    ChatBox.AppendText("Invalid Message Flag\r\n");
                    break;
            }
        }

        private async Task ProcessStandardMessage()
        {
            string message = await ReadMessageBytes();
            message = message + "\r\n";
            ChatBox.AppendText(message); // Append message to chat box.
        }

        private async Task ProcessUsernameMessage()
        {
            string message = await ReadMessageBytes();
            Users.Items.Add(message); // Add user to list of users.
        }

        private async Task ProcessUserDisconnectMessage()
        {
            string message = await ReadMessageBytes();
            Users.Items.Remove(message); // Remove user from list of users.
        }

        private void ProcessServerClose()
        {
            connected = false;
            cancelSource.Cancel();
            stream.Close();
            client.Close();
            ChatBox.AppendText("The server has been closed. You have been disconnected.\r\n");
            DisableChatControls();
        }

        private async Task<string> ReadMessageBytes()
        {
            int x, size;
            byte[] sizeBytes = new byte[4];
            x = await stream.ReadAsync(sizeBytes, 0, sizeBytes.Length, cancelToken);
            size = BitConverter.ToInt32(sizeBytes, 0);
            byte[] buffer = new byte[size];
            string message = null;
            x = await stream.ReadAsync(buffer, 0, buffer.Length, cancelToken);
            message += Encoding.Unicode.GetString(buffer);
            return message;
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.Close();
        }

        private void MenuHostServer_Click(object sender, RoutedEventArgs e)
        {
            /*
            var HostWindow = new ServerHostWindow();
            HostWindow.Show();
            */
            Thread HostWindowThread = new Thread(new ThreadStart(HostServerThread));
            //windowThreads.Add(HostWindowThread);
            HostWindowThread.SetApartmentState(ApartmentState.STA);
            HostWindowThread.IsBackground = true;
            HostWindowThread.Start();
        }

        private void HostServerThread()
        {
            var HostWindow = new ServerHostWindow();
            HostWindow.Show();
            Dispatcher.Run();
        }

        private void MenuViewServer_Click(object sender, RoutedEventArgs e)
        {
            /*
            var ViewWindow = new ServerViewWindow();
            ViewWindow.Show();
            Thread ViewWindowThread = new Thread(new ThreadStart(ViewServerThread));
            windowThreads.Add(ViewWindowThread);
            ViewWindowThread.SetApartmentState(ApartmentState.STA);
            ViewWindowThread.IsBackground = true;
            ViewWindowThread.Start();
            */
            if (UsernameInput.Text == "")
            {
                ChatBox.AppendText("Please enter a username before connecting.\r\n");
                return;
            }
            ServerViewWindow window = new ServerViewWindow();
            window.ShowDialog();
            Connect(window.SelectedAddress);
        }

        private void ViewServerThread()
        {
            var ViewWindow = new ServerViewWindow();
            ViewWindow.Show();
            Dispatcher.Run();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            /*foreach (Thread thread in windowThreads)
            {
                Dispatcher.FromThread(thread).InvokeShutdown();
            }*/
            //Dispatcher.FromThread(ServerThread).InvokeShutdown();
            if (ServerThread != null && ServerThread.IsAlive)
            {
                Dispatcher.FromThread(ServerThread).InvokeShutdown();
            }
            Application.Current.Shutdown();
            //Environment.Exit(0);
        }

        private void ChatBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ChatBox.ScrollToEnd();
        }

    }
}
