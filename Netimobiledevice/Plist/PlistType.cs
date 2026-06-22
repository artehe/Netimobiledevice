namespace Netimobiledevice.Plist;

/// <summary>
/// Plist node types, where the value is the binary plist tag
/// and the EnumMember value is the xml plist tag
/// </summary>
internal enum PlistType : byte {
    /// <summary>
    /// Null node type.
    /// </summary>
    Null = 0x00,
    /// <summary>
    /// Boolean (false) node type.
    /// </summary>
    Bool = 0x08,
    /// <summary>
    /// Boolean (true) node type.
    /// </summary>
    Boolean = 0x09,
    /// <summary>
    /// Fill node type.
    /// </summary>
    Fill = 0x0F,
    /// <summary>
    /// Integer node type.
    /// </summary>
    Integer = 0x10,
    /// <summary>
    /// Real node type.
    /// </summary>
    Real = 0x20,
    /// <summary>
    /// Date node type.
    /// </summary>
    Date = 0x30,
    /// <summary>
    /// Data node type.
    /// </summary>
    Data = 0x40,
    /// <summary>
    /// String node type.
    /// </summary>
    String = 0x50,
    /// <summary>
    /// UTF16 String node type.
    /// </summary>
    UString = 0x60,
    /// <summary>
    /// UID node type.
    /// </summary>
    Uid = 0x80,
    /// <summary>
    /// Array node type.
    /// </summary>
    Array = 0xA0,
    /// <summary>
    /// Dictionary node type.
    /// </summary>
    Dict = 0xD0,
}

public static class PlistTypeExtensions {
    /// <summary>
    /// Returns the xml plist tag for a given <see cref="PlistType"/>.
    /// </summary>
    internal static string GetXmlTag(this PlistType value) => value switch {
        PlistType.Null => "null",
        PlistType.Bool or PlistType.Boolean => "boolean",
        PlistType.Fill => "fill",
        PlistType.Integer => "integer",
        PlistType.Real => "real",
        PlistType.Date => "date",
        PlistType.Data => "data",
        PlistType.String => "string",
        PlistType.UString => "ustring",
        PlistType.Uid => "uid",
        PlistType.Array => "array",
        PlistType.Dict => "dict",
        _ => value.ToString()
    };
}
