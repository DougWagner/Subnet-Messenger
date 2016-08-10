using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Subnet_Messenger
{
    /// <summary>
    /// Object for storage of messages sent via TCP.
    /// Constructed from string and byte flag, or existing formatted byte array.
    /// Byte array is formatted as follows:
    /// [0] - message flag
    /// [1-4] - string bytes size (Int32)
    /// [5-n] - utf-8 encoded string bytes
    /// </summary>
    class MessageData
    {
        protected string _message;
        protected byte _flag;
        protected byte[] _mBuffer;

        public MessageData(string message, byte flag)
        {
            _message = message;
            _flag = flag;
            _mBuffer = GetBytes();
        }

        public MessageData(byte[] bytes)
        {
            _flag = bytes[0];
            _message = Encoding.UTF8.GetString(bytes, 5, BitConverter.ToInt32(bytes, 1));
            _mBuffer = bytes;
        }

        private byte[] GetBytes()
        {
            byte[] stringBytes = Encoding.UTF8.GetBytes(_message);
            byte[] sizeBytes = BitConverter.GetBytes(stringBytes.Length);
            byte[] message = new byte[stringBytes.Length + sizeBytes.Length + 1];
            message[0] = _flag;
            sizeBytes.CopyTo(message, 1);
            stringBytes.CopyTo(message, 5);
            return message;
        }

        public string Message
        {
            get
            {
                return _message;
            }
        }

        public byte Flag
        {
            get
            {
                return _flag;
            }
        }

        public byte[] MessageBuffer
        {
            get
            {
                return _mBuffer; // *tips fedora*
            }
        }
    }

    /* NONE OF THIS WILL WORK ;_; ALL THAT TYPING FOR NOTHING
    /// <summary>
    /// Object for storage of Private messages through TCP
    /// Inherits from MessageData
    /// Byte array is formatted as follows:
    /// [0] - message flag
    /// [1-4] - username size (Int32)
    /// [5-8] - message size (Int32)
    /// [9-n] - username (utf-8 encoded string)
    /// [n+1-m] - message (utf-8 encoded string)
    /// </summary>
    class PrivateMessageData : MessageData
    {
        protected string _recipient;

        public PrivateMessageData(string recipient, string message, byte flag)
        {
            _recipient = recipient;
            _message = message;
            _flag = flag;
            _mBuffer = GetBytes();
        }

        public PrivateMessageData(byte[] bytes)
        {
            _flag = bytes[0];
            int usernameSize = BitConverter.ToInt32(bytes, 1);
            int messageSize = BitConverter.ToInt32(bytes, 5);
            _recipient = Encoding.UTF8.GetString(bytes, 9, usernameSize);
            _message = Encoding.UTF8.GetString(bytes, 9 + usernameSize, messageSize);
            _mBuffer = bytes;
        }

        private byte[] GetBytes()
        {
            byte[] usernameBytes = Encoding.UTF8.GetBytes(_recipient);
            byte[] usernameSizeBytes = BitConverter.GetBytes(usernameBytes.Length);
            byte[] stringBytes = Encoding.UTF8.GetBytes(_message);
            byte[] sizeBytes = BitConverter.GetBytes(stringBytes.Length);
            byte[] message = new byte[usernameBytes.Length + usernameSizeBytes.Length + stringBytes.Length + sizeBytes.Length + 1];
            message[0] = _flag;
            usernameSizeBytes.CopyTo(message, 1);
            sizeBytes.CopyTo(message, 5);
            usernameBytes.CopyTo(message, 9);
            stringBytes.CopyTo(message, 9 + usernameBytes.Length);
            return message;
        }

        public string Recipient
        {
            get
            {
                return _recipient;
            }
        }
    }*/
}
