using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ghost.Core.Models;

/// <summary>System.Text.Json has no built-in converter for nint; snapshots serialize the window handle as a plain number.</summary>
public sealed class NintJsonConverter : JsonConverter<nint>
{
    public override nint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        (nint)reader.GetInt64();

    public override void Write(Utf8JsonWriter writer, nint value, JsonSerializerOptions options) =>
        writer.WriteNumberValue((long)value);
}
