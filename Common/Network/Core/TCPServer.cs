using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using System.Net.NetworkInformation;
using static zFramework.Misc.Loom;
using static zFramework.Misc.MessageQueue;
namespace zFramework.Network
{
    public class TCPServer : IDisposable
    {
        #region 事件
        /// <summary>
        /// 当有客户端连接上时触发
        /// </summary>
        public TCPServerEvent OnClientConnected = new TCPServerEvent();
        /// <summary>
        /// 当服务器断线时触发
        /// </summary>
        public UnityEvent OnServiceClosed = new UnityEvent();
        public class TCPServerEvent : UnityEvent<TcpClient> { }
        #endregion
        CircularBuffer recvbuffer = new CircularBuffer();
        PacketParser recvparser;


        IPEndPoint endpoint;
        TcpListener listener;
        List<TcpClient> clients = new List<TcpClient>();
        volatile bool acceptLoop = true;
        public IReadOnlyList<TcpClient> Clients => clients;
        public TCPServer(IPEndPoint endpoint)
        {
            this.endpoint = endpoint;
            recvparser = new PacketParser(recvbuffer);
        }


        public async Task ListenAsync()
        {
            lock (this)
            {
                if (listener != null)
                    throw new InvalidOperationException("Already started");
                acceptLoop = true;
                listener = new TcpListener(endpoint);
            }

            listener.Start();
            Debug.Log("[播放器] 开始监听！");
            while (acceptLoop)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = Task.Run(() => OnConnectClientAsync(client));
                    //通过该语句，程序将返回主线程上下文，其他地方一个意思
                    Post(() => OnClientConnected.Invoke(client));
                }
                catch (ObjectDisposedException e)// thrown if the listener socket is closed
                {
                    Debug.Log($"{nameof(TCPServer)}: Server was Closed! {e}");
                }
                catch (SocketException e)// Some socket error
                {
                    Debug.Log($"{nameof(TCPServer)}: Some socket error occurred! {e}");
                }
                finally
                {
                    Post(() => OnServiceClosed.Invoke());
                }
            }
        }

        public void Stop()
        {
            //先通知在线的客户端
            lock (clients)
            {
                foreach (var c in clients)
                {
                    c?.Close();
                }
                clients.Clear();
            }
            //然后关断自身
            lock (this)
            {
                if (listener == null)
                    throw new InvalidOperationException("Not started");
                acceptLoop = false;
                listener.Stop();
                listener = null;
            }
        }

        async Task OnConnectClientAsync(TcpClient client)
        {
            var clientEndpoint = client.Client.RemoteEndPoint;
            Debug.Log($"完成握手 {clientEndpoint}");
            clients.Add(client);
            try
            {
                await HandleNetworkStreamAsync(client); //连接断开时，stream 会抛出dispose相关异常,捕获避免向上传递中断了监听。
            }
            catch (Exception e)
            {
                Debug.Log($"{nameof(TCPServer)}: 客户端意外断开连接 {e}");
            }
            finally
            {
                Debug.Log($"连接断开 {clientEndpoint}");
                clients.Remove(client);
            }
        }


        async Task HandleNetworkStreamAsync(TcpClient client)
        {
            while (client.IsOnline())
            {
                var stream = client.GetStream();
                var byteCount = await recvbuffer.WriteAsync(stream);
                stream.Flush();
                if (byteCount == 0) break;//断线了
                var packets = await recvparser.ParseAsync();
                //var packets = recvparser.Parse();

                foreach (var packet in packets)
                {
                    var message = Encoding.UTF8.GetString(packet.Bytes, 0, packet.Length);
                    Debug.Log($"[播放器] 接收到控制器消息 {message},将消息压入消息队列！");
                    Enqueue(message);
                }
            }
        }

        /// <summary>
        /// 广播
        /// </summary>
        /// <param name="data"></param>
        public void BroadcastToClients(byte[] data)
        {
            Debug.Log($"Clients.Count : {Clients.Count}");
            foreach (var c in Clients)
            {
                SendMessageToClient(c, data);
            }
        }

        /// <summary>
        /// 像指定的客户端发消息
        /// </summary>
        /// <param name="c"></param>
        /// <param name="data"></param>
        public void SendMessageToClient(TcpClient c, byte[] data)
        {
            if (null != c)
            {
                try
                {
                    byte[] size = BytesHelper.GetBytes(data.Length); //简易封包协议：包长度+包体
                    var temp = new byte[size.Length + data.Length];
                    Buffer.BlockCopy(size, 0, temp, 0, size.Length);
                    Buffer.BlockCopy(data, 0, temp, size.Length, data.Length);
                    c.GetStream().Write(temp, 0, temp.Length);
                    c.GetStream().Flush();
                }
                catch (Exception e)
                {
                    Debug.Log($"{nameof(TCPServer)}: Send Message To Client Failed - {e}");
                }
            }
        }
        public void Dispose() => Stop();

        /// <summary>
        /// 获得本地IP(ipv4)
        /// </summary>
        /// <returns></returns>
        public static List<string> GetIP()
        {
            List<string> output = new List<string>();
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                NetworkInterfaceType _type1 = NetworkInterfaceType.Wireless80211;
                NetworkInterfaceType _type2 = NetworkInterfaceType.Ethernet;

                if ((item.NetworkInterfaceType == _type1 || item.NetworkInterfaceType == _type2) && item.OperationalStatus == OperationalStatus.Up)
#endif
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            output.Add(ip.Address.ToString());
                        }
                    }
                }
            }
            return output;
        }
    }
}
