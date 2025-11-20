using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Netimobiledevice.Utils;

public static class PathSanitiser {
    private enum WindowsPathParserState {
        PossibleDriveLetter,
        PossibleDriveLetterSeparator,
        Path
    }

    public static string SantiseWindowsPath(string sourcePath) {
        if (string.IsNullOrEmpty(sourcePath)) {
            throw new ArgumentException("sourcePath cannot be null or empty", nameof(sourcePath));
        }

        // Remove the leading and trailing white spaces so we know we are starting from a sane position
        sourcePath = sourcePath.Trim();

        StringBuilder output = new StringBuilder(sourcePath.Length);
        WindowsPathParserState state = WindowsPathParserState.PossibleDriveLetter;
        foreach (char current in sourcePath) {
            if (
                (current >= 'a' && current <= 'z') ||
                (current >= 'A' && current <= 'Z')
            ) {
                output.Append(current);
                if (state == WindowsPathParserState.PossibleDriveLetter) {
                    state = WindowsPathParserState.PossibleDriveLetterSeparator;
                }
                else {
                    state = WindowsPathParserState.Path;
                }
            }
            else if (
                current == Path.DirectorySeparatorChar ||
                current == Path.AltDirectorySeparatorChar ||
                (current == ':' && state == WindowsPathParserState.PossibleDriveLetterSeparator) ||
                !Path.GetInvalidFileNameChars().Contains(current)
            ) {

                output.Append(current);
                state = WindowsPathParserState.Path;
            }
            else {
                output.Append('_');
                state = WindowsPathParserState.Path;
            }
        }
        return output.ToString();
    }
}
