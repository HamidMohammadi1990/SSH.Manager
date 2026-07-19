using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SshManager.Models;

namespace SshManager.Services;

public class JsonDataService
{
    private static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SshManager");

    private static readonly string DataFilePath = Path.Combine(AppFolder, "data.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string DataFilePathPublic => DataFilePath;

    public AppData Load()
    {
        try
        {
            if (!File.Exists(DataFilePath))
                return new AppData();

            var json = File.ReadAllText(DataFilePath);
            return JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? new AppData();
        }
        catch
        {
            return new AppData();
        }
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(AppFolder);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(DataFilePath, json);
    }

    public void Export(AppData data, string filePath)
    {
        var exportData = CloneForExport(data);
        var json = JsonSerializer.Serialize(exportData, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public AppData Import(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? new AppData();
    }

    private static AppData CloneForExport(AppData data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        return JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? new AppData();
    }
}
