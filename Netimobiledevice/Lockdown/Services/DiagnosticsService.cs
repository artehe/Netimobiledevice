using System;
using System.Diagnostics;

namespace Netimobiledevice.Lockdown.Services
{
    /// <summary>
    /// Provides a service to query MobileGestalt & IORegistry keys, as well functionality to
    /// reboot, shutdown, or put the device into sleep mode.
    /// </summary>
    public sealed class DiagnosticsService : BaseService
    {
        private const string SERVICE_NAME_NEW = "com.apple.mobile.diagnostics_relay";
        private const string SERVICE_NAME_OLD = "com.apple.iosdiagnostics.relay";

        protected override string ServiceName => SERVICE_NAME_NEW;

        public DiagnosticsService(LockdownClient client) : base(client, GetDiagnosticsServiceConnection(client)) { }

        private static ServiceConnection GetDiagnosticsServiceConnection(LockdownClient client)
        {
            ServiceConnection service;
            try {
                service = client.StartService(SERVICE_NAME_NEW);
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                service = client.StartService(SERVICE_NAME_OLD);
            }

            return service;
        }
    }
}
