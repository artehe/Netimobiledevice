using System;

namespace Netimobiledevice.NotificationProxy
{
    /// <summary>
    /// Class that contains event argument for <see cref="NotificationProxyService"/> events
    /// </summary>
    /// <remarks>
    /// Create the event args.
    /// </remarks>
    /// <param name="event">The receivable event type</param>
    /// <param name="name">The event name</param>
    public sealed class ReceivedNotificationEventArgs(string @event, string name) : EventArgs()
    {
        /// <summary>
        /// The type of the notification proxy event.
        /// </summary>
        public string Event { get; } = @event;
        /// <summary>
        /// The name of the notification proxy event.
        /// </summary>
        public string Name { get; } = name;
    }
}
