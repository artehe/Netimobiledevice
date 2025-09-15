using System;

namespace Netimobiledevice.NotificationProxy;

/// <summary>
/// Class that contains event argument for <see cref="NotificationProxyService"/> events
/// </summary>
/// <remarks>
/// Create the event args.
/// </remarks>
/// <param name="event">The receivable event type</param>
/// <param name="udid">The UDID of the device which raised the notification</param>
public sealed class ReceivedNotificationEventArgs(string @event, string udid) : EventArgs()
{
    /// <summary>
    /// The name of the notification proxy event which was sent.
    /// </summary>
    public string Event { get; } = @event;
    /// <summary>
    /// The UDID of the device which raised the notification.
    /// </summary>
    public string UDID { get; } = udid;
}
