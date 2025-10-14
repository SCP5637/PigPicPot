using System;

namespace PigPicPot.Messaging
{
    public interface IMessenger
    {
        void Register<TMessage>(object recipient, Action<object, TMessage> action);
        void Send<TMessage>(TMessage message);
    }
}
