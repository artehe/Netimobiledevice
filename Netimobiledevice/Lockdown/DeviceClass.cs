using Netimobiledevice.Plist;

namespace Netimobiledevice.Lockdown
{
    internal static class DeviceClass
    {
        public const string IPHONE = "iPhone";
        public const string IPAD = "iPad";
        public const string IPOD = "iPod";
        public const string WATCH = "Watch";
        public const string APPLE_TV = "AppleTV";
        public const string UNKNOWN = "Unknown";

        public static string GetDeviceClass(PropertyNode property)
        {
            return property.AsStringNode().Value switch {
                IPHONE => IPHONE,
                IPAD => IPAD,
                IPOD => IPOD,
                WATCH => WATCH,
                APPLE_TV => APPLE_TV,
                _ => UNKNOWN,
            };
        }
    }
}
