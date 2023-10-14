// Copyright (c) https://github.com/Bian-Sh
// Licensed under the MIT License.

namespace zFramework.Network.Misc
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
#if UNITY_EDITOR
    using UnityEditor;
#endif
    using UnityEngine;
    using UnityEngine.LowLevel;
    using zFramework.Network.Events;

    public static class MessageQueue
    {
        static SynchronizationContext context;
        static readonly ConcurrentQueue<MessageInfo> messages = new ConcurrentQueue<MessageInfo>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            context = SynchronizationContext.Current;
            #region 使用 PlayerLoop 在 Unity 主线程的 Update 中更新本任务同步器
            var playerloop = PlayerLoop.GetCurrentPlayerLoop();
            var loop = new PlayerLoopSystem
            {
                type = typeof(Loom),
                updateDelegate = Update
            };
            //1. 找到 Update Loop System
            int index = Array.FindIndex(playerloop.subSystemList, v => v.type == typeof(UnityEngine.PlayerLoop.Update));
            //2.  将咱们的 loop 插入到 Update loop 中
            var updateloop = playerloop.subSystemList[index];
            var temp = updateloop.subSystemList.ToList();
            temp.Add(loop);
            updateloop.subSystemList = temp.ToArray();
            playerloop.subSystemList[index] = updateloop;
            //3. 设置自定义的 Loop 到 Unity 引擎
            PlayerLoop.SetPlayerLoop(playerloop);
#if UNITY_EDITOR
            //4. 已知：编辑器停止 Play 我们自己插入的 loop 依旧会触发，进入或退出Play 模式先清空 tasks
            EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
            static void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
            {
                if (obj == PlayModeStateChange.ExitingEditMode ||
                      obj == PlayModeStateChange.ExitingPlayMode)
                {
                    //清空任务列表
                    while (messages.TryDequeue(out _)) { }
                }
            }
#endif
            #endregion
        }

#if UNITY_EDITOR
        //5. 确保编辑器下推送的事件也能被执行
        [InitializeOnLoadMethod]
        static void EditorForceUpdate()
        {
            Install();
            EditorApplication.update -= ForceEditorPlayerLoopUpdate;
            EditorApplication.update += ForceEditorPlayerLoopUpdate;
            void ForceEditorPlayerLoopUpdate()
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    // Not in Edit mode, don't interfere
                    return;
                }
                Update();
            }
        }
#endif

        /// <summary>
        ///  封送计划在主线程中执行的消息
        /// </summary>
        /// <param name="session">消息来源</param>
        /// <param name="message">要推送的消息</param>
        public static void Enqueue(Session session, string message)
        {
            var info = new MessageInfo { session = session, cmdContext = message };

            if (SynchronizationContext.Current == context)
            {
                Dispatcher(info);
            }
            else
            {
                messages.Enqueue(info);
                if (messages.Count > 50)
                {
                    Debug.LogWarning($"{nameof(MessageQueue)}:请控制消息推送速率，消息队列中未处理的数据量已超 50 个 {messages.Count} ！");
                }
            }
        }

        private static void Dispatcher(MessageInfo message)
        {
            // 这里必须使用Try catch ，避免用户逻辑异常被外部捕捉而导致消息队列异常
            try
            {
                var obj = JsonUtility.FromJson<Message>(message.cmdContext);
                EventManager.Invoke(message.session, obj);
            }
            catch (Exception e)
            {
                Debug.Log($"{nameof(Loom)}:  封送的任务执行过程中发现异常，请确认 ↓ \n{e} \n{e.StackTrace}");
            }
        }

        static void Update()
        {
            while (messages.TryDequeue(out var message))
            {
                Dispatcher(message);
            }
        }

        struct MessageInfo
        {
            public Session session;
            public string cmdContext;
        }
    }
}