using Microsoft.Extensions.Logging;
using Netimobiledevice.Plist;
using System;
using System.Threading.Tasks;

namespace Netimobiledevice.Lockdown;

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

    /// <summary>
    /// Gets the value for the specified domain and key.
    /// </summary>
    /// <param name="domain">The domain to obtain the value from.</param>
    /// <param name="key">The key of the property to obtain.</param>
    /// <returns>The value obtained.</returns>
    public abstract PropertyNode? GetValue(string? domain, string? key);

    /// <summary>
    /// Gets the value for the specified key in the root domain.
    /// </summary>
    /// <param name="key">The key of the property to obtain.</param>
    /// <returns>The string value obtained.</returns>
    public PropertyNode? GetValue(string? key)
    {
        return GetValue(null, key);
    }

    /// <summary>
    /// Get every value for the specified in the root domain.
    /// </summary>
    /// <returns>The values obtained.</returns>
    public PropertyNode? GetValue()
    {
        return GetValue(null, null);
    }

    /// <summary>
    /// Gets the value for the specified domain and key.
    /// </summary>
    /// <param name="domain">The domain to obtain the value from.</param>
    /// <param name="key">The key of the property to obtain.</param>
    /// <returns>The value obtained.</returns>
    public abstract Task<PropertyNode?> GetValueAsync(string? domain, string? key);

    /// <summary>
    /// Gets the value for the specified key in the root domain.
    /// </summary>
    /// <param name="key">The key of the property to obtain.</param>
    /// <returns>The string value obtained.</returns>
    public async Task<PropertyNode?> GetValueAsync(string? key)
    {
        return await GetValueAsync(null, key).ConfigureAwait(false);
    }

    /// <summary>
    /// Get every value for the specified in the root domain.
    /// </summary>
    /// <returns>The values obtained.</returns>
    public async Task<PropertyNode?> GetValueAsync()
    {
        return await GetValueAsync(null, null).ConfigureAwait(false);
    }

    public abstract ServiceConnection StartLockdownService(string name, bool useEscrowBag = false, bool useTrustedConnection = true);

    public abstract Task<ServiceConnection> StartLockdownServiceAsync(string name, bool useEscrowBag = false, bool useTrustedConnection = true);
}
