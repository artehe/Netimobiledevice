using System.Runtime.Serialization;

namespace Netimobiledevice.Plist;

/// <summary>
/// Plist node types, where the value is the binary plist tag
/// and the EnumMember value is the xml plist tag
/// </summary>
internal enum PlistType : byte
{
    /// <summary>
    /// Fill node type.
    /// </summary>
    [EnumMember(Value = "fill")]
    Fill = 0x00,
    /// <summary>
    /// Null node type.
    /// </summary>
    [EnumMember(Value = "null")]
    Null = 0x00,
    /// <summary>
    /// Boolean node type.
    /// </summary>
    [EnumMember(Value = "boolean")]
    Boolean = 0x00,
    /// <summary>
    /// Integer node type.
    /// </summary>
    [EnumMember(Value = "integer")]
    Integer = 0x01,
    /// <summary>
    /// Real node type.
    /// </summary>
    [EnumMember(Value = "real")]
    Real = 0x02,
    /// <summary>
    /// Date node type.
    /// </summary>
    [EnumMember(Value = "date")]
    Date = 0x03,
    /// <summary>
    /// Data node type.
    /// </summary>
    [EnumMember(Value = "data")]
    Data = 0x04,
    /// <summary>
    /// String node type.
    /// </summary>
    [EnumMember(Value = "string")]
    String = 0x05,
    /// <summary>
    /// UTF16 String node type.
    /// </summary>
    [EnumMember(Value = "string")]
    UString = 0x06,
    /// <summary>
    /// UID node type.
    /// </summary>
    [EnumMember(Value = "uid")]
    Uid = 0x08,
    /// <summary>
    /// Array node type.
    /// </summary>
    [EnumMember(Value = "array")]
    Array = 0x0A,
    /// <summary>
    /// Dictionary node type.
    /// </summary>
    [EnumMember(Value = "dict")]
    Dict = 0x0D,
}

public static class PlistTypeExtensions
{
    internal static string ToEnumMemberAttrValue(this Enum @enum)
    {
        EnumMemberAttribute? attr = @enum.GetType()
            .GetMember(@enum.ToString())
            .FirstOrDefault()?
            .GetCustomAttributes(false)
            .OfType<EnumMemberAttribute>()
            .FirstOrDefault();

        if (attr == null) {
            return @enum.ToString();
        }
        return attr.Value ?? string.Empty;
    }
}
