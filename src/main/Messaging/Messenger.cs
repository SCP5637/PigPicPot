using System;
using System.Collections.Generic;

namespace PigPicPot.Messaging
{
    /// <summary>
    /// 消息传递器，用于在不同组件间传递消息
    /// Messenger, used to pass messages between different components
    /// </summary>
    public class Messenger : IMessenger
    {
        /// <summary>
        /// 消息处理器内部类
        /// Message handler internal class
        /// </summary>
        private class MessageHandler
        {
            /// <summary>
            /// 消息接收者（弱引用）
            /// Message recipient (weak reference)
            /// </summary>
            public WeakReference Recipient { get; }
            
            /// <summary>
            /// 处理动作
            /// Processing action
            /// </summary>
            public Delegate Action { get; }

            /// <summary>
            /// 构造函数，初始化消息处理器
            /// Constructor, initialize message handler
            /// </summary>
            /// <param name="recipient">消息接收者</param>
            /// <param name="action">处理动作</param>
            public MessageHandler(object recipient, Delegate action)
            {
                Recipient = new WeakReference(recipient);
                Action = action;
            }
        }

        /// <summary>
        /// 消息处理器字典
        /// Message handler dictionary
        /// </summary>
        private readonly Dictionary<Type, List<MessageHandler>> _handlers = new Dictionary<Type, List<MessageHandler>>();

        /// <summary>
        /// 注册消息接收者
        /// Register message recipient
        /// </summary>
        /// <typeparam name="TMessage">消息类型</typeparam>
        /// <param name="recipient">消息接收者</param>
        /// <param name="action">处理动作</param>
        public void Register<TMessage>(object recipient, Action<object, TMessage> action)
        {
            var messageType = typeof(TMessage);
            if (!_handlers.ContainsKey(messageType))
            {
                _handlers[messageType] = new List<MessageHandler>();
            }

            _handlers[messageType].Add(new MessageHandler(recipient, action));
        }

        /// <summary>
        /// 发送消息
        /// Send message
        /// </summary>
        /// <typeparam name="TMessage">消息类型</typeparam>
        /// <param name="message">消息内容</param>
        public void Send<TMessage>(TMessage message)
        {
            var messageType = typeof(TMessage);
            if (_handlers.ContainsKey(messageType))
            {
                var handlers = _handlers[messageType];
                for (int i = handlers.Count - 1; i >= 0; i--)
                {
                    var handler = handlers[i];
                    var target = handler.Recipient.Target;
                    if (target != null)
                    {
                        var action = handler.Action as Action<object, TMessage>;
                        action?.Invoke(target, message);
                    }
                    else
                    {
                        handlers.RemoveAt(i);
                    }
                }
            }
        }
    }
}
