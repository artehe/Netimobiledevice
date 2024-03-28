using System;

namespace Netimobiledevice.Lockdown
{
    public abstract class LockdownServiceProvider
    {
        public string Udid { get; private set; } = string.Empty;

        /// <summary>
        /// The internal device model identifier
        /// </summary>
        public string ProductType { get; private set; } = string.Empty;

        /// <summary>
        /// The iOS version attached to this lockdown service provider
        /// </summary>
        public abstract Version OsVersion { get; }

        public LockdownServiceProvider() { }

        public abstract ServiceConnection StartLockdownService(string name, bool useEscrowBag = false, bool useTrustedConnection = true);
    }
}
