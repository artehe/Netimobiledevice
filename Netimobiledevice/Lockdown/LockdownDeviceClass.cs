using Netimobiledevice.Plist;

namespace Netimobiledevice.Lockdown;

internal static class LockdownDeviceClass
{
    public const string IPhone = "iPhone";
    public const string IPad = "iPad";
    public const string IPod = "iPod";
    public const string Watch = "Watch";
    public const string AppleTv = "AppleTV";
    public const string Unknown = "Unknown";

    public static string GetDeviceClass(PropertyNode property)
    {
        return property.AsStringNode().Value switch {
            IPhone => IPhone,
            IPad => IPad,
            IPod => IPod,
            Watch => Watch,
            AppleTv => AppleTv,
            _ => Unknown,
        };
    }
}
