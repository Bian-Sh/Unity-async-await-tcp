using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using static zFramework.Misc.MessageQueue;
using static zFramework.Misc.Loom;
using zFramework.Events;

public class PlayerController : MonoBehaviour
{

    public Button playAndPause;
    public Button stop;
    public Button connectButton; //连接与断开连接

    public Dropdown dropdown;

    public bool isPlay = false;
    public PlayList playList;
    private string currentPlayFile;
    TcpClient tcpClient;
    bool isRun = false;


    CircularBuffer recvbuffer = new CircularBuffer();
    PacketParser recvparser;

    void Start()
    {
        playAndPause.onClick.AddListener(OnPlayAndPauseButtonClicked);
        stop.onClick.AddListener(Stop);
        connectButton.onClick.AddListener(OnConnectOrDisConnectRequired);
        dropdown.onValueChanged.AddListener(OnDropDownValueChanged);
        stop.GetComponentInChildren<Text>().text = "Stop";
        playAndPause.GetComponentInChildren<Text>().text = "Play";
        EventManager.AddListener(Command.Play, OnPlayResponse);
        EventManager.AddListener(Command.Pause, OnPauseResponse);
        EventManager.AddListener(Command.Stop, OnStopResponse);
        EventManager.AddListener(Command.PlayList, OnPlayListResponse);
    }


    private async void OnConnectOrDisConnectRequired()
    {

        connectButton.interactable = false;
        var text = connectButton.GetComponentInChildren<Text>();
        if (!isRun)
        {
            text.text = "连接中...";
            var isConnectedSuccess = await ConnectAsTcpClientAsync();
            text.text = isConnectedSuccess ? "已连接" : "连接服务器";
        }
        else
        {
            tcpClient.Close();
            tcpClient = null;
            isRun = false;
            connectButton.GetComponentInChildren<Text>().text = "连接服务器";
        }
        connectButton.interactable = true;
    }

    private async Task<bool> ConnectAsTcpClientAsync()
    {
        isRun = true;
        tcpClient = new TcpClient();
        recvparser = new PacketParser(recvbuffer);
        tcpClient.NoDelay = true;
        try
        {
            await tcpClient.ConnectAsync("127.0.0.1", 8888);
            _ = Task.Run(StreamReadHandleAsync);

        }
        catch (Exception e)
        {
            Debug.LogError($"{nameof(PlayerController)}: [控制器] 连接到播放器失败 {e}");
            Close();
        }
        return isRun;
    }

    async Task StreamReadHandleAsync()
    {
        Debug.Log("开启数据读逻辑");
        try
        {
            while (isRun && tcpClient.IsOnline())
            {
                var stream = tcpClient.GetStream();
                await recvbuffer.WriteAsync(stream);
                var packets = await recvparser.ParseAsync();
                foreach (var packet in packets)
                {
                    var message = Encoding.UTF8.GetString(packet.Bytes, 0, packet.Length);
                    Debug.Log($"[控制器] 接收到播放器消息 {message}， 压入消息队列？");
                    Enqueue(message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{nameof(PlayerController)}: [控制器] 接收消息失败: {e}");
        }
        finally
        {
            Debug.LogError($"{nameof(PlayerController)}: 与服务器断开连接！");
            Close();
        }
    }

    void Close()
    {
        tcpClient?.Close();
        tcpClient = null;
        isRun = false;
        // 此函数可能在非主线程执行，需要
        Post(() => 
        {
            connectButton.GetComponentInChildren<Text>().text = "连接服务器";
            playAndPause.GetComponentInChildren<Text>().text = "Play";
            isPlay = false;
        });
    }

    void SendNetMessage(string str)
    {
        try
        {
            if (isRun)
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
                Debug.LogWarning($"{nameof(PlayerController)}: 已经与服务器断开连接！");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[控制器] 发送消息到播放器错误 {e}!");
        }
    }

    private void OnPlayListResponse(string obj)
    {
        Message m = JsonUtility.FromJson<Message>(obj);
        playList = JsonUtility.FromJson<PlayList>(m.cmdContext);
        UpdatePlayList();
    }

    private void OnStopResponse(string obj)
    {
        Message m = JsonUtility.FromJson<Message>(obj);
        isPlay = false;
        playAndPause.GetComponentInChildren<Text>().text = "Play";
        currentPlayFile = string.Empty;
        Debug.LogWarning($"{nameof(PlayerController)}: 播放停止 !");
    }

    private void OnPauseResponse(string obj)
    {
        Message m = JsonUtility.FromJson<Message>(obj);
        isPlay = false;
        playAndPause.GetComponentInChildren<Text>().text = "Play";
        Debug.LogWarning($"{nameof(PlayerController)}: 播放暂停 !");
    }

    private void OnPlayResponse(string obj)
    {
        Message m = JsonUtility.FromJson<Message>(obj);
        isPlay = true;
        playAndPause.GetComponentInChildren<Text>().text = "Pause";
        if (null != m)
        {
            VideoItem i = JsonUtility.FromJson<VideoItem>(m.cmdContext);
            Debug.Log($"{nameof(PlayerController)}: 确认播放 ：{i.name}\n文件备注 ：{i.description}");
        }
        else
        {
            Debug.LogWarning($"{nameof(PlayerController)}: 播放失败 ! ");
        }
    }

    private void OnDropDownValueChanged(int arg0)
    {
        Play();
    }


    #region PlayerBehaviours

    private void Stop()//停止播放
    {
        Debug.Log("请求停止播放视频！");
        SendNetMessage(JsonUtility.ToJson(new Message { command = Command.Stop }));
    }
    private void Play()
    {
        if (dropdown.options.Count == 0)
        {
            Debug.Log("播放列表为空！");
            return;
        }

        if (isPlay && currentPlayFile == dropdown.captionText.text)
        {
            Debug.Log($"{currentPlayFile} 正在播放中！");
            return;
        }

        VideoItem video = playList.items.Find(v => v.name == dropdown.captionText.text);
        if (null != video)
        {
            currentPlayFile = video.name;
            Debug.Log($"正在请求播放 {currentPlayFile}...");
            SendNetMessage(JsonUtility.ToJson(new Message { command = Command.Play, cmdContext = currentPlayFile }));
        }
        else
        {
            Debug.Log($"请求的文件 : {dropdown.captionText.text} 不在播放列表");
        }
    }

    private void Pause()
    {
        Debug.Log($"请求暂停视频播放！ ");
        SendNetMessage(JsonUtility.ToJson(new Message { command = Command.Pause }));
    }

    //登陆后请求更新播放列表
    private void RequestPlayList() => SendNetMessage(JsonUtility.ToJson(new Message { command = Command.PlayList }));

    //更新播放列表
    private void UpdatePlayList()
    {
        Debug.Log("updateplaylist");
        if (null != playList && playList.items.Count > 0)
        {
            dropdown.ClearOptions();
            var files = playList.items.Select(v => v.name).ToList();
            dropdown.AddOptions(files);
            Debug.Log("列表更新完毕！");
        }
    }

    #endregion


    private void OnPlayAndPauseButtonClicked()//播放/暂停
    {
        if (isPlay)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }
    private void OnDestroy()
    {
        isRun = false;
        //软件退出主动关闭socket
        tcpClient?.Close();
    }
}

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