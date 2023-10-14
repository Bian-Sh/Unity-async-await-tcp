using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static zFramework.Network.Misc.MessageQueue;

namespace zFramework.Network
{
    public class Session : IDisposable
    {
        public bool IsAlive => client.IsOnline();
        public EndPoint RemoteEndPoint => client.Client.RemoteEndPoint;
        public EndPoint LocalEndPoint => client.Client.LocalEndPoint;
        public Session(TcpClient client)
        {
            this.client = client;
            recvbuffer = new CircularBuffer();
            recvparser = new PacketParser(recvbuffer);
        }

        public void Send(byte[] data)
        {
            if (IsAlive)
            {
                byte[] size = BytesHelper.GetBytes(data.Length); //简易封包协议：包长度+包体
                var temp = new byte[size.Length + data.Length];
                Buffer.BlockCopy(size, 0, temp, 0, size.Length);
                Buffer.BlockCopy(data, 0, temp, size.Length, data.Length);
                client.GetStream().Write(temp, 0, temp.Length);
                client.GetStream().Flush();
            }
        }

        public async Task HandleNetworkStreamAsync()
        {
            //连接断开时，stream 会抛出dispose相关异常,捕获避免向上传递中断了监听。
            while (IsAlive)
            {
                var stream = client.GetStream();
                var byteCount = await recvbuffer.WriteAsync(stream);
                stream.Flush();
                if (byteCount == 0) break;//断线了
                var packets = await recvparser.ParseAsync();
                foreach (var packet in packets)
                {
                    var message = Encoding.UTF8.GetString(packet.Bytes, 0, packet.Length);
                    Enqueue(this, message);
                }
            }
        }

        public void Close()
        {
            client.Close();
            recvbuffer.Close();
        }

        internal Task ConnectAsync(string ip, int port) => client.ConnectAsync(ip, port);

        public void Dispose()
        {
            Close();
            client = null;
        }

        public bool IsDisposed => client == null;
        TcpClient client;
        readonly CircularBuffer recvbuffer;
        readonly PacketParser recvparser;
    }
}
