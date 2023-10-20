using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workbench.Shared;

internal class EnumConverter<T>
    where T : struct
{
    private readonly Dictionary<string, T> from_string = new();
    private readonly Dictionary<T, string> to_string = new();

    public EnumConverter<T> Add(T value, string primary_name, params string[] other_names)
    {
        to_string.Add(value, primary_name);
        from_string.Add(Transform(primary_name), value);
        foreach (var name in other_names)
        {
            from_string.Add(Transform(name), value);
        }

        return this;
    }

    public string EnumToString(T value)
    {
        if (to_string.TryGetValue(value, out var str))
        {
            return str;
        }

        Debug.Assert(false, "missing value");
        return $"<BUG: missing converter for {value}>";
    }

    public (T?, string) StringToEnum(string name)
    {
        if (from_string.TryGetValue(Transform(name), out var ret))
        {
            return (ret, string.Empty);
        }

        Debug.Assert(from_string.Count > 0);

        static string simple_quote(string s)
        {
            if (s.Contains('"'))
            {
                s = s.Replace("\\", "\\\\")
                     .Replace("\"", "\\\"")
                     ;
            }
            return $"\"{s}\"";
        }

        var suggestions = EditDistance.ClosestMatches(3, Transform(name), max_diff: 5, candidates: from_string.Keys)
            .Select(simple_quote).ToArray();
        if (suggestions.Length == 0)
        {
            var all = StringListCombiner.EnglishOr().CombineArray(from_string.Keys.Select(simple_quote).ToArray());
            return (null, $"Invalid value {simple_quote(name)}! You need to select either {all}");
        }
        else
        {
            var mean = StringListCombiner.EnglishOr().CombineArray(suggestions);
            return (null, $"Invalid value {simple_quote(name)}! Perhaps you meant {mean}?");
        }
    }

    private static string Transform(string name)
    {
        return name.Trim().ToLowerInvariant();
    }

    internal static EnumConverter<T> ReflectValues()
    {
        var ret = new EnumConverter<T>();

        foreach (var (att, val) in Reflect.PublicStaticValuesOf<T, EnumStringAttribute>(Reflect.Attributes<EnumStringAttribute>()))
        {
            ret.Add(val, att.PrimaryName, att.OtherNames);
        }

        Debug.Assert(ret.to_string.Count > 0, "Missing entries");

        return ret;
    }
}

internal static class ReflectedValues<T>
    where T : struct
{
    public static readonly EnumConverter<T> Converter = EnumConverter<T>.ReflectValues();
}

public static class EnumTools
{
    public static string? GetString<T>(T? t)
        where T : struct
    {
        if (t == null) return null;
        else return ReflectedValues<T>.Converter.EnumToString(t.Value);
    }
}


public class EnumStringAttribute : Attribute
{
    public string PrimaryName { get; }
    public string[] OtherNames { get; }

    public EnumStringAttribute(string primary_name, params string[] other_names)
    {
        PrimaryName = primary_name;
        OtherNames = other_names;
    }
}

internal class EnumTypeConverter<T> : TypeConverter
    where T : struct
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type source_type)
    {
        return source_type == typeof(string) || base.CanConvertFrom(context, source_type);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string name)
        {
            return base.ConvertFrom(context, culture, value);
        }

        var (ret, error) = ReflectedValues<T>.Converter.StringToEnum(name);
        if (ret == null)
        {
            throw new NotSupportedException(error);
        }

        return ret;
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, [NotNullWhen(true)] Type? destination_type)
    {
        return base.CanConvertTo(context, destination_type);
    }

    public override object? ConvertTo(
        ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destination_type)
    {
        return destination_type == typeof(string) && value is T enum_value
            ? ReflectedValues<T>.Converter.EnumToString(enum_value)
            : base.ConvertTo(context, culture, value, destination_type);
    }
}

internal class EnumJsonConverter<T> : JsonConverter<T>
    where T : struct
{
    public override bool HandleNull => false;

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ReflectedValues<T>.Converter.EnumToString(value));
    }

    public override T Read(ref Utf8JsonReader reader, Type type_to_convert, JsonSerializerOptions options)
    {
        var name = reader.GetString()!;

        var (ret, error) = ReflectedValues<T>.Converter.StringToEnum(name);
        if (ret == null)
        {
            // AppendPathInformation is internal
            throw new JsonException(error);//  { AppendPathInformation = true };

            // use JsonSerializationException.Create but that is internal
            // same issue with Json.Net
        }

        return ret.Value;
    }
}
