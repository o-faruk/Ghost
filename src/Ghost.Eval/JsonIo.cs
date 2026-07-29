using System.Text.Json;
using System.Text.Json.Serialization;
using Ghost.Core.Models;

namespace Ghost.Eval;

internal static class JsonIo
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new NintJsonConverter(), new JsonStringEnumConverter() },
    };

    public static T ReadRequired<T>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new InvalidDataException($"failed to deserialize {typeof(T).Name} from {path}");
    }

    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
    }
}
