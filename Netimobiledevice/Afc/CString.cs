using System;
using System.Text;

namespace Netimobiledevice.Afc;

internal sealed class CString {
    private byte[] _bytes;
    private readonly Encoding _encoding;
    private string _sourceValue;

    public CString(string str, Encoding encoding) {
        _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        _sourceValue = str ?? throw new ArgumentNullException(nameof(str));
        _bytes = _encoding.GetBytes(Value);
    }

    public int Length => GetBytes().Length;
    public string SourceValue {
        get => _sourceValue;
        set {
            _sourceValue = value;
            _bytes = _encoding.GetBytes(Value);
        }
    }
    public string Value => $"{SourceValue}\0";

    public byte[] GetBytes() => _bytes;
}

internal static class CStringExtentions {
    public static CString AsCString(this string str, Encoding encoding) {
        return new CString(str, encoding);
    }
}
