namespace Netimobiledevice.Lockdown.Services
{
    public abstract class BaseService
    {
        /// <summary>
        /// Name of the service to use
        /// </summary>
        protected abstract string ServiceName { get; }

        protected LockdownClient Lockdown { get; }
        protected ServiceConnection Service { get; }

        /// <summary>
        /// Create a new instance of BaseService
        /// </summary>
        /// <param name="lockdown">lockdown connection</param>
        /// <param name="serviceConnection">An established service connection object. If not set then we will attempt connecting to the given serviceName</param>
        protected BaseService(LockdownClient lockdown, ServiceConnection? serviceConnection = null)
        {
            Lockdown = lockdown;
            if (serviceConnection == null) {
                Service = Lockdown.StartService(ServiceName);
            }
            else {
                Service = serviceConnection;
            }
        }

        public void Close()
        {
            Service.Close();
        }
    }
}
