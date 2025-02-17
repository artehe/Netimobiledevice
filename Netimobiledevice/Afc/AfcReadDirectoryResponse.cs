using System.Collections.Generic;
using System.Text;

namespace Netimobiledevice.Afc
{
    internal class AfcReadDirectoryResponse(List<string> filenames)
    {
        public List<string> Filenames { get; } = filenames;

        public static AfcReadDirectoryResponse Parse(byte[] data)
        {
            string decodedData = Encoding.UTF8.GetString(data);
            List<string> seperatedData = [.. decodedData.Split('\0')];
            seperatedData.RemoveAt(seperatedData.Count - 1);
            return new AfcReadDirectoryResponse(seperatedData);
        }
    }
}
