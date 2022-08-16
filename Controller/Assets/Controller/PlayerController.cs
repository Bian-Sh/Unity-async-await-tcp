using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using zFramework.Events;
using zFramework.Network;

public class PlayerController : MonoBehaviour
{
    public Button playAndPause;
    public Button stop;
    public Button connectButton; //连接与断开连接
    public Dropdown dropdown;
    public bool isPlay = false;
    public PlayList playList;
    private string currentPlayFile;

    TCPChannel channel;
    private void Awake()
    {
        channel = new TCPChannel();
        channel.OnClosed += OnChannelClosed;
        channel.OnDisconnected += OnDisconnected;
    }
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
    #region TCP 
    private void OnChannelClosed()
    {
        connectButton.GetComponentInChildren<Text>().text = "连接服务器";
        playAndPause.GetComponentInChildren<Text>().text = "Play";
        isPlay = false;
    }
    private void OnDisconnected() => Debug.Log($"{nameof(PlayerController)}:  TCP 连接意外中断！");
    private void SendNetMessage(string v) => channel.SendMessage(v);
    private void OnApplicationQuit() => channel.Close();
    private async void OnConnectOrDisConnectRequired()
    {
        connectButton.interactable = false;
        var text = connectButton.GetComponentInChildren<Text>();
        if (!channel.IsRun)
        {
            text.text = "连接中...";
            var isConnectedSuccess = await channel.ConnectAsTcpClientAsync("127.0.0.1", 8888);
            text.text = isConnectedSuccess ? "已连接" : "连接服务器";
        }
        else
        {
            channel.Close();
        }
        connectButton.interactable = true;
    }
    #endregion
    #region Response 
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
        }
        else
        {
            Debug.LogWarning($"{nameof(PlayerController)}: 播放失败 ! ");
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
            currentPlayFile = video.name;
            Debug.Log($"正在请求播放 {currentPlayFile}...");
            for (int i = 0; i < 500; i++)
            {
                SendNetMessage(JsonUtility.ToJson(new Message { id = i, command = Command.Play, cmdContext = currentPlayFile }));
            }
        }
        else
        {
            Debug.Log($"请求的文件 : {dropdown.captionText.text} 不在播放列表");
        }
    }
    private void Stop()//停止播放
    {
        Debug.Log("请求停止播放视频！");
        channel.SendMessage(JsonUtility.ToJson(new Message { command = Command.Stop }));
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

