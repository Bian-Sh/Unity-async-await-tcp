using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;
using System.Linq;
using Shell32;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using zFramework.Network.Events;
using zFramework.Network;

public class PlayerServer : MonoBehaviour
{
    public VideoPlayer player;
    private string currentPlayFile = string.Empty;
    private PlayList playList;
    TCPServer server;
    private void Awake()
    {
        RefreshFile();
    }

    void Start()
    {
        server = new TCPServer("127.0.0.1", 8888);
        player.loopPointReached += Player_loopPointReached;
        EventManager.AddListener(Command.Play, OnPlayRequest);
        EventManager.AddListener(Command.Pause, OnPauseRequest);
        EventManager.AddListener(Command.Stop, OnStopRequest);
        EventManager.AddListener(Command.PlayList, OnPlayListResponse);

        player.targetTexture.Release();
        server.OnClientConnected.AddListener(OnClientConnected);
        server.OnClientDisconnected.AddListener(session =>
        {
            Debug.Log($"客户端 {session.IPEndPoint} 断开连接！");
        });
        _ = Task.Run(server.ListenAsync); //此处务必使用 Task 执行，否则这个Awake 方法会回不到主线程，如果下面还有逻辑也不会执行咯
    }


    private void OnClientConnected(Session session)
    {
        string pl = JsonUtility.ToJson(playList);
        Message message = new Message { command = Command.PlayList, cmdContext = pl };
        server.Send(session, Encoding.UTF8.GetBytes(JsonUtility.ToJson(message)));
    }

    private void OnPlayListResponse(Session session, Message message)
    {
        Debug.Log("[播放器] 向控制器发送播放列表");
        string pl = JsonUtility.ToJson(playList);
        message = new Message { command = Command.PlayList, cmdContext = pl };

        var datas = SerializeHelper.Serialize(message);
        server.BroadcastOthers(session, datas);
    }

    private void Player_loopPointReached(VideoPlayer source)
    {
        Debug.Log("播放完毕！");
        var datas = SerializeHelper.Serialize(new Message { command = Command.Stop });
        server.Broadcast(datas);
    }

    private void OnStopRequest(Session session, Message message)
    {
        Debug.Log($"播放器收到 {session.IPEndPoint} 停止播放指令！");
        currentPlayFile = string.Empty;
        player.Stop();
        player.targetTexture.Release();
        //向其他控制器同步转发视频被停止的状态
        var datas = SerializeHelper.Serialize(message);
        server.BroadcastOthers(session, datas);
    }

    private void OnPauseRequest(Session session, Message message)
    {
        Debug.Log($"播放器收到 {session.IPEndPoint} 暂停播放指令！");
        if (!player.isPlaying) return;
        player.Pause();

        // 向其他控制器同步视频被暂停的状态
        var datas = SerializeHelper.Serialize(message);
        server.BroadcastOthers(session, datas);
    }

    private void OnPlayRequest(Session session, Message message)
    {
        Debug.Log($"播放器 {session.IPEndPoint} 请求播放 {message.cmdContext}！");
        var item = playList.items.Find(v => v.name == message.cmdContext);
        if (currentPlayFile == message.cmdContext)
        {
            // 从暂停状态恢复播放
            if (!player.isPlaying)
            {
                player.Play();
            }
        }
        else
        {
            if (null != item)
            {
                currentPlayFile = message.cmdContext;
                string url = item.path;
                player.url = url;
                player.Play();
            }
            else
            {
                var msg = $"找不到指定的文件 {message.cmdContext}";
                Debug.LogError(msg);
                //todo : 向请求的控制器发送错误消息
                // 这里使用 RPC 最好了，直接返回播放结果，成功、失败以及错误
            }
        }

        //向其他控制器同步视频被播放的状态
        var datas = SerializeHelper.Serialize(message);
        server.BroadcastOthers(session, datas);
    }

    private void RefreshFile()//刷新文件列表
    {
        DirectoryInfo direction = new DirectoryInfo(Application.streamingAssetsPath);
        FileInfo[] files = direction.GetFiles("*.mp4", SearchOption.AllDirectories);
        if (null != files && files.Length > 0)
        {
            Func<FileInfo, VideoItem> CreateItem = v =>
             {
                 VideoItem itm = new VideoItem();
                 itm.name = v.Name;
                 itm.path = v.FullName;
                 ShellClass sh = new ShellClass();
                 Folder dir = sh.NameSpace(v.DirectoryName);
                 FolderItem item = dir.ParseName(v.Name);
                 itm.description = dir.GetDetailsOf(item, 24);
                 return itm;
             };
            playList = new PlayList { items = files.Select(CreateItem).ToList() };
        }
        foreach (var item in playList.items)
        {
            Debug.Log(item.name + " : " + item.description);
        }
    }
    private void OnDestroy()
    {
        server?.OnClientConnected.RemoveListener(OnClientConnected);
        server?.Stop();
    }
}
