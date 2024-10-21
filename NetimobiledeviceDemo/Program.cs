using Netimobiledevice.Backup;
using Netimobiledevice.Remoted;
using Netimobiledevice.Remoted.Tunnel;

namespace NetimobiledeviceDemo;

public class Program
{
    internal static async Task Main()
    {
        Tunneld tunneld = Remote.StartTunneld();

        Console.WriteLine("Yay!");

        await Task.Delay(5000);
        RemoteServiceDiscoveryService rsd = await tunneld.GetDevice() ?? throw new Exception("No device found");

        CancellationTokenSource tokenSource = new CancellationTokenSource();
        using (Mobilebackup2Service mb2 = new Mobilebackup2Service(rsd.Lockdown)) {
            await mb2.Backup(true, "backups", tokenSource.Token);
        }

    }
}
