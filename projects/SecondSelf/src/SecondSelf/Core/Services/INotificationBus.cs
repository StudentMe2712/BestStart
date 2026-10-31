using System;

namespace SecondSelf.Core.Services;

/// <summary>
/// Реактивная внутрипроцессная шина сообщений приложения.
/// Обеспечивает слабую связанность и мгновенную синхронизацию между виджетом и Dashboard.
/// </summary>
public interface INotificationBus
{
    /// <summary>
    /// Публикует сообщение для всех активных подписчиков.
    /// </summary>
    void Publish<TMessage>(TMessage message) where TMessage : class;

    /// <summary>
    /// Регистрирует подписчика на сообщение указанного типа.
    /// </summary>
    void Subscribe<TMessage>(object recipient, Action<TMessage> handler) where TMessage : class;

    /// <summary>
    /// Отменяет подписку получателя на сообщение указанного типа.
    /// </summary>
    void Unsubscribe<TMessage>(object recipient) where TMessage : class;

    /// <summary>
    /// Отменяет все подписки для указанного получателя.
    /// </summary>
    void UnregisterAll(object recipient);
}
