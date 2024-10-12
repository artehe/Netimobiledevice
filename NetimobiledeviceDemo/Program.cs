using Netimobiledevice.Remoted;

namespace NetimobiledeviceDemo;

public class Program
{
    internal static async Task Main()
    {
        await Remote.Browse();
    }
}
