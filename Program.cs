using System;
using System.IO;

namespace ETR
{
    /// <summary>
    /// Custom entry point that dispatches to CLI or WPF GUI based on argv.
    /// Wired via &lt;StartupObject&gt; in ETR.csproj.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return RunGui();
            }

            if (args.Length > 2)
            {
                AttachParentConsole();
                Console.Error.WriteLine("Usage: ETR.exe [<input.exe> [<output.exe>]]");
                Console.Error.WriteLine("  (no args) opens the GUI");
                Console.Error.WriteLine("  <input>            CLI: writes <input-name>-Removed<ext>");
                Console.Error.WriteLine("  <input> <output>   CLI: writes to <output>");
                return 2;
            }

            AttachParentConsole();
            return RunCli(args[0], args.Length == 2 ? args[1] : null);
        }

        static int RunGui()
        {
            // Existing WPF startup: instantiate the auto-generated App and run.
            var app = new App();
            app.InitializeComponent();
            app.Run(new MainWindow());
            return 0;
        }

        static int RunCli(string input, string outputOrNull)
        {
            try
            {
                if (!File.Exists(input))
                {
                    Console.Error.WriteLine("[ETR] Input file not found: " + input);
                    return 1;
                }

                string output = outputOrNull ?? DefaultRemovedPath(input);

                Console.WriteLine("[ETR] Input : " + input);
                Console.WriteLine("[ETR] Output: " + output);

                var result = Patcher.Run(input, output,
                    (pct, msg) => Console.WriteLine("[ETR] " + pct + "% " + msg));

                switch (result)
                {
                    case PatchResult.Success:
                        Console.WriteLine("[ETR] Done.");
                        return 0;
                    case PatchResult.MultipleTypesUsedFirst:
                        Console.WriteLine("[ETR] Done (multiple candidate types; used the first).");
                        return 0;
                    case PatchResult.NoTypeFound:
                        Console.Error.WriteLine("[ETR] No Eazfuscator evaluation type found — version not supported.");
                        return 1;
                    case PatchResult.WriteFailed:
                        Console.Error.WriteLine("[ETR] Failed to write patched module to " + output);
                        return 1;
                    default:
                        Console.Error.WriteLine("[ETR] Unknown result: " + result);
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ETR] Exception: " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        static string DefaultRemovedPath(string input)
        {
            string dir = Path.GetDirectoryName(input);
            string noExt = Path.GetFileNameWithoutExtension(input);
            string ext = Path.GetExtension(input);
            return Path.Combine(dir ?? "", noExt + "-Removed" + ext);
        }

        static void AttachParentConsole()
        {
            // Best-effort. If parent has no console (e.g. launched from Explorer),
            // AttachConsole returns false and stdout stays disconnected — fine.
            NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
        }
    }
}
