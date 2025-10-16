using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace PigPicPot.Helpers
{
    public static class PathHelper
    {
        public static string GetApplicationRoot()
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName 
                ?? Assembly.GetEntryAssembly()?.Location 
                ?? AppDomain.CurrentDomain.BaseDirectory;
            
            return Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}