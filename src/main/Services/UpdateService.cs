using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using PigPicPot.Helpers;

namespace PigPicPot.Services
{
    /// <summary>
    /// GitHub发布信息类
    /// GitHub release information class
    /// </summary>
    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 更新服务，负责检查应用程序和资源更新
    /// Update service, responsible for checking application and resource updates
    /// </summary>
    public class UpdateService
    {
        private readonly ConfigurationService _configurationService;

        /// <summary>
        /// 构造函数
        /// Constructor
        /// </summary>
        /// <param name="configurationService">配置服务</param>
        public UpdateService(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        /// <summary>
        /// 检查资源更新
        /// Check for resource updates
        /// </summary>
        /// <param name="showNotification">显示通知的回调函数</param>
        public async Task CheckForResourceUpdate(Action<string> showNotification)
        {
            LoggingHelper.Log("Checking for resource updates...");
            var config = _configurationService.GetConfig();
            if (config.TryGetValue("check_for_updates", out var check) && check.ToLower() == "false")
            {
                LoggingHelper.Log("Resource update check disabled.");
                return;
            }

            try
            {
                string versionPath = Path.Combine(PathManager.AppRoot, "resource", "version.txt");
                if (!File.Exists(versionPath))
                {
                    LoggingHelper.Log("Version file not found, skipping resource update check.");
                    return;
                }
                string localVersion = File.ReadAllText(versionPath).Trim();

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "PigPicPot");
                    var response = await client.GetAsync("https://api.github.com/repos/JodieRuth/PigPicPot/releases/latest");
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json);
                    if (release == null)
                    {
                        LoggingHelper.Log("Failed to deserialize GitHub release info.");
                        return;
                    }
                    string latestVersion = release.TagName;

                    if (new Version(localVersion.TrimStart('v')) < new Version(latestVersion.TrimStart('v')))
                    {
                        showNotification(PigPicPot.Strings.Resources.ResourceUpdateAvailable);
                        Process.Start(new ProcessStartInfo("https://github.com/JodieRuth/PigPicPot/releases/latest") { UseShellExecute = true });
                        LoggingHelper.Log("Resource update available.");
                    }
                    else
                    {
                        LoggingHelper.Log("Resource is up to date.");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Resource update check failed");
                Console.WriteLine($"Resource update check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查应用程序更新
        /// Check for application updates
        /// </summary>
        /// <param name="showNotification">显示通知的回调函数</param>
        public async Task CheckForAppUpdate(Action<string> showNotification)
        {
            LoggingHelper.Log("Checking for application updates...");
            var config = _configurationService.GetConfig();
            if (config.TryGetValue("check_for_updates", out var check) && check.ToLower() == "false")
            {
                LoggingHelper.Log("Application update check disabled.");
                return;
            }

            try
            {
                string currentVersion = "v0.5";
                File.WriteAllText(Path.Combine(PathManager.AppRoot, "version.txt"), currentVersion);

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "PigPicPot");
                    var response = await client.GetAsync("https://api.github.com/repos/SCP5637/PigPicPot/releases/latest");
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json);
                    if (release == null)
                    {
                        LoggingHelper.Log("Failed to deserialize GitHub release info.");
                        return;
                    }
                    string latestVersion = release.TagName;

                    if (new Version(currentVersion.TrimStart('v')) < new Version(latestVersion.TrimStart('v')))
                    {
                        showNotification(PigPicPot.Strings.Resources.ApplicationUpdateAvailable);
                        Process.Start(new ProcessStartInfo("https://github.com/SCP5637/PigPicPot/releases/latest") { UseShellExecute = true });
                        LoggingHelper.Log("Application update available.");
                    }
                    else
                    {
                        LoggingHelper.Log("Application is up to date.");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "App update check failed");
                Console.WriteLine($"App update check failed: {ex.Message}");
            }
        }
    }
}