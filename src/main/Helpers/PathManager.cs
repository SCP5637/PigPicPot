using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;

namespace PigPicPot.Helpers
{
    /// <summary>
    /// 路径管理器，用于管理应用程序的各种路径
    /// Path manager for handling application paths
    /// </summary>
    public static class PathManager
    {
        /// <summary>
        /// 应用程序根目录路径
        /// Application root directory path
        /// </summary>
        public static string AppRoot { get; private set; }

        /// <summary>
        /// 数据根目录路径
        /// Data root directory path
        /// </summary>
        public static string DataRoot { get; private set; }

        /// <summary>
        /// 静态构造函数，初始化路径
        /// Static constructor to initialize paths
        /// </summary>
        static PathManager()
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName 
                ?? Assembly.GetEntryAssembly()?.Location 
                ?? AppDomain.CurrentDomain.BaseDirectory;
            
            AppRoot = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            DataRoot = AppRoot;
        }

        /// <summary>
        /// 初始化路径管理器
        /// Initialize path manager
        /// </summary>
        /// <param name="args">命令行参数</param>
        public static void Initialize(string[] args)
        {
            if (args.Contains("--data-dir-sln"))
            {
                string? slnDir = FindSolutionDirectory(AppRoot);
                if (slnDir != null)
                {
                    DataRoot = slnDir;
                }
            }
        }

        /// <summary>
        /// 查找解决方案目录
        /// Find solution directory
        /// </summary>
        /// <param name="startPath">起始搜索路径</param>
        /// <returns>解决方案目录路径，未找到则返回null</returns>
        private static string? FindSolutionDirectory(string startPath)
        {
            try
            {
                DirectoryInfo? currentDir = new DirectoryInfo(startPath);
                while (currentDir != null)
                {
                    if (Directory.GetFiles(currentDir.FullName, "PigPicPot.sln").Any())
                    {
                        return currentDir.FullName;
                    }
                    currentDir = currentDir.Parent;
                }
            }
            catch (Exception) {
            }
            return null;
        }
    }
}