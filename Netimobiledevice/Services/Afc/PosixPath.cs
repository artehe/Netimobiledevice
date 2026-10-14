namespace Netimobiledevice.Services.Afc;

public static class PosixPath {
    public const char Separator = '/';

    public static string BaseName(string path) => path[(path.LastIndexOf(Separator) + 1)..];

    public static string DirName(string path) {
        string head = path[..(path.LastIndexOf(Separator) + 1)];
        if (head.Length > 0 && head.Trim(Separator).Length > 0) {
            head = head.TrimEnd(Separator);
        }

        return head;
    }

    public static string Join(string path, params string[] paths) {
        string result = path;
        foreach (string part in paths) {
            if (part.StartsWith(Separator)) {
                result = part;
            }
            else if (result.Length == 0 || result.EndsWith(Separator)) {
                result += part;
            }
            else {
                result += Separator + part;
            }
        }

        return result;
    }
}
