using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PigPicPot.Helpers;

namespace PigPicPot.Helpers
{
    /// <summary>
    /// 日志记录助手类，提供应用程序的日志记录功能
    /// Logging helper class, provides logging functionality for the application
    /// </summary>
    public static class LoggingHelper
    {
        private static readonly string LogFilePath = Path.Combine(PathManager.DataRoot, "run_log.txt");
        private static readonly object LogLock = new object();
        private static bool _initialized = false;

        /// <summary>
        /// 初始化日志系统
        /// Initialize the logging system
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // 确保日志目录存在
                var logDir = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                // 清空旧的日志文件
                if (File.Exists(LogFilePath))
                {
                    File.WriteAllText(LogFilePath, string.Empty);
                }

                _initialized = true;
                Log("Logging system initialized.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize logging system: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录日志信息
        /// Record log information
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="level">日志级别</param>
        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (!_initialized) Initialize();

            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                
                // 同时输出到控制台和日志文件
                Console.WriteLine(logEntry);
                
                // 写入日志文件
                lock (LogLock)
                {
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write log: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录异常信息
        /// Record exception information
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="additionalInfo">附加信息</param>
        public static void LogException(Exception ex, string additionalInfo = "")
        {
            if (!_initialized) Initialize();

            try
            {
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] {additionalInfo}{Environment.NewLine}{ex}";
                
                // 同时输出到控制台和日志文件
                Console.WriteLine(logEntry);
                
                // 写入日志文件
                lock (LogLock)
                {
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write exception log: {logEx.Message}");
            }
        }
    }

    /// <summary>
    /// 日志级别枚举
    /// Log level enumeration
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}