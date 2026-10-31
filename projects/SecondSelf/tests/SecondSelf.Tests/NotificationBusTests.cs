using System.Threading;
using SecondSelf.Core.Messages;
using SecondSelf.Core.Services;
using Xunit;

namespace SecondSelf.Tests;

public class NotificationBusTests
{
    [Fact]
    public void Publish_TaskCompletedMessage_DeliveredToSubscriber()
    {
        var bus = new NotificationBus();
        TaskCompletedMessage? receivedMessage = null;

        bus.Subscribe<TaskCompletedMessage>(this, msg => receivedMessage = msg);

        var msgToSend = new TaskCompletedMessage(
            TaskId: "task-1",
            Title: "Test Task",
            CompletedAtIso: "2026-10-31T10:00:00Z",
            LogicalDate: "2026-10-31",
            TaskSource: "Today");

        bus.Publish(msgToSend);

        Assert.NotNull(receivedMessage);
        Assert.Equal("task-1", receivedMessage.TaskId);
        Assert.Equal("Test Task", receivedMessage.Title);

        bus.UnregisterAll(this);
    }

    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        var bus = new NotificationBus();
        int callCount = 0;

        bus.Subscribe<DayChangedMessage>(this, _ => callCount++);

        bus.Publish(new DayChangedMessage("2026-10-30", "2026-10-31"));
        Assert.Equal(1, callCount);

        bus.Unsubscribe<DayChangedMessage>(this);
        bus.Publish(new DayChangedMessage("2026-10-31", "2026-11-01"));

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Publish_DailyRitualCountedMessage_DeliveredProperly()
    {
        var bus = new NotificationBus();
        DailyRitualCountedMessage? received = null;

        bus.Subscribe<DailyRitualCountedMessage>(this, msg => received = msg);

        var msg = new DailyRitualCountedMessage("2026-10-31", "video-uuid", "2026-10-31T12:00:00Z");
        bus.Publish(msg);

        Assert.NotNull(received);
        Assert.Equal("2026-10-31", received.LogicalDate);
        Assert.Equal("video-uuid", received.VideoId);

        bus.UnregisterAll(this);
    }
}
