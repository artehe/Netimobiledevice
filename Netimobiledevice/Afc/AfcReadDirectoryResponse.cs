using System.Linq;
using System.Text;

namespace Netimobiledevice.Afc;

internal class AfcReadDirectoryResponse(string[] filenames)
{
    public string[] Filenames { get; } = filenames;

    public static AfcReadDirectoryResponse Parse(byte[] data)
    {
        string decodedData = Encoding.UTF8.GetString(data);
        string[] seperatedData = decodedData.Split('\0');
        return new AfcReadDirectoryResponse([.. seperatedData.Take(seperatedData.Length - 1)]);
    }
}
