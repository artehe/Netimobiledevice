using System.Reflection;

namespace NetimobiledeviceTest.TestFiles
{
    internal static class TestFileHelper
    {
        internal static Stream GetTestFileStream(string relativeFilePath)
        {
            const char namespaceSeparator = '.';

            // get calling assembly
            Assembly assembly = Assembly.GetCallingAssembly();

            // compute resource name suffix (replace Windows/Unix directory separators with namespace separator)
            string relativeName = "." + relativeFilePath
                .Replace('/', namespaceSeparator)
                .Replace('\\', namespaceSeparator)
                .Replace(' ', '_');

            // get resource stream
            string? fullName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(relativeName, StringComparison.InvariantCulture));
            if (string.IsNullOrEmpty(fullName)) {
                throw new Exception($"Unable to find resource for path \"{relativeFilePath}\". Resource with name ending on \"{relativeName}\" was not found in assembly.");
            }

            Stream? stream = assembly.GetManifestResourceStream(fullName);
            return stream ?? throw new Exception($"Unable to find resource for path \"{relativeFilePath}\". Resource named \"{fullName}\" was not found in assembly.");
        }
    }
}
