using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace Netimobiledevice.Lockdown
{
    /// <summary>
    /// Create a new LockdownService instance
    /// </summary>
    /// <param name="lockdown">Service provider</param>
    /// <param name="serviceName">The service name to attempt to connect to</param>
    /// <param name="service">An established service connection, if none we will attempt connecting to the provided serviceName</param>
    /// <param name="useEscrowBag">Use the available lockdown escrow back to start the service</param>
    public abstract class LockdownService(LockdownServiceProvider lockdown, string serviceName, ServiceConnection? service = null, bool useEscrowBag = false, ILogger? logger = null) : IDisposable
    {
        protected LockdownServiceProvider Lockdown { get; } = lockdown;
        /// <summary>
        /// The internal logger
        /// </summary>
        protected ILogger Logger { get; } = logger ?? NullLogger.Instance;
        protected ServiceConnection Service { get; } = service ?? lockdown.StartLockdownService(serviceName, useEscrowBag);
        protected string ServiceName { get; } = serviceName;

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
