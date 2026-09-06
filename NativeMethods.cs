using System;
using System.Runtime.InteropServices;

namespace ETR
{
    /// <summary>P/Invoke declarations for CLI console attachment.</summary>
    internal static class NativeMethods
    {
        internal const int ATTACH_PARENT_PROCESS = -1;

        /// <summary>
        /// Attach this process to the parent console so Console.WriteLine reaches
        /// the parent shell (cmd.exe / PowerShell). Only meaningful for WinExe
        /// processes; no-op if already attached.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AttachConsole(int dwProcessId);
    }
}
