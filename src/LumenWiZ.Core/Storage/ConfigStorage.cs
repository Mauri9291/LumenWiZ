using System.Text.Json;
using LumenWiZ.Core.Models;

namespace LumenWiZ.Core.Storage;

public class ConfigStorage
{
    private readonly string _storageDirectory;
    private readonly string _filePath;

    public ConfigStorage(string? customDirectory = null)
    {
        _storageDirectory = customDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "LumenWiZ"
        );
        _filePath = Path.Combine(_storageDirectory, "config.json");
    }

    public async Task<AppConfig> LoadConfigAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new AppConfig();
        }

        try
        {
            string json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }

        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }
}
