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
        public static bool IsOnline(this TcpClient c)
        {
            return !((c.Client.Poll(1000, SelectMode.SelectRead) && (c.Client.Available == 0)) || !c.Client.Connected);
        }
    }
}