using System.Net.Sockets;
namespace zFramework.Network
{
    /// <summary>
    /// TcpClient.Connected: 属性获取截止到最后一次 I/O 操作时的 Client 套接字的连接状态。
    /// C# TcpClient在连接成功后，对方关闭了网络连接是不能及时的检测到断开的，
    /// 故而使用此扩展检测连接状态
    /// </summary>
    public static class TcpClientEx
    {
        public static bool IsOnline(this TcpClient client)
        {
            try
            {
                if (client != null && client.Client != null && client.Client.Connected)
                {
                    /* 根据 Poll 的文档：
                        * 当将 SelectMode.SelectRead 作为参数传递给 Poll 方法时，它将返回
                        * -如果调用了 Socket.Listen(Int32) 并且连接正在等待，则为 true；
                        * -如果有数据可供读取，则为 true；
                        * -如果连接已被关闭、重置或终止，则为 true；
                        * 否则，返回 false
                        */

                    // Socket 已连接并且已被标记为非阻塞（使用 Socket.Blocking = false）
                    // 但不能保证 Socket 会保持连接状态
                    return !(client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0);
                }
                else
                {
                    return false;
                }
            }
            catch (SocketException se)
            {
                // 在这里编写你的异常处理代码。
                UnityEngine.Debug.Log($"{nameof(TcpClientEx)}:  {se}");
                return false;
            }
        }
    }
}