using System.IO;
using System.Runtime.InteropServices;

namespace PigPicPot
{
    public static class DebugConsole
    {
        private static bool _consoleAllocated = false;

        public static void Show()
        {
            if (_consoleAllocated) return;

            try
            {
                string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.cfg");
                if (File.Exists(configFile))
                {
                    var content = File.ReadAllText(configFile);
                    if (content.Contains("debug=true"))
                    {
                        AllocConsole();
                        _consoleAllocated = true;
                        Console.WriteLine("Debug console enabled.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Can't write to console if it failed, but we can avoid crashing.
                System.Diagnostics.Debug.WriteLine($"Error showing debug console: {ex.Message}");
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();
    }
}
