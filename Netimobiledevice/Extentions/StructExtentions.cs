using System;
using System.Runtime.InteropServices;

namespace Netimobiledevice.Extentions
{
    internal static class StructExtentions
    {
        public static byte[] GetBytes<T>(this T obj) where T : struct
        {
            int size = Marshal.SizeOf(obj);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            return arr;
        }

        public static T FromBytes<T>(byte[] bytearray) where T : struct
        {
            T obj = new T();
            int size = Marshal.SizeOf(obj);

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(bytearray, 0, ptr, size);
            Marshal.PtrToStructure(ptr, obj);
            Marshal.FreeHGlobal(ptr);

            return obj;
        }
    }
}
