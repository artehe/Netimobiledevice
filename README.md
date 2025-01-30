# Netimobiledevice

Netimobiledevice is a pure C# implementation for working with iOS devices (iPhone, iPad, iPod). Implements quite a lot of the services available from lockdownd and remoted. This library isn't a wrapper around the libimobiledevice library (but is inspired by it among others such as pymobiledevice3) and is designed to be used in .NET applications. It is a cross-platform library that can be used on Windows, Linux and MacOS.

- [Netimobiledevice](#Netimobiledevice)
    * [Features](#Features)
    * [Installation](#Installation)
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
 - File system management
 - Crash reports management
 - Notification listening
 - Querying and setting SpringBoard options

## Installation

To install Netimobiledevice, you can use the following command in the Package Manager Console:

```powershell
Install-Package Netimobiledevice
```

Alternatively, you can use the .NET CLI:

```csharp
dotnet add package Netimobiledevice
```

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

## Services

The list of all the services from lockdownd which have been implemented and the functions available for each one. Clicking on the service name will take you to it's implementation, to learn more about it.

- [com.apple.afc](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/Afc/AfcService.cs)
  * Interact with the publicly available directories and files
- [com.apple.mobile.heartbeat](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/Lockdown/Services/HeartbeatService.cs)
  * A regular ping to used to keep an active connection with lockdownd
- [com.apple.misagent](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/Misagent/MisagentService.cs)
  * Management for provisioning profiles 
- [com.apple.mobile.diagnostics_relay](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/Lockdown/Services/DiagnosticsService.cs)
  * Query MobileGestalt & IORegistry keys.
  * Reboot, shutdown or put the device in sleep mode.
- [com.apple.mobile.installation_proxy](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/Lockdown/Services/OsTraceService.cs)
  * Browse installed applications
  * Manage applications (install/uninstall/update)
- [com.apple.mobile.notification_proxy](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/Lockdown/Services/NotificationProxyService.cs) & [com.apple.mobile.insecure_notification_proxy](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/Lockdown/Services/NotificationProxyService.cs)
  * Send and receive notifications from the device for example informing a backup sync is about to occur.
- [com.apple.mobilebackup2](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/Backup/Mobilebackup2Service.cs)
  * Backup Creation
  * Restore a backup to the iOS device
  * Communication with the Backup service
- [com.apple.os_trace_relay](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/Lockdown/Services/InstallationProxyService.cs)
  * Get pid list
  * More structural syslog lines.
- [com.apple.springboardservices](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/SpringBoardServices/SpringBoardServicesService.cs)
  * Get icons from the installed apps on the device.
- [com.apple.syslog_relay](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/Lockdown/Services/SyslogService.cs)
  * Stream raw syslog lines from the device.

## License

This project is licensed under the [MIT LICENSE](https://github.com/artehe/Netimobiledevice/blob/main/LICENSE).

## Contributing

Contributions are welcome. Please submit a pull request or create an issue to discuss your proposed changes.

## Acknowledgments

This library was based on the following repositories with either some refactoring or in the case of libraries such as libusbmuxd translating from C to C#.

- **[BitConverter](https://github.com/davidrea-MS/BitConverter):** Provides a big-endian and little-endian BitConverter that convert base data types to an array of bytes, and an array of bytes to base data types, regardless of machine architecture.
- **[libimobiledevice](https://github.com/libimobiledevice/libimobiledevice):** A cross-platform protocol library to communicate with iOS devices
- **[libusbmuxd](https://github.com/libimobiledevice/libusbmuxd):** A client library for applications to handle usbmux protocol connections with iOS devices.
- **[MobileDeviceSharp](https://github.com/mveril/MobileDeviceSharp):** A C# object oriented wrapper around Libimobiledevice
- **[PList-Net](https://github.com/PList-Net/PList-Net):** .Net Library for working with Apple *.plist Files.
- **[pymobiledevice3](https://github.com/doronz88/pymobiledevice3):** A pure python3 implementation to work with iOS devices.
- **[UniversalTunTapDriver](https://github.com/HBSnail/UniversalTunTapDriver/tree/master):** A driver for TUN/TAP devices to support basic operations on both linux and windows platform. 