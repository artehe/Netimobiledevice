using System;

namespace Netimobiledevice.NotificationProxy
{
    /// <summary>
    /// Class that contains event argument for <see cref="NotificationProxyService"/> events
    /// </summary>
    public sealed class ReceivedNotificationEventArgs : EventArgs
    {
        /// <summary>
        /// The type of the notification proxy event.
        /// </summary>
        public ReceivableNotification Event { get; }
        /// <summary>
        /// The name of the notification proxy event.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Create the event args.
        /// </summary>
        /// <param name="event">The receivable event type</param>
        /// <param name="name">The event name</param>
        public ReceivedNotificationEventArgs(ReceivableNotification @event, string name) : base()
        {
            Event = @event;
            Name = name;
        }
    }
}
