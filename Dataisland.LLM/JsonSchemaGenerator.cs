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

    /// <summary>
    /// Produce a per-request schema string by injecting an <c>"enum"</c> constraint into the
    /// named top-level properties of <paramref name="baseSchema"/>. Used when the set of
    /// allowed values is only known at call time (e.g. the candidate file IDs for Phase C
    /// file-selection) — the type-level schema permits any string, this narrows it to the
    /// actual whitelist so the model can't hallucinate a value outside it.
    ///
    /// Returns a fresh JSON string; <paramref name="baseSchema"/> (and the module cache) is
    /// untouched.
    /// </summary>
    public static string ApplyPropertyEnums(
        string baseSchema,
        IReadOnlyDictionary<string, string[]> propertyEnums)
    {
        if (propertyEnums.Count == 0) return baseSchema;

        using var doc = JsonDocument.Parse(baseSchema);
        var root = JsonElementToMutable(doc.RootElement) as Dictionary<string, object?>
            ?? throw new InvalidOperationException("Schema root must be an object.");

        if (!root.TryGetValue("properties", out var propsObj)
            || propsObj is not Dictionary<string, object?> props)
        {
            return baseSchema;
        }

        foreach (var (propName, allowed) in propertyEnums)
        {
            if (!props.TryGetValue(propName, out var propSchemaObj)
                || propSchemaObj is not Dictionary<string, object?> propSchema)
            {
                continue;
            }
            propSchema["enum"] = allowed;
        }

        return JsonSerializer.Serialize(root, SerializerOptions);
    }

    private static object? JsonElementToMutable(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => JsonElementToMutable(p.Value)),
        JsonValueKind.Array => element.EnumerateArray()
            .Select(JsonElementToMutable).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => null
    };

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
