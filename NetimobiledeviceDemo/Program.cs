using Netimobiledevice.Remoted;
using Netimobiledevice.Remoted.Tunnel;

namespace NetimobiledeviceDemo;

public class Program
{
    internal static async Task Main()
    {
        Tunneld tunneld = Remote.StartTunneld();

        Console.WriteLine("Yay!");
        Console.ReadLine();
    }
}
