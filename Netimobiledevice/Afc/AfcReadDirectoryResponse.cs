using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Netimobiledevice.Afc
{
    internal class AfcReadDirectoryResponse
    {
        public List<string> Filenames { get; }

        public AfcReadDirectoryResponse(List<string> filenames)
        {
            Filenames = filenames;
        }

        public static AfcReadDirectoryResponse Parse(byte[] data)
        {
            string decodedData = Encoding.UTF8.GetString(data);
            List<string> seperatedData = decodedData.Split('\0').ToList();
            seperatedData.RemoveAt(seperatedData.Count - 1);
            return new AfcReadDirectoryResponse(seperatedData);
        }
    }
}
