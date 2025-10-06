using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Netimobiledevice.Remoted;

internal partial class RemotedProcessStopper : IDisposable {
    private const string REMOTED_PATH = "/usr/libexec/remoted";
    /// <summary>
    /// Resume process signal for UNIX systems
    /// </summary>
    private const int SIGCONT = 18;
    /// <summary>
    /// Suspend process signal for UNIX systems
    /// </summary>
    private const int SIGSTOP = 19;

    private static Process? GetRemotedProcess() {
        foreach (Process process in Process.GetProcesses()) {
            if (process.Id == 0) {
                // Skip the system idle process, similar to skipping pid 0 in Python
                continue;
            }

            try {
                // Check the main module's file path
                string exePath = process.MainModule?.FileName ?? string.Empty;

                if (string.Equals(exePath, REMOTED_PATH, StringComparison.OrdinalIgnoreCase)) {
                    return process;
                }
            }
            catch (System.ComponentModel.Win32Exception) {
                // Access denied (e.g., for system processes)
                continue;
            }
            catch (InvalidOperationException) {
                // Process may have exited
                continue;
            }
        }

        return null;
    }

    [LibraryImport("libc")]
    private static partial int Kill(int pid, int sig);

    private static void ResumeRemotedIfRequired() {
        if (!OperatingSystem.IsMacOS()) {
            // Only required for macOS
            return;
        }

        Process? remoted = GetRemotedProcess();
        if (remoted == null) {
            return;
        }

        try {
            if (!remoted.HasExited || remoted.Responding) {
                return;
            }

            int result = Kill(remoted.Id, SIGCONT);
            if (result != 0) {
                throw new AccessDeniedException("Failed to resume process.");
            }
        }
        catch (Exception ex) {
            throw new AccessDeniedException("Access denied when suspending the process.", ex);
        }
    }

    private static void StopRemotedIfRequired() {
        if (!OperatingSystem.IsMacOS()) {
            // Only required for macOS
            return;
        }

        Process? remoted = GetRemotedProcess();
        if (remoted == null) {
            return;
        }

        try {
            if (remoted.HasExited) {
                return;
            }

            if (!remoted.Responding) {
                // Process already unresponsive / suspended
                return;
            }

            int result = Kill(remoted.Id, SIGSTOP);
            if (result != 0) {
                throw new AccessDeniedException("Failed to suspend process.");
            }
        }
        catch (Exception ex) {
            throw new AccessDeniedException("Access denied when suspending the process.", ex);
        }
    }

    public RemotedProcessStopper() {
        StopRemotedIfRequired();
    }

    public void Dispose() {
        ResumeRemotedIfRequired();
    }
}
