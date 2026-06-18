using Netimobiledevice.Remoted.Tunnel;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Netimobiledevice.Serialisation;

[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(EstablishTunnelResponse))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true
)]
internal partial class InternalJsonSerialisationContext : JsonSerializerContext {
}
