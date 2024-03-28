using Microsoft.Extensions.Logging;
using Netimobiledevice;
using Netimobiledevice.Diagnostics;

namespace NetimobiledeviceDemo;

public class Program
{
    internal static async Task Main()
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddConsole());
        ILogger logger = factory.CreateLogger("NetimobiledeviceDemo");
        logger.LogInformation("Hello World! Logging is {Description}.", "fun");

        TaskScheduler.UnobservedTaskException += (sender, args) => {
            Console.WriteLine($"UnobservedTaskException error: {args.Exception}");
        };

        // Connect via usbmuxd
        var lockdown = MobileDevice.CreateUsingUsbmux();
        foreach (string line in new SyslogService(serviceProvider = lockdown).Watch()) {
            // Print all syslog lines as is
            Console.WriteLine(line);
        }
    }
}
