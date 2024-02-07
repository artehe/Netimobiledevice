using Netimobiledevice.Plist;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown.Services
{
    /// <summary>
    /// Used to keep an active connection with lockdownd
    /// </summary>
    public sealed class HeartbeatService : BaseService
    {
        private readonly BackgroundWorker heartbeatWorker;
        // Have the interval be 10 seconds as default
        private int interval = 10 * 1000;

        protected override string ServiceName => "com.apple.mobile.heartbeat";

        public HeartbeatService(LockdownClient client) : base(client)
        {
            heartbeatWorker = new BackgroundWorker {
                WorkerSupportsCancellation = true
            };
            heartbeatWorker.DoWork += HeartbeatWorker_DoWork;
        }

        private async void HeartbeatWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            Service.SetTimeout(500);
            do {
                try {
                    PropertyNode? response = await Service.ReceivePlistAsync(CancellationToken.None);
                    DictionaryNode responseDict = response?.AsDictionaryNode() ?? new DictionaryNode();

                    Debug.WriteLine(PropertyList.SaveAsString(responseDict, PlistFormat.Xml));

                    // Update the interval adding an extra second to be certain we have waited long enough
                    interval = ((int) responseDict["Interval"].AsIntegerNode().Value + 1) * 1000;

                    await Service.SendPlistAsync(new DictionaryNode() {
                        { "Command", new StringNode("Polo") }
                    }, CancellationToken.None);
                }
                catch (IOException) {
                    // If there is an IO exception we also have to assume that the service is closed so we abort the listener
                    break;
                }
                catch (ObjectDisposedException) {
                    // If the object is disposed the most likely reason is that the service is closed
                    break;
                }
                catch (TimeoutException) {
                    Debug.WriteLine("No heartbeat received, trying again");
                }
                catch (Exception ex) {
                    if (!heartbeatWorker.CancellationPending) {
                        Debug.WriteLine("======================== EXCEPTION ==============");
                        Debug.WriteLine($"Heartbeat service has an error: {ex}");
                        throw;
                    }
                }
                await Task.Delay(interval);
            } while (!heartbeatWorker.CancellationPending);
        }

        public override void Dispose()
        {
            if (heartbeatWorker.IsBusy) {
                heartbeatWorker.CancelAsync();
            }
            heartbeatWorker.Dispose();
            base.Dispose();
        }

        /// <summary>
        /// Start the heartbeat service
        /// </summary>
        public void Start()
        {
            if (!heartbeatWorker.IsBusy) {
                heartbeatWorker.RunWorkerAsync();
            }
        }

        public void Stop()
        {
            heartbeatWorker.CancelAsync();
        }
    }
}
