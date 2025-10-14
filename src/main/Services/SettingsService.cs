using System;
using System.IO;
using System.Threading.Tasks;

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
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_settings.json");
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
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_settings.json");
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
    }
}
