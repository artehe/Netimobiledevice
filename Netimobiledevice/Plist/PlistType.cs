using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Netimobiledevice.Plist;

/// <summary>
/// Plist node types, where the value is the binary plist tag
/// and the EnumMember value is the xml plist tag
/// </summary>
internal enum PlistType : byte
{
    /// <summary>
    /// Null node type.
    /// </summary>
    [EnumMember(Value = "null")]
    Null = 0x00,
    /// <summary>
    /// Boolean (false) node type.
    /// </summary>
    [EnumMember(Value = "boolean")]
    Bool = 0x08,
    /// <summary>
    /// Boolean (true) node type.
    /// </summary>
    [EnumMember(Value = "boolean")]
    Boolean = 0x09,
    /// <summary>
    /// Fill node type.
    /// </summary>
    [EnumMember(Value = "fill")]
    Fill = 0x0F,
    /// <summary>
    /// Integer node type.
    /// </summary>
    [EnumMember(Value = "integer")]
    Integer = 0x10,
    /// <summary>
    /// Real node type.
    /// </summary>
    [EnumMember(Value = "real")]
    Real = 0x20,
    /// <summary>
    /// Date node type.
    /// </summary>
    [EnumMember(Value = "date")]
    Date = 0x30,
    /// <summary>
    /// Data node type.
    /// </summary>
    [EnumMember(Value = "data")]
    Data = 0x40,
    /// <summary>
    /// String node type.
    /// </summary>
    [EnumMember(Value = "string")]
    String = 0x50,
    /// <summary>
    /// UTF16 String node type.
    /// </summary>
    [EnumMember(Value = "ustring")]
    UString = 0x60,
    /// <summary>
    /// UID node type.
    /// </summary>
    [EnumMember(Value = "uid")]
    Uid = 0x80,
    /// <summary>
    /// Array node type.
    /// </summary>
    [EnumMember(Value = "array")]
    Array = 0xA0,
    /// <summary>
    /// Dictionary node type.
    /// </summary>
    [EnumMember(Value = "dict")]
    Dict = 0xD0,
}

public static class PlistTypeExtensions
{
    internal static string ToEnumMemberAttrValue<TEnum>(this TEnum value) where TEnum : struct, Enum {
        string? name = Enum.GetName(value);
        if (name is null) {
            // Unnamed or invalid enum value
            return value.ToString(); 
        }

        FieldInfo? field = typeof(TEnum).GetField(name);
        EnumMemberAttribute? attr = field?.GetCustomAttribute<EnumMemberAttribute>();

        return attr?.Value ?? name;
    }
}
