using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netimobiledevice.HelperFiles
{
    internal static class ModelIdentifier
    {
        internal static string GetDeviceModelName(string identifier)
        {
            Dictionary<string, string> modelMapping = new Dictionary<string, string>
            {
                //iPhone mapping begins
                {"iPhone1,1", "iPhone"},
                {"iPhone1,2", "iPhone 3G"},
                {"iPhone2,1", "iPhone 3GS"},
                {"iPhone3,1", "iPhone 4 (GSM)"},
                {"iPhone3,2", "iPhone 4 (GSM Rev A)"},
                {"iPhone3,3", "iPhone 4 (CDMA)"},
                {"iPhone4,1", "iPhone 4S"},
                {"iPhone5,1", "iPhone 5 (GSM)"},
                {"iPhone5,2", "iPhone 5 (Global)"},
                {"iPhone5,3", "iPhone 5c (GSM)"},
                {"iPhone5,4", "iPhone 5c (Global)"},
                {"iPhone6,1", "iPhone 5s (GSM)"},
                {"iPhone6,2", "iPhone 5s (Global)"},
                {"iPhone7,1", "iPhone 6 Plus"},
                {"iPhone7,2", "iPhone 6"},
                {"iPhone8,1", "iPhone 6s"},
                {"iPhone8,2", "iPhone 6s Plus"},
                {"iPhone8,4", "iPhone SE (1st generation)"},
                {"iPhone9,1", "iPhone 7"},
                {"iPhone9,2", "iPhone 7 Plus"},
                {"iPhone9,3", "iPhone 7"},
                {"iPhone9,4", "iPhone 7 Plus"},
                {"iPhone10,1", "iPhone 8"},
                {"iPhone10,2", "iPhone 8 Plus"},
                {"iPhone10,3", "iPhone X"},
                {"iPhone10,4", "iPhone 8"},
                {"iPhone10,5", "iPhone 8 Plus"},
                {"iPhone10,6", "iPhone X"},
                {"iPhone11,2", "iPhone XS"},
                {"iPhone11,4", "iPhone XS Max"},
                {"iPhone11,6", "iPhone XS Max (China)"},
                {"iPhone11,8", "iPhone XR"},
                {"iPhone12,1", "iPhone 11"},
                {"iPhone12,3", "iPhone 11 Pro"},
                {"iPhone12,5", "iPhone 11 Pro Max"},
                {"iPhone12,8", "iPhone SE (2nd generation)"},
                {"iPhone13,1", "iPhone 12 mini"},
                {"iPhone13,2", "iPhone 12"},
                {"iPhone13,3", "iPhone 12 Pro"},
                {"iPhone13,4", "iPhone 12 Pro Max"},
                {"iPhone14,4", "iPhone 13 mini"},
                {"iPhone14,5", "iPhone 13"},
                {"iPhone14,2", "iPhone 13 Pro"},
                {"iPhone14,3", "iPhone 13 Pro Max"},
                {"iPhone14,6","iPhone SE (3rd generation)"},
                {"iPhone15,3","iPhone 14 Pro Max"},
                {"iPhone15,2","iPhone 14 Pro"},
                {"iPhone14,8","iPhone 14 Plus"},
                {"iPhone14,7","iPhone 14"},
                {"iPhone16,2","iPhone 15 Pro Max"},
                {"iPhone16,1","iPhone 15 Pro"},
                {"iPhone15,5","iPhone 15 Plus"},
                {"iPhone15,4","iPhone 15"},
       
                //Ipad version begins
                {"iPad1,1", "iPad"},
                {"iPad2,1", "iPad 2 (Wi-Fi)"},
                {"iPad2,2", "iPad 2 (GSM)"},
                {"iPad2,3", "iPad 2 (CDMA)"},
                {"iPad2,4", "iPad 2 (Wi-Fi, Rev A)"},
                {"iPad3,1", "iPad (3rd generation, Wi-Fi)"},
                {"iPad3,2", "iPad (3rd generation, GSM)"},
                {"iPad3,3", "iPad (3rd generation, CDMA)"},
                {"iPad3,4", "iPad (4th generation, Wi-Fi)"},
                {"iPad3,5", "iPad (4th generation, GSM)"},
                {"iPad3,6", "iPad (4th generation, CDMA)"},
                {"iPad6,11", "iPad (5th generation, Wi-Fi)"},
                {"iPad6,12", "iPad (5th generation, Cellular)"},
                {"iPad7,5", "iPad (6th generation, Wi-Fi)"},
                {"iPad7,6", "iPad (6th generation, Cellular)"},
                {"iPad7,11", "iPad (7th generation, Wi-Fi)"},
                {"iPad7,12", "iPad (7th generation, Cellular)"},
                {"iPad11,6", "iPad (8th generation, Wi-Fi)"},
                {"iPad11,7", "iPad (8th generation, Cellular)"},
                {"iPad5,3","iPad Air 2"},
                {"iPad5,4","iPad Air 2"},
                {"iPad4,7","iPad mini 3"}
            };
            
            if (modelMapping.TryGetValue(identifier, out var modelName)) {
                return modelName;
            }

            //if none of the above one is returned, then new iPhone version has been released and it's model has to be added to the list.
            return identifier;
        }
    }
}
