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
        public bool isServerSide;
        public EndPoint IPEndPoint { get; private set; }
        public Session(TcpClient client,bool isServerSide)
        {
            this.client = client;
            this.isServerSide = isServerSide;
            // 记录IP端口信息，可供调试
            var ipEndPoint =( isServerSide?client.Client.RemoteEndPoint:client.Client.LocalEndPoint) as IPEndPoint;
            var ip = ipEndPoint.Address.ToString();
            var port = ipEndPoint.Port;
            IPEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

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
            throw new Exception("连接已断开");
        }

        public void Close()
        {
            client.Close();
            recvbuffer.Close();
        }

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
