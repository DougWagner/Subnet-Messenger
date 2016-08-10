using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Subnet_Messenger
{
    /// <summary>
    /// Static helper class for sending and recieving MessageData objects via NetworkStream
    /// Byte array is formatted as follows:
    /// [0-3] - Message Size (Int32)
    /// [4-n] - MessageData.MessageBuffer
    /// </summary>
    static class StreamHelper
    {
        public static void Send(NetworkStream stream, MessageData data)
        {
            byte[] buffer = GenerateByteArray(data.MessageBuffer);
            stream.Write(buffer, 0, buffer.Length);
        }

        public static async Task SendAsync(NetworkStream stream, MessageData data)
        {
            byte[] buffer = GenerateByteArray(data.MessageBuffer);
            await stream.WriteAsync(buffer, 0, buffer.Length);
        }

        public static MessageData Read(NetworkStream stream)
        {
            byte[] messageSize = new byte[4];
            stream.Read(messageSize, 0, messageSize.Length);
            byte[] buffer = new byte[BitConverter.ToInt32(messageSize, 0)];
            stream.Read(buffer, 0, buffer.Length);
            MessageData data = new MessageData(buffer);
            return data;
        }
        
        public static async Task<MessageData> ReadAsync(NetworkStream stream)
        {
            byte[] messageSize = new byte[4];
            await stream.ReadAsync(messageSize, 0, messageSize.Length);
            byte[] buffer = new byte[BitConverter.ToInt32(messageSize, 0)];
            await stream.ReadAsync(buffer, 0, buffer.Length);
            MessageData data = new MessageData(buffer);
            return data;
        }

        private static byte[] GenerateByteArray(byte[] buffer)
        {
            byte[] arr = new byte[buffer.Length + 4];
            BitConverter.GetBytes(buffer.Length).CopyTo(arr, 0);
            buffer.CopyTo(arr, 4);
            return arr;
        }
    }
}
