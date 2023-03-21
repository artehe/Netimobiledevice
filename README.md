# Netimobiledevice

Pure .Net Core implementation for working with iOS devices (iPhone, iPad, iPod).

## Installation

The fastest way of getting started is to install the NuGet package.

```csharp
dotnet add package Netimobiledevice
```

## Acknowledgments

This library was based on the following repositories with either some refactoring or in the case of libraries such as libusbmuxd translating from C to C#.

- **[BitConverter](https://github.com/davidrea-MS/BitConverter):** Provides a big-endian and little-endian BitConverter that convert base data types to an array of bytes, and an array of bytes to base data types, regardless of machine architecture.
- **[libusbmuxd](https://github.com/libimobiledevice/libusbmuxd):** A client library for applications to handle usbmux protocol connections with iOS devices.
- **[PList-Net](https://github.com/PList-Net/PList-Net):** .Net Library for working with Apple *.plist Files.
- **[pymobiledevice3](https://github.com/doronz88/pymobiledevice3):** A pure python3 implementation to work with iOS devices.
