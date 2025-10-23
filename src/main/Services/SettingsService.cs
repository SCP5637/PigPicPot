using System;
using System.IO;
using System.Threading.Tasks;
using PigPicPot.Helpers;

namespace PigPicPot.Services
{
    public class SettingsService : ISettingsService
    {
        public async Task SavePinState(bool isPinned)
        {
            try
            {
                var settings = new { IsPinned = isPinned };
                string json = System.Text.Json.JsonSerializer.Serialize(settings);
                string filePath = Path.Combine(PathManager.DataRoot, "usersettings.json");
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save pin state: {ex.Message}");
            }
        }

        public async Task<bool> LoadPinState()
        {
            try
            {
                // 检查是否应该在每次启动时重置状态
                if (ShouldResetStateOnStartup())
                {
                    return false; // 重置状态，返回默认值
                }
                
                string filePath = Path.Combine(PathManager.DataRoot, "usersettings.json");
                if (File.Exists(filePath))
                {
                    string json = await File.ReadAllTextAsync(filePath);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                    if (settings.TryGetProperty("IsPinned", out var isPinnedProperty))
                    {
                        return isPinnedProperty.GetBoolean();
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load pin state: {ex.Message}");
            }
            return false; // Default value
        }
        
        /// <summary>
        /// 检查是否应该在每次启动时重置状态
        /// </summary>
        /// <returns>如果应该重置状态则返回true，否则返回false</returns>
        private bool ShouldResetStateOnStartup()
        {
            try
            {
                string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
                if (!File.Exists(configFile))
                    return true; // 如果没有配置文件，默认重置状态

                // 尝试读取JSON格式的配置文件
                string jsonContent = File.ReadAllText(configFile);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                
                // 检查是否有reset_mini_mode_state配置项
                if (jsonDoc.RootElement.TryGetProperty("reset_mini_mode_state", out var resetStateElement))
                {
                    return resetStateElement.GetString()?.ToLower() == "true";
                }
                
                // 如果没有找到配置项，默认重置状态
                return true;
            }
            catch (System.Text.Json.JsonException)
            {
                // 如果JSON解析失败，尝试使用旧的INI格式解析
                try
                {
                    string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
                    var config = File.ReadAllLines(configFile);
                    var resetStateLine = Array.Find(config, line => line.StartsWith("reset_mini_mode_state="));
                    if (resetStateLine != null)
                    {
                        var resetStateValue = resetStateLine.Split('=')[1].Trim();
                        return resetStateValue.ToLower() == "true";
                    }
                    // 如果没有找到配置项，默认重置状态
                    return true;
                }
                catch
                {
                    // 如果解析失败，默认重置状态
                    return true;
                }
            }
            catch
            {
                // 如果出现其他异常，默认重置状态
                return true;
            }
        }
    }
}