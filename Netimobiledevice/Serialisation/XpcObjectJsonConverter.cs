using Netimobiledevice.Remoted.Xpc;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Netimobiledevice.Serialisation;

internal class XpcObjectJsonConverter : JsonConverter<XpcObject> {
    public override void Write(Utf8JsonWriter writer, XpcObject value, JsonSerializerOptions options) {
        switch (value) {
            case XpcDictionary dict:
                writer.WriteStartObject();
                foreach (KeyValuePair<string, XpcObject> kvp in dict) {
                    writer.WritePropertyName(kvp.Key);
                    Write(writer, kvp.Value, options);
                }
                writer.WriteEndObject();
                break;

            case XpcArray arr:
                writer.WriteStartArray();
                foreach (XpcObject item in arr) {
                    Write(writer, item, options);
                }
                writer.WriteEndArray();
                break;

            case XpcString s:
                writer.WriteStringValue(s.Data);
                break;

            case XpcBool b:
                writer.WriteBooleanValue(b.Data);
                break;

            case XpcInt64 i:
                writer.WriteNumberValue(i.Data);
                break;

            case XpcUInt64 u:
                writer.WriteNumberValue(u.Data);
                break;

            case XpcDouble d:
                writer.WriteNumberValue(d.Data);
                break;

            case XpcUuid uuid:
                writer.WriteStringValue(uuid.Data.ToString());
                break;

            case XpcNull:
                writer.WriteNullValue();
                break;

            default:
                throw new NotSupportedException($"Unsupported XpcObject type: {value.GetType()}");
        }
    }

    public override XpcObject? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        // Only needed if you actually deserialise JSON back into XpcObject.
        throw new NotSupportedException("Reading XpcObject from JSON is not supported.");
    }
}
