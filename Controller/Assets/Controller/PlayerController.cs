using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using zFramework.Network.Events;
using zFramework.Network;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private string ip = "127.0.0.1";
    [SerializeField]
    private int port = 8888;

    public Button playAndPause;
    public Button stop;
    public Button connectButton; //连接与断开连接
    public Dropdown dropdown;
    public bool isPlay = false;
    public PlayList playList;
    private string currentPlayFile;

    TCPChannel channel;

    void Start()
    {
        channel = new TCPChannel(ip, port);
        channel.OnDisconnected += OnChannelClosed;
        channel.OnEstablished += OnEstablished;
        channel.OnEstablishFailed += OnEstablishFailed;

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

    private void OnDestroy()
    {
        EventManager.RemoveListener(Command.Play, OnPlayResponse);
        EventManager.RemoveListener(Command.Pause, OnPauseResponse);
        EventManager.RemoveListener(Command.Stop, OnStopResponse);
        EventManager.RemoveListener(Command.PlayList, OnPlayListResponse);
    }

    #region TCPChannel Interaction
    private void OnEstablishFailed() => Debug.Log($"{nameof(PlayerController)}: 握手失败！");
    private void OnEstablished() => Debug.Log($"{nameof(PlayerController)}: 握手成功！");
    private void OnChannelClosed()
    {
        Debug.Log($"{nameof(PlayerController)}: TCP 断开连接 ...");
        connectButton.GetComponentInChildren<Text>().text = "连接服务器";
        playAndPause.GetComponentInChildren<Text>().text = "Play";
        isPlay = false;
    }
    private void SendNetMessage(Message message)
    {
        if (channel == null)
        {
            Debug.Log($"{nameof(PlayerController)}: 请先点击 “连接服务器” 构建 TCPChannel ！");
            return;
        }
        var datas = SerializeHelper.Serialize(message);
        channel?.Send(datas);
    }

    private void OnApplicationQuit() => channel?.Close();

    private async void OnConnectOrDisConnectRequired()
    {
        connectButton.interactable = false;
        var text = connectButton.GetComponentInChildren<Text>();

        if (!channel .IsConnected)
        {
            text.text = "连接中...";
            var isConnectedSuccess = await channel.ConnectAsync();
            text.text = isConnectedSuccess ? "已连接" : "连接服务器";
        }
        else
        {
            channel.Close();
            text.text = "连接服务器";
        }
        connectButton.interactable = true;
    }
    #endregion
    #region Response 
    private void OnPlayListResponse(Session session, Message message)
    {
        playList = JsonUtility.FromJson<PlayList>(message.cmdContext);
        UpdatePlayList();
    }

    private void OnStopResponse(Session session, Message message)
    {
        Debug.Log($"{nameof(PlayerController)}: 其他控制器请求停止播放视频");
        isPlay = false;
        playAndPause.GetComponentInChildren<Text>().text = "Play";
        currentPlayFile = string.Empty;
    }

    private void OnPauseResponse(Session session, Message message)
    {
        Debug.Log($"{nameof(PlayerController)}: 其他控制器请求暂停播放视频");
        isPlay = false;
        playAndPause.GetComponentInChildren<Text>().text = "Play";
    }

    private void OnPlayResponse(Session session, Message message)
    {
        Debug.Log($"{nameof(PlayerController)}: 其他控制器请求播放视频 {message.cmdContext}");
        isPlay = true;
        playAndPause.GetComponentInChildren<Text>().text = "Pause";

        // 设置 dropdown
        var index = dropdown.options.FindIndex(v => v.text == message.cmdContext);
        if (index != -1)
        {
            dropdown.SetValueWithoutNotify(index);
        }
    }
    #endregion
    #region PlayerBehaviours
    private void OnDropDownValueChanged(int arg0) => Play();
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
            isPlay = true;
            playAndPause.GetComponentInChildren<Text>().text = "Pause";

            currentPlayFile = video.name;
            Debug.Log($"正在请求播放 {currentPlayFile}...");

            var message = new Message { command = Command.Play, cmdContext = currentPlayFile };
            SendNetMessage(message);
        }
        else
        {
            Debug.Log($"请求的文件 : {dropdown.captionText.text} 不在播放列表");
        }
    }
    private void Stop()//停止播放
    {
        Debug.Log("请求停止播放视频！");
        isPlay = false;
        playAndPause.GetComponentInChildren<Text>().text = "Play";

        var message = new Message { command = Command.Stop };
        SendNetMessage(message);
    }

    private void Pause()
    {
        Debug.Log($"请求暂停视频播放！ ");
        isPlay = false;
        playAndPause.GetComponentInChildren<Text>().text = "Play";

        var message = new Message { command = Command.Pause };
        SendNetMessage(message);
    }

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

    #endregion
}

