using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dataisland.LLM;

/// <summary>
/// Generates JSON Schema from C# types for LLM structured outputs.
/// Supports records, classes, primitives, enums, lists, and nullable types.
/// </summary>
public static class JsonSchemaGenerator
{
    private static readonly ConcurrentDictionary<Type, string> SchemaCache = new();

    /// <summary>Generate a JSON Schema string for the given type.</summary>
    public static string Generate<T>() => Generate(typeof(T));

    /// <summary>Generate a JSON Schema string for the given type.</summary>
    public static string Generate(Type type)
    {
        return SchemaCache.GetOrAdd(type, t =>
        {
            var schema = BuildSchema(t, []);
            return JsonSerializer.Serialize(schema, SerializerOptions);
        });
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static Dictionary<string, object?> BuildSchema(Type type, HashSet<Type> visited)
    {
        // Unwrap Nullable<T>
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            var inner = BuildSchema(underlying, visited);
            // OpenAI structured outputs don't support "nullable", use anyOf
            return inner;
        }

        // Primitives
        if (type == typeof(string))
            return new() { ["type"] = "string" };
        if (type == typeof(bool))
            return new() { ["type"] = "boolean" };
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
            return new() { ["type"] = "integer" };
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return new() { ["type"] = "number" };

        // Enums → string with enum values
        if (type.IsEnum)
        {
            return new()
            {
                ["type"] = "string",
                ["enum"] = Enum.GetNames(type)
            };
        }

        // Arrays / Lists / IReadOnlyList<T> / IList<T>
        var elementType = GetCollectionElementType(type);
        if (elementType is not null)
        {
            return new()
            {
                ["type"] = "array",
                ["items"] = BuildSchema(elementType, visited)
            };
        }

        // Dictionary<string, T> → object with additionalProperties
        var dictValueType = GetDictionaryValueType(type);
        if (dictValueType is not null)
        {
            return new()
            {
                ["type"] = "object",
                ["additionalProperties"] = BuildSchema(dictValueType, visited)
            };
        }

        // Complex object
        if (!visited.Add(type))
        {
            // Circular reference — emit as generic object
            return new() { ["type"] = "object" };
        }

        var properties = new Dictionary<string, object?>();
        var required = new List<string>();

        foreach (var prop in GetSerializableProperties(type))
        {
            var name = GetJsonPropertyName(prop);
            properties[name] = BuildSchema(prop.PropertyType, visited);

            // Required if non-nullable value type or non-nullable reference type
            if (!IsNullable(prop))
                required.Add(name);
        }

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false
        };

        if (required.Count > 0)
            schema["required"] = required;

        visited.Remove(type);
        return schema;
    }

    private static IEnumerable<PropertyInfo> GetSerializableProperties(Type type)
    {
        // Get public instance properties, including from records (init-only)
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null
                     && p.CanRead);
    }

    private static string GetJsonPropertyName(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (attr is not null)
            return attr.Name;

        // Default to camelCase
        var name = prop.Name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static bool IsNullable(PropertyInfo prop)
    {
        // Nullable value type
        if (Nullable.GetUnderlyingType(prop.PropertyType) is not null)
            return true;

        // Check NullableAttribute or NullabilityInfo for reference types
        var context = new NullabilityInfoContext();
        var info = context.Create(prop);
        return info.ReadState == NullabilityState.Nullable;
    }

    private static Type? GetCollectionElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();

        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(List<>) || def == typeof(IList<>) ||
                def == typeof(IReadOnlyList<>) || def == typeof(IEnumerable<>) ||
                def == typeof(ICollection<>) || def == typeof(IReadOnlyCollection<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static Type? GetDictionaryValueType(Type type)
    {
        if (!type.IsGenericType) return null;

        var def = type.GetGenericTypeDefinition();
        if (def == typeof(Dictionary<,>) || def == typeof(IDictionary<,>) ||
            def == typeof(IReadOnlyDictionary<,>))
        {
            var args = type.GetGenericArguments();
            if (args[0] == typeof(string))
                return args[1];
        }

        return null;
    }
}
