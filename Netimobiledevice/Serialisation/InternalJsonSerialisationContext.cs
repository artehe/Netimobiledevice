using Netimobiledevice.Remoted.Tunnel;
using Netimobiledevice.Remoted.Xpc;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Netimobiledevice.Serialisation;

[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(EstablishTunnelResponse))]
[JsonSerializable(typeof(XpcDictionary))]
[JsonSerializable(typeof(XpcObject))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true
)]
internal partial class InternalJsonSerialisationContext : JsonSerializerContext {
}
