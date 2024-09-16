using Microsoft.Extensions.Logging;
using System;

namespace Netimobiledevice.Lockdown
{
    public abstract class LockdownServiceProvider
    {
        public abstract ILogger Logger { get; }

        /// <summary>
        /// The iOS version attached to this lockdown service provider
        /// </summary>
        public abstract Version OsVersion { get; }

        /// <summary>
        /// The internal device model identifier
        /// </summary>
        public string ProductType { get; protected set; } = string.Empty;

        public string Udid { get; protected set; } = string.Empty;

        public LockdownServiceProvider() { }

        public abstract ServiceConnection StartLockdownService(string name, bool useEscrowBag = false, bool useTrustedConnection = true);
    }
}
