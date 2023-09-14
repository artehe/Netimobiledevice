# Netimobiledevice

Netimobiledevice is a .Net Core implementation for working with all iOS devices (iPhone, iPad, iPod) as well the plists that they use.

- [Netimobiledevice](#Netimobiledevice)
    * [Features](#Features)
    * [Installation](#Installation)
    * [Usage](#Usage)
    * [Services](#Services)
    * [License](#License)
    * [Contributing](#Contributing)
    * [Acknowledgments](#Acknowledgments)

## Features

 - Backup an iOS device in the normal iTunes way or as customised as you like. 
 - Device discovery and connection via Usbmux.
 - Interact with iOS services
 - Handle all Plists whether they are in XML or Binary format

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
using (LockdownClient lockdown = LockdownClient.CreateLockdownClient("60653a518d33eb53b3ca2322de3f44e162a42069")) {
    SpringBoardServicesService springBoard = new SpringBoardServicesService(lockdown);
    PropertyNode png = springBoard.GetIconPNGData("net.whatsapp.WhatsApp");
}
```

Create an iTunes backup:

```csharp
using (LockdownClient lockdown = LockdownClient.CreateLockdownClient("60653a518d33eb53b3ca2322de3f44e162a42069")) {
    using (DeviceBackup backupJob = new DeviceBackup(lockdown, @"C:\Users\User\Downloads")) {
        backupJob.BeforeReceivingFile += BackupJob_BeforeReceivingFile;
        backupJob.Completed += BackupJob_Completed;
        backupJob.Error += BackupJob_Error;
        backupJob.FileReceived += BackupJob_FileReceived;
        backupJob.FileReceiving += BackupJob_FileReceiving;
        backupJob.FileTransferError += BackupJob_FileTransferError;
        backupJob.PasscodeRequiredForBackup += BackupJob_PasscodeRequiredForBackup;
        backupJob.Progress += BackupJob_Progress;
        backupJob.Status += BackupJob_Status;
        backupJob.Started += BackupJob_Started;

        await backupJob.Start();
    }
}
```

Pair an iOS device asyncronously:

```csharp
using (LockdownClient lockdown = LockdownClient.CreateLockdownClient(testDevice?.Serial ?? string.Empty)) {
    Progress<PairingState> progress = new();
    progress.ProgressChanged += Progress_ProgressChanged;
    await lockdown.PairAsync(progress);
}

private void Progress_ProgressChanged(object? sender, PairingState e)
{
    Console.WriteLine($"Pair Progress Changed: {e}");
}
```

## Services

The list of all the services from lockdownd which have been implemented and the functions available for each one. Clicking on the service name will take you to it's implementation, to learn more about it.

- [com.apple.afc](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/Lockdown/Services/AfcService.cs)
  * Interact with the publicly available directories and files
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
  * Communication with the Backup service
- [com.apple.os_trace_relay](https://github.com/artehe/Netimobiledevice/blob/main/Netimobiledevice/Lockdown/Services/InstallationProxyService.cs)
  * Get pid list
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
