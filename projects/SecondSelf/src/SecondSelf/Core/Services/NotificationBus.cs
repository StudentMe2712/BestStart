using System;
using CommunityToolkit.Mvvm.Messaging;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реализация шины сообщений на базе WeakReferenceMessenger из CommunityToolkit.Mvvm.
/// Предотвращает утечки памяти за счёт использования слабых ссылок (WeakReference).
/// </summary>
public sealed class NotificationBus : INotificationBus
{
    private readonly IMessenger _messenger;

    public NotificationBus(IMessenger? messenger = null)
    {
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public void Publish<TMessage>(TMessage message) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        _messenger.Send(message);
    }

    public void Subscribe<TMessage>(object recipient, Action<TMessage> handler) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(handler);
        _messenger.Register<TMessage>(recipient, (r, m) => handler(m));
    }

    public void Unsubscribe<TMessage>(object recipient) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(recipient);
        _messenger.Unregister<TMessage>(recipient);
    }

    public void UnregisterAll(object recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        _messenger.UnregisterAll(recipient);
    }
}
