
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SPS.App.Services;

public sealed class JsonStorage
{
    public void Save(string path, ParametersSnapshot snapshot)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) 
            {
                Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception) { /* Handle exception, e.g., log it */ }
    }

    public ParametersSnapshot LoadOrDefault(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<ParametersSnapshot>(json);
                if (loaded != null) return loaded;
            }
        }
        catch (Exception) { /* Handle exception */ }

        return DefaultsFactory.CreateDefaultsSnapshot();
    }
}
