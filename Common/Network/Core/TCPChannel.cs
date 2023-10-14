namespace zFramework.Network
{
    using System;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using UnityEngine;
    using static Misc.Loom;

    public class TCPChannel
    {
        public Action OnEstablished;
        public Action OnEstablishFailed;
        public Action OnDisconnected;
        public bool IsConnected { get; private set; }

        public TCPChannel(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
            var client = new TcpClient();
            client.NoDelay = true;
            session = new Session(client);
            recvbuffer = new CircularBuffer();
            recvparser = new PacketParser(recvbuffer);
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (IsConnected)
                {
                    return true;
                }
                await session.ConnectAsync(ip, port);
                IsConnected = true;
                Post(OnEstablished); //发布握手成功事件
                try
                {
                    _ = Task.Run(session.HandleNetworkStreamAsync);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{nameof(TCPChannel)}: [控制器] 接收消息失败: {e}");
                    DisaliveSessionHandle(session);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(TCPChannel)}: [控制器] 连接到播放器失败 {e}");
                Close();
                Post(OnEstablishFailed); //发布握手失败事件
            }
            return IsConnected;
        }

        public void Send(byte[] datas)
        {
            try
            {
                if (IsConnected)
                {
                    session.Send(datas);
                }
                else
                {
                    Debug.LogWarning($"{nameof(TCPChannel)}: 已经与服务器断开连接！");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[控制器] 发送消息到播放器错误 {e}!");
                DisaliveSessionHandle(session);
            }
        }

        public void Close()
        {
            IsConnected = false;
            session.Dispose();
        }

        private void DisaliveSessionHandle(Session session)
        {
            IsConnected = false;
            session.Close();
            Post(OnDisconnected); //发布断线事件
        }

        readonly string ip;
        readonly int port;
        readonly Session session;
        readonly CircularBuffer recvbuffer;
        readonly PacketParser recvparser;
    }
}