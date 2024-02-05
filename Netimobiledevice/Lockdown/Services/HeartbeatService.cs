using Netimobiledevice.Plist;
using System.Diagnostics;
using System.Threading;

namespace Netimobiledevice.Lockdown.Services
{
    /// <summary>
    /// Used to keep an active connection with lockdownd
    /// </summary>
    public sealed class HeartbeatService : BaseService
    {
        private readonly Timer timer;

        protected override string ServiceName => "com.apple.mobile.heartbeat";

        public HeartbeatService(LockdownClient client) : base(client)
        {
            timer = new Timer(Timer_Callback, this, Timeout.Infinite, Timeout.Infinite);
        }

        private static async void Timer_Callback(object? state)
        {
            if (state is null or not HeartbeatService) {
                return;
            }
            HeartbeatService heartbeatService = (HeartbeatService) state;

            PropertyNode? response = await heartbeatService.Service.ReceivePlistAsync(CancellationToken.None);
            // TODO log this response to debug
            Debug.WriteLine(response);

            await heartbeatService.Service.SendPlistAsync(new DictionaryNode() {
                { "Command", new StringNode("Polo") }
            }, CancellationToken.None);

        }

        public override void Dispose()
        {
            base.Dispose();
            timer.Dispose();
        }

        /// <summary>
        /// Start the heartbeat service checking in on the specified period
        /// </summary>
        /// <param name="interval">How many seconds between heatbeats</param>
        /// <param name="skipFirst">Run imedietly or wait until the next heartbeat</param>
        public void Start(int interval, bool skipFirst = false)
        {
            if (skipFirst) {
                timer.Change(interval * 1000, interval * 1000);
            }
            else {
                timer.Change(0, interval * 1000);
            }
        }

        public void Stop()
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}
