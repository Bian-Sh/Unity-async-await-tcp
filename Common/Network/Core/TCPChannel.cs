using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static zFramework.Misc.Loom;
using static zFramework.Misc.MessageQueue;
namespace zFramework.Network
{
    public class TCPChannel
    {
        public Action OnEstablished;
        public Action OnEstablishFailed;
        public Action OnDisconnected;

        TcpClient tcpClient;
        CircularBuffer recvbuffer;
        PacketParser recvparser;
        public bool IsRun { get; private set; }
        public void Close()
        {
            IsRun = false;
            tcpClient?.Close();
            tcpClient = null;
        }

        public async Task<bool> ConnectAsync(string ip, int port)
        {
            IsRun = true;
            tcpClient = new TcpClient();
            recvbuffer = new CircularBuffer();
            recvparser = new PacketParser(recvbuffer);
            tcpClient.NoDelay = true;
            try
            {
                await tcpClient.ConnectAsync(ip, port);
                Post(OnEstablished); //发布握手成功事件
                _ = Task.Run(StreamReadHandleAsync);
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(TCPChannel)}: [控制器] 连接到播放器失败 {e}");
                Close();
                Post(OnEstablishFailed); //发布握手失败事件
            }
            return IsRun;
        }

        public void SendMessage(string str)
        {
            try
            {
                if (IsRun)
                {
                    var networkStream = tcpClient.GetStream();
                    var data = Encoding.UTF8.GetBytes(str);
                    byte[] size = BytesHelper.GetBytes(data.Length);
                    var temp = new byte[size.Length + data.Length];
                    Buffer.BlockCopy(size, 0, temp, 0, size.Length);
                    Buffer.BlockCopy(data, 0, temp, size.Length, data.Length);
                    networkStream.Write(temp, 0, temp.Length);
                    networkStream.Flush();
                    Debug.Log($"[控制器] 发送到播放器消息 {str}!");
                }
                else
                {
                    Debug.LogWarning($"{nameof(TCPChannel)}: 已经与服务器断开连接！");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[控制器] 发送消息到播放器错误 {e}!");
            }
        }
        async Task StreamReadHandleAsync()
        {
            Debug.Log("开启数据读逻辑");
            try
            {
                while (IsRun&& tcpClient.IsOnline())
                {
                        var stream = tcpClient.GetStream();
                        await recvbuffer.WriteAsync(stream);
                        var packets = await recvparser.ParseAsync();
                        foreach (var packet in packets)
                        {
                            var message = Encoding.UTF8.GetString(packet.Bytes, 0, packet.Length);
                            Enqueue(message);
                        }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(TCPChannel)}: [控制器] 接收消息失败: {e}");
            }
            finally
            {
                Debug.LogError($"{nameof(TCPChannel)}: 与服务器断开连接！");
                Close();
                Post(OnDisconnected); //发布断线事件
            }
        }
    }
}