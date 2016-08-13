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
using MS.Internal.AppModel;
using MS.Win32;

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
        Thread ServerThread = null;

        public MainWindow()
        {
            InitializeComponent();
            cancelSource = new CancellationTokenSource();
            cancelToken = cancelSource.Token;
            foreach (string e in Emoji.EmojiList)
            {
                EmojiBox.Items.Add(e);
            }
            // There is probably a better way to do this...
            EmojiBox.SelectionChanged -= EmojiBox_SelectionChanged;
            EmojiBox.SelectedIndex = 0;
            EmojiBox.SelectionChanged += EmojiBox_SelectionChanged;
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

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            MessageData disconnectMessage = new MessageData("", (byte)ToServerMessageFlag.DisconnectMessage);
            StreamHelper.Send(stream, disconnectMessage);
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
                MessageData message = new MessageData(SendTextBox.Text, (byte)ToServerMessageFlag.StandardMessage);
                await StreamHelper.SendAsync(stream, message);
            }
            else if ((bool)SendToOne.IsChecked)
            {
                if (Users.SelectedItem == null)
                {
                    ChatBox.AppendText("No currently selected user.\r\n");
                    SendTextBox.Text = "";
                    return;
                }
                MessageData recipient = new MessageData(Users.SelectedValue.ToString(), (byte)ToServerMessageFlag.PrivateMessage);
                MessageData text = new MessageData(SendTextBox.Text, 0);
                await StreamHelper.SendAsync(stream, recipient);
                await StreamHelper.SendAsync(stream, text);
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
                client = new TcpClient();
                await client.ConnectAsync(address, 42424); // Connect to server at specified address.
                stream = client.GetStream(); // Obtain network stream.
                MessageData initialMessage = new MessageData(UsernameInput.Text, (byte)ToServerMessageFlag.InitialConnection);
                await StreamHelper.SendAsync(stream, initialMessage); // Send initial connection message.
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

        private void EnableChatControls()
        {
            UsernameInput.IsEnabled = false;
            ConnectButton.Content = "Disconnect";
            ConnectButton.Click -= ConnectButton_Click;
            ConnectButton.Click += DisconnectButton_Click;
            ConnectButton.IsDefault = false;
            SendButton.IsEnabled = true;
            SendButton.IsDefault = true;
            SendTextBox.IsEnabled = true;
            EmojiBox.IsEnabled = true;
        }

        private void DisableChatControls()
        {
            SendButton.IsEnabled = false;
            SendButton.IsDefault = false;
            SendTextBox.IsEnabled = false;
            UsernameInput.IsEnabled = true;
            ConnectButton.Content = "Connect";
            ConnectButton.Click -= DisconnectButton_Click;
            ConnectButton.Click += ConnectButton_Click;
            ConnectButton.IsDefault = true;
            EmojiBox.IsEnabled = false;
            Users.Items.Clear();
        }

        private async Task WaitForMessages()
        {
            MessageData incomingMessage = await StreamHelper.ReadAsync(stream);
            switch ((ToClientMessageFlag)incomingMessage.Flag)
            {
                case ToClientMessageFlag.StandardMessage: // Standard message for ChatText box.
                    ProcessStandardMessage(incomingMessage);
                    break;
                case ToClientMessageFlag.UsernameMessage: // Username message for Users list.
                    ProcessUsernameMessage(incomingMessage);
                    break;
                case ToClientMessageFlag.RemoteUserDisconnect: // Username disconnect message for Users list.
                    ProcessUserDisconnectMessage(incomingMessage);
                    break;
                case ToClientMessageFlag.UserDisconnect:
                    ProcessDisconnect();
                    break;
                case ToClientMessageFlag.ServerClose:
                    ProcessServerClose();
                    break;
                default: // Invalid flag.
                    ChatBox.AppendText("Invalid Message Flag\r\n");
                    break;
            }
        }

        private void ProcessStandardMessage(MessageData data)
        {
            ChatBox.AppendText(data.Message + "\r\n"); // Append message to chat box.
        }

        private void ProcessUsernameMessage(MessageData data)
        {
            Users.Items.Add(data.Message); // Add user to list of users.
        }

        private void ProcessUserDisconnectMessage(MessageData data)
        {
            Users.Items.Remove(data.Message); // Remove user from list of users.
        }

        private void ProcessDisconnect()
        {
            connected = false;
            cancelSource.Cancel();
            stream.Close();
            client.Close();
            DisableChatControls();
            ChatBox.AppendText("You have disconnected from the server.\r\n");
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

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.Close();
        }

        private void HostServerThread()
        {
            var HostWindow = new ServerHostWindow();
            HostWindow.Show();
            Dispatcher.Run();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (ServerThread != null && ServerThread.IsAlive)
            {
                Dispatcher.FromThread(ServerThread).InvokeShutdown();
            }
            Application.Current.Shutdown();
        }

        private void ChatBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ChatBox.ScrollToEnd();
        }

        // There is also probably a better way to do this too.
        private void EmojiBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SendTextBox.AppendText(Emoji.EmojiList.ElementAt(EmojiBox.SelectedIndex));
        }

        private void EmojiBox_DropDownOpened(object sender, EventArgs e)
        {
            EmojiBox.SelectionChanged -= EmojiBox_SelectionChanged;
            EmojiBox.SelectedIndex = -1;
            EmojiBox.SelectionChanged += EmojiBox_SelectionChanged;
        }

        private void EmojiBox_DropDownClosed(object sender, EventArgs e)
        {
            EmojiBox.SelectionChanged -= EmojiBox_SelectionChanged;
            EmojiBox.SelectedIndex = 0;
            EmojiBox.SelectionChanged += EmojiBox_SelectionChanged;
        }
    }

    static class Emoji
    {
        private static readonly List<string> _emojiList = new List<string>
        {
            "😀", "😁", "😂", "😃", "😄", "😅", "😆", "😉", "😊", "😋", "😎", "😍",
            "😘", "😗", "😙", "😚", "☺️", "🙂", "😇", "😐", "😑", "😶", "🙄", "😏",
            "😣", "😥", "😮", "😯", "😪", "😫", "😴", "😌", "😛", "😜", "😝", "😒",
            "😓", "😔", "😕", "🙃", "😲", "😷", "☹", "🙁", "😖", "😞", "😟", "😤",
            "😢", "😦", "😧", "😨", "😩", "😬", "😰", "😱", "😳", "😵", "😡", "😈",
            "👿", "💀", "👻", "👽", "💩"
        };
        public static List<string> EmojiList
        {
            get
            {
                return _emojiList;
            }
        }
    }

    enum ToClientMessageFlag : byte { StandardMessage = 1, UsernameMessage, RemoteUserDisconnect, UserDisconnect, ServerClose };
}
