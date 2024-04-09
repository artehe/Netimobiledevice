using Netimobiledevice.Afc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Netimobiledevice.Lockdown.Services
{
    public class CrashReportsManager : IDisposable
    {
        private const string LOCKDOWN_COPY_MOBILE_NAME = "com.apple.crashreportcopymobile";
        private const string RSD_COPY_MOBILE_NAME = "com.apple.crashreportcopymobile.shim.remote";

        private const string LOCKDOWN_CRASH_MOVER_NAME = "com.apple.crashreportmover";
        private const string RSD_CRASH_MOVER_NAME = "com.apple.crashreportmover.shim.remote";

        private const string APPSTORED_PATH = "/com.apple.appstored";

        private readonly AfcService _afcService;
        private readonly LockdownServiceProvider _lockdown;

        private readonly string _copyMobileServiceName;
        private readonly string _crashMoverServiceName;

        public CrashReportsManager(LockdownServiceProvider lockdown)
        {
            _lockdown = lockdown;

            if (lockdown is LockdownClient) {
                _copyMobileServiceName = LOCKDOWN_COPY_MOBILE_NAME;
                _crashMoverServiceName = LOCKDOWN_CRASH_MOVER_NAME;
            }
            else {
                _copyMobileServiceName = RSD_COPY_MOBILE_NAME;
                _crashMoverServiceName = RSD_CRASH_MOVER_NAME;
            }

            _afcService = new AfcService(lockdown, _copyMobileServiceName);
        }

        public void Close()
        {
            _afcService.Close();
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Clear all crash reports
        /// </summary>
        public void Clear()
        {
            List<string> undeletedFiles = new List<string>();
            foreach (string filename in GetCrashReportsList("/")) {
                undeletedFiles.AddRange(_afcService.Rm(filename, force: true));
            }

            foreach (string item in undeletedFiles) {
                // Special case of file that comtimes created itself autmatically right after deleting,
                // and then we can't delete the folder because it's not empty
                if (item != APPSTORED_PATH) {
                    throw new AfcException($"Failed to clear crash reports directory, undeleted items: {string.Join(", ", undeletedFiles)}");
                }
            }
        }

        /// <summary>
        /// List the files and folders in the crash reports directory
        /// </summary>
        /// <param name="path">Path to list, relative to the crash report's directory</param>
        /// <param name="depth">Listing depth, -1 to list infinite depth</param>
        /// <returns>List of files found</returns>
        public List<string> GetCrashReportsList(string path = "/", int depth = 1)
        {
            // Get the results then skip the root path '/'
            List<string> results = _afcService.LsDirectory(path, depth).Skip(1).ToList();
            return results;
        }

        /// <summary>
        /// Pull crash report(s) from the device
        /// </summary>
        /// <param name="outDir">The directory to pull the crash report(s) to</param>
        /// <param name="entry">File or folder to pull</param>
        /// <param name="erase">Whether to erase the original file form the CrashReports directory</param>
        public void GetCrashReport(string outDir, string entry = "/", bool erase = false)
        {
            _afcService.Pull(entry, outDir);

            if (erase) {
                string[] paths = new string[] { ".", "/" };
                if (paths.Contains(entry.Trim())) {
                    Clear();
                }
                else {
                    _afcService.Rm(entry, force: true);
                }
            }
        }
    }
}
