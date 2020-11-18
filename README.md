# Unity Async Await TCP

[中文](#1-简要说明)

# 1. Description

1. Base on async/await(TAP) syntax sugar.
2. This Demo is a Remote Video Controller base on tcp , support play, pause, and stop command, and can read the remarks of MP4 file as well.
3. Environment：Windows10 & Unity 2019.2.12f1
4. Circularbuffer and a simple packet strategy (header + content) inside for avoiding message segmentation those caused by hard-coded buffer size.
6. Disconnection notification, not the best maybe, but at least there is.
7. Friendly reminder: Task will open threads on demand. It is a good habit to always pay attention to context thread id, **API**:`` System.Threading.Thread.CurrentThread.ManagedThreadId``, to avoid exceptions caused by using Unity components in non-main threads.

# 2. Project structure

> Controller

- This is a TCP client
- The remote controller of the video player realizes the acquisition of the playlist from the server, and the playback, pause, and stop of the video

> Player

- This is the TCP server
- Video player based on UnityEngine.Video.VideoPlayer
- Realized getting file playlist and media file description from SteamingAssets 
- Media file description information is obtained using Interop.Shell32.dll
- This demo provides a test video, but please do not use it for commercial purposes

> Common

- Including communication message data model, simple event system, UnitySynchronizationContext extension
- The common components of the client and the server managed in the form of UnityPackage facilitate the synchronization of the modification of the message data model at both ends.

> UniTask
- The recommended async / await enhancement plug-in can easily return to the main thread
- Do the same thing as Task, but adapt to Unity and lighter.

# 3. Demonstrate

- you can open several controller at a time.
![](Doc/Demo.gif)


# 1. 简要说明
1. 测试基于async/await(TAP) 模式下的TCP 通信。
2. 这个Demo 是一个基于TCP通信的Remotecontroller for video player，实现了客户端控制服务端视频播放器的播放、暂停、停止，也实现了MP4文件备注信息的获取。
3. Windows10 & Unity 2019.2.12f1 
4. 加入了环形缓冲器和简单的分包策略（记录包体长度的包头+包体）避免了写死缓存尺寸带来的消息被分割的异常。
5. 加强了断线提醒，逻辑可能不是最优，但至少是有。
6. 友情提示： Task 会按需开线程，时刻关注上下文线程是一个很好的习惯,**API**:``System.Threading.Thread.CurrentThread.ManagedThreadId`` ,避免在非主线程中使用Unity 组件导致的异常。

# 2. 工程结构
> Controller
- 这是TCP 客户端
- 视频播放器的远程控制器，实现了从服务端获取播放列表、视频的播放、暂停、与停止

> Player 
- 这是TCP 服务端
- 基于UnityEngine.Video.VideoPlayer 的视频播放器
- 实现了从 SteamingAssets 获取文件播放列表和媒体文件描述
- 媒体文件描述信息使用 Interop.Shell32.dll 获取
- 本Demo提供了测试视频，但请勿用于商业用途

> Common 
- 包括通信消息数据模型，简易事件系统，UnitySynchronizationContext 扩展
- 以UnityPackage 的形式管理的客户端与服务端的公共组件，方便了消息数据模型一端修改两端同步。

> UniTask
- 推荐的 async / await 增强插件，可以很方便的返回主线程了
- 跟 Task 做一样的事情，只是更加适配Unity更加轻量化。
 

# 3. 演示
- 可以开多个控制器
![](Doc/Demo.gif)

