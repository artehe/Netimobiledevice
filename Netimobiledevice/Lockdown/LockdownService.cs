using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace Netimobiledevice.Lockdown
{
    public abstract class LockdownService : IDisposable
    {
        protected LockdownServiceProvider Lockdown { get; }
        /// <summary>
        /// The internal logger
        /// </summary>
        protected ILogger Logger { get; }
        protected ServiceConnection Service { get; }
        protected string ServiceName { get; }

        /// <summary>
        /// Create a new LockdownService instance
        /// </summary>
        /// <param name="lockdown">Service provider</param>
        /// <param name="serviceName">The service name to attempt to connect to</param>
        /// <param name="service">An established service connection, if none we will attempt connecting to the provided serviceName</param>
        /// <param name="useEscrowBag">Use the available lockdown escrow back to start the service</param>
        public LockdownService(LockdownServiceProvider lockdown, string serviceName, ServiceConnection? service = null, bool useEscrowBag = false, ILogger? logger = null)
        {
            Lockdown = lockdown;
            Logger = logger ?? NullLogger.Instance;
            ServiceName = serviceName;
            Service = service ?? lockdown.StartLockdownService(ServiceName, useEscrowBag).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Create a new LockdownService instance
        /// </summary>
        /// <param name="lockdown">Service provider</param>
        /// <param name="lockdownServiceName">The service name to attempt to connect to if we have a Lockdown connection</param>
        /// <param name="rsdServiceName">The service name to attempt to connect to if we have an RSD connection</param>
        /// <param name="service">An established service connection, if none we will attempt connecting to the provided serviceName</param>
        /// <param name="useEscrowBag">Use the available lockdown escrow back to start the service</param>
        public LockdownService(LockdownServiceProvider lockdown, string lockdownServiceName, string rsdServiceName, ServiceConnection? service = null, bool useEscrowBag = false, ILogger? logger = null)
        {
            if (lockdown is LockdownClient) {
                ServiceName = lockdownServiceName;
            }
            else {
                ServiceName = rsdServiceName;
            }

            Lockdown = lockdown;
            Logger = logger ?? NullLogger.Instance;
            Service = service ?? lockdown.StartLockdownService(ServiceName, useEscrowBag).GetAwaiter().GetResult();
        }

        public void Close()
        {
            Service.Close();
        }

        public virtual void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }
    }
}
