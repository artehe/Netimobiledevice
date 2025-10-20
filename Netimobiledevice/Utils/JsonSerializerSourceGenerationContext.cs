using Netimobiledevice.Remoted;
using Netimobiledevice.Remoted.Tunnel;
using System.Text.Json.Serialization;

namespace Netimobiledevice.Utils;

/// <summary>
/// Utility class for JSON (de)serialization source generation.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ClientParameters))]
[JsonSerializable(typeof(CoreDeviceTunnelEstablishRequest))]
[JsonSerializable(typeof(EstablishTunnelResponse))]
internal partial class JsonSerializerSourceGenerationContext : JsonSerializerContext { }
