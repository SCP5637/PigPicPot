using System;
using System.IO;

public static class PathHelper
{
    public static string GetApplicationRoot()
    {
        string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        string? exeDir = Path.GetDirectoryName(exePath);
        return exeDir ?? AppDomain.CurrentDomain.BaseDirectory;
    }
}
