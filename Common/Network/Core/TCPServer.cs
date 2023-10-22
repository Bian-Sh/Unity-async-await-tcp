using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Net.NetworkInformation;
using static zFramework.Network.Misc.Loom;

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
        ///  当有客户端断开时触发
        /// </summary>
        public TCPServerEvent OnClientDisconnected = new TCPServerEvent();
        /// <summary>
        /// 当服务器断线时触发
        /// </summary>
        public UnityEvent OnServiceClosed = new UnityEvent();
        public class TCPServerEvent : UnityEvent<Session> { }
        #endregion

        public IReadOnlyList<Session> Sessions => sessions;
        public TCPServer(string ip, int port)
        {
            this.endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
            listener = new TcpListener(endpoint);
        }
        public async Task ListenAsync()
        {
            acceptLoop = true;
            listener.Start();
            Debug.Log("[播放器] 开始监听！");
            while (acceptLoop)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    var session = new Session(client, true);
                    sessions.Add(session);
                    Post(() => OnClientConnected?.Invoke(session)); //通过该语句，程序将返回主线程上下文，其他地方一个意思
                    _ = Task.Run(()=>ReceiveDataAsync(session));
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

        private async Task ReceiveDataAsync(Session session)
        {
            try
            {
                await session.HandleNetworkStreamAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(TCPServer)}: [播放器] 接收消息失败: {e}");
                DisaliveSessionHandle(session);
            }
        }

        public void Stop()
        {
            //先通知在线的客户端
            lock (sessions)
            {
                foreach (var c in sessions)
                {
                    c?.Close();
                }
                sessions.Clear();
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

        /// <summary>
        /// 广播
        /// </summary>
        /// <param name="data"></param>
        public void Broadcast(byte[] data)
        {
            foreach (var session in Sessions)
            {
                Send(session, data);
            }
        }

        /// <summary>
        /// 广播
        /// </summary>
        /// <param name="data"></param>
        public void BroadcastOthers(Session session, byte[] data)
        {
            foreach (var item in Sessions)
            {
                if (item != session)
                {
                    Send(item, data);
                }
            }
        }

        public void Send(Session session, byte[] data)
        {
            try
            {
                session?.Send(data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{nameof(TCPServer)}: Send Message To Client Failed - {e}");
                DisaliveSessionHandle(session);
            }
        }

        private void DisaliveSessionHandle(Session session)
        {
            session.Close();
            sessions.Remove(session);
            Post(() => OnClientDisconnected?.Invoke(session));
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

        IPEndPoint endpoint;
        TcpListener listener;
        List<Session> sessions = new List<Session>();
        volatile bool acceptLoop = true;
    }
}
