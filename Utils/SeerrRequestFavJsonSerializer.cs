using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace Jellyfin.Plugin.SeerrRequestFav.Utils;

#region Deserializer Converter

/// <summary>
/// Factory for creating SafeObjectConverter instances.
/// Applies robust deserialization that skips invalid objects instead of throwing.
/// </summary>
public class SafeObjectConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsClass && typeToConvert != typeof(string);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var filteredOptions = new JsonSerializerOptions(options);
        var toRemove = filteredOptions.Converters.OfType<SafeObjectConverterFactory>().SingleOrDefault();
        if (toRemove != null) filteredOptions.Converters.Remove(toRemove);
        var converterType = typeof(SafeObjectConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType, filteredOptions)!;
    }
}

public class SafeObjectConverter<T> : JsonConverter<T>
{
    private readonly JsonSerializerOptions _filteredOptions;

    public SafeObjectConverter(JsonSerializerOptions filteredOptions)
    {
        _filteredOptions = filteredOptions;
    }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(ref reader, _filteredOptions);
        }
        catch
        {
            reader.Skip();
            return default;
        }
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, _filteredOptions);
}

#endregion

#region Serializer Converter

/// <summary>
/// Serializes all public instance properties of the actual runtime type.
/// </summary>
public class SeerrPropertiesConverter<T> : JsonConverter<T> where T : class
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<T>(ref reader, options);

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (value == null) { writer.WriteNullValue(); return; }

        writer.WriteStartObject();
        var actualType = value.GetType();
        var properties = actualType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>(false) != null) continue;
            var jsonNameAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            string jsonName = jsonNameAttr?.Name ?? prop.Name;
            if (jsonName.Contains("_")) continue;

            writer.WritePropertyName(jsonName);
            JsonSerializer.Serialize(writer, prop.GetValue(value), prop.PropertyType, options);
        }

        writer.WriteEndObject();
    }
}

#endregion

#region Json Serializer

/// <summary>
/// Static JSON serialization helpers for the SeerrRequestFav plugin.
/// </summary>
public static class SeerrRequestFavJsonSerializer
{
    public static JsonSerializerOptions DefaultSerializerOptions<T>() where T : class
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            Converters = { new SeerrPropertiesConverter<T>() }
        };
    }

    public static string Serialize<T>(T value) where T : class
        => JsonSerializer.Serialize(value, DefaultSerializerOptions<T>());

    public static string Serialize(object? value)
    {
        if (value == null) return "null";
        return JsonSerializer.Serialize(value, value.GetType(), DefaultSerializerOptions<object>());
    }

    public static JsonSerializerOptions DefaultDeserializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            Converters = { new SafeObjectConverterFactory() }
        };
    }

    public static object? Deserialize(string json, Type type)
        => JsonSerializer.Deserialize(json, type, DefaultDeserializerOptions());

    public static T? Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, DefaultDeserializerOptions());
}

#endregion
