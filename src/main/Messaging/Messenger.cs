using System;
using System.Collections.Generic;

namespace PigPicPot.Messaging
{
    public class Messenger : IMessenger
    {
        private class MessageHandler
        {
            public WeakReference Recipient { get; }
            public Delegate Action { get; }

            public MessageHandler(object recipient, Delegate action)
            {
                Recipient = new WeakReference(recipient);
                Action = action;
            }
        }

        private readonly Dictionary<Type, List<MessageHandler>> _handlers = new Dictionary<Type, List<MessageHandler>>();

        public void Register<TMessage>(object recipient, Action<object, TMessage> action)
        {
            var messageType = typeof(TMessage);
            if (!_handlers.ContainsKey(messageType))
            {
                _handlers[messageType] = new List<MessageHandler>();
            }

            _handlers[messageType].Add(new MessageHandler(recipient, action));
        }

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
