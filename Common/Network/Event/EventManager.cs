using System;
using System.Collections.Generic;
namespace zFramework.Network.Events
{
    /// <summary>
    /// 简易事件总线, 设计上仅用于当前的工程
    /// 由于该事件系统不区分订阅的消息究竟来自于客户端还是服务器，所以在同一应用中不支持客户端和服务器同时使用
    ///  如果你想在客户端和服务器同时使用，那么请完善的事件系统， 或者对客户端和服务器的消息处理进行区分即可
    /// </summary>
    public static class EventManager
    {
        private static Dictionary<Command, List<Action<Session, Message>>> events = new Dictionary<Command, List<Action<Session, Message>>>();
        /// <summary>
        /// 监听指定事件
        /// </summary>
        /// <param name="command"></param>
        /// <param name="action"></param>
        public static void AddListener(Command command, Action<Session, Message> action)
        {
            if (!events.TryGetValue(command, out var acts))
            {
                acts = new List<Action<Session, Message>>();
                events.Add(command, acts);
            }
            if (!acts.Contains(action))
            {
                acts.Add(action);
            }
        }

        public static void RemoveListener(Command command, Action<Session, Message> action) 
        {
            if (events.TryGetValue(command, out var acts))
            {
                acts.Remove(action);
            }
        }

        /// <summary>
        /// 执行指定事件
        /// </summary>
        /// <param name="message"></param>
        public static void Invoke(Session session, Message message)
        {
            if (events.TryGetValue(message.command, out var acts))
            {
                foreach (var item in acts)
                {
                    item.Invoke(session, message);
                }
            }
        }
    }
}