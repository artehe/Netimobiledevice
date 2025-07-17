# Netimobiledevice

Netimobiledevice is a pure C# implementation for working with iOS devices (iPhone, iPad, iPod). Provides a way to interact with the services available for an iOS device via lockdownd and/or remoted. This library isn't a wrapper around the libimobiledevice library (but is inspired by it among others such as pymobiledevice3) and is designed to be used in .NET applications. It is a cross-platform library that can be used on Windows and MacOS as well as Linux (although this may require a bit more work).

- [Netimobiledevice](#Netimobiledevice)
    * [Features](#Features)
    * [Usage](#Usage)
    * [Services](#Services)
    * [License](#License)
    * [Contributing](#Contributing)
    * [Acknowledgments](#Acknowledgments)

## Features

 - Backup and Restore an iOS device in the normal iTunes way or as customised as you like. 
 - Device discovery and connection via Usbmux.
 - Interact with iOS services using Lockdownd or Remoted
 - Handle all property lists files (.plist) whether they are in XML or Binary format
 - Use remoted Apples new framework for working with iOS devices. This uses the RemoteXPC protocol and you can read more about it [here](https://github.com/doronz88/pymobiledevice3/blob/master/misc/RemoteXPC.md)
 - TCP port forwarding
 - Viewing syslog lines
 - Profile management
 - Application management
 - File system management via AFC
 - Crash reports management
 - Notification listening
 - Querying and setting SpringBoard options

# Usage

A few examples of how to use Netimobiledevice are below.

Get a list of all currently connected devices using:

```csharp
using Netimobiledevice.Usbmuxd;

List<UsbmuxdDevice> devices = Usbmux.GetDeviceList();
Console.WriteLine($"There's {devices.Count} devices connected");
foreach (UsbmuxdDevice device in devices) {
    Console.WriteLine($"Device found: {device.DeviceId} - {device.Serial}");
}
```

Listen to connection events:

```csharp
Usbmux.Subscribe(SubscriptionCallback);

private static void SubscriptionCallback(UsbmuxdDevice device, UsbmuxdConnectionEventType connectionEvent)
{
    Console.WriteLine("NewCallbackExecuted");
    Console.WriteLine($"Connection event: {connectionEvent}");
    Console.WriteLine($"Device: {device.DeviceId} - {device.Serial}");
}
```

Get the app icon displayed on the home screen as a PNG:

```csharp
using (UsbmuxLockdownClient lockdown = MobileDevice.CreateUsingUsbmux("60653a518d33eb53b3ca2322de3f44e162a42069"))
{
    SpringBoardServicesService springBoard = new SpringBoardServicesService(lockdown);
    PropertyNode png = springBoard.GetIconPNGData("net.whatsapp.WhatsApp");
}
```

Create an iTunes backup:

```csharp
using (UsbmuxLockdownClient lockdown = MobileDevice.CreateUsingUsbmux("60653a518d33eb53b3ca2322de3f44e162a42069")) {
    using (Mobilebackup2Service mb2 = new Mobilebackup2Service(lockdown)) {
        mb2.BeforeReceivingFile += BackupJob_BeforeReceivingFile;
        mb2.Completed += BackupJob_Completed;
        mb2.Error += BackupJob_Error;
        mb2.FileReceived += BackupJob_FileReceived;
        mb2.FileReceiving += BackupJob_FileReceiving;
        mb2.FileTransferError += BackupJob_FileTransferError;
        mb2.PasscodeRequiredForBackup += BackupJob_PasscodeRequiredForBackup;
        mb2.Progress += BackupJob_Progress;
        mb2.Status += BackupJob_Status;
        mb2.Started += BackupJob_Started;

        await mb2.Backup(true, true, "backups", tokenSource.Token);
    }
}
```

Pair an iOS device asyncronously:

```csharp
using (UsbmuxLockdownClient lockdown = MobileDevice.CreateUsingUsbmux(testDevice?.Serial ?? string.Empty)) {
    Progress<PairingState> progress = new();
    progress.ProgressChanged += Progress_ProgressChanged;
    await lockdown.PairAsync(progress);
}

private void Progress_ProgressChanged(object? sender, PairingState e)
{
    Console.WriteLine($"Pair Progress Changed: {e}");
}
```

Get structured logging information using the logger of your choice (provided it can interact with Microsoft.Extentions.Logging ILogger):

```csharp
using Microsoft.Extensions.Logging;

using ILoggerFactory factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddConsole());
using (LockdownClient lockdown = MobileDevice.CreateUsingUsbmux(testDevice?.Serial ?? string.Empty, logger: factory.CreateLogger("Netimobiledevice"))) {
    using (Mobilebackup2Service mb2 = new Mobilebackup2Service(lockdown)) {
        await mb2.Backup(true, true, "backups", tokenSource.Token);
    }
}
```

## License

This project is licensed under the [MIT LICENSE](https://github.com/artehe/Netimobiledevice/blob/main/LICENSE).

## Contributing

Any and all contributions are welcome. Please submit a pull request, create an issue, or start a discussion.

## Acknowledgments

This library was based on the following repositories with either some refactoring or in the case of libraries such as libusbmuxd translating from C to C#.

- **[BitConverter](https://github.com/davidrea-MS/BitConverter):** Provides a big-endian and little-endian BitConverter that convert base data types to an array of bytes, and an array of bytes to base data types, regardless of machine architecture.
- **[libimobiledevice](https://github.com/libimobiledevice/libimobiledevice):** A cross-platform protocol library to communicate with iOS devices
- **[libusbmuxd](https://github.com/libimobiledevice/libusbmuxd):** A client library for applications to handle usbmux protocol connections with iOS devices.
- **[MobileDeviceSharp](https://github.com/mveril/MobileDeviceSharp):** A C# object oriented wrapper around Libimobiledevice
- **[PList-Net](https://github.com/PList-Net/PList-Net):** .Net Library for working with Apple *.plist Files.
- **[pymobiledevice3](https://github.com/doronz88/pymobiledevice3):** A pure python3 implementation to work with iOS devices.
- **[UniversalTunTapDriver](https://github.com/HBSnail/UniversalTunTapDriver/tree/master):** A driver for TUN/TAP devices to support basic operations on both linux and windows platform. 