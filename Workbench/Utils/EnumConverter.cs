using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workbench.Utils;

internal class EnumConverter<T>
    where T : struct
{
    private readonly Dictionary<string, T> fromString = new();
    private readonly Dictionary<T, string> toString = new();

    public EnumConverter<T> Add(T value, string primaryName, params string[] otherNames)
    {
        toString.Add(value, primaryName);
        fromString.Add(Transform(primaryName), value);
        foreach (var name in otherNames)
        {
            fromString.Add(Transform(name), value);
        }

        return this;
    }

    public string EnumToString(T value)
    {
        if (toString.TryGetValue(value, out var str))
        {
            return str;
        }

        Debug.Assert(false, "missing value");
        return $"<BUG: missing converter for {value}>";
    }

    public (T?, string) StringToEnum(string name)
    {
        if (fromString.TryGetValue(Transform(name), out var ret))
        {
            return (ret, string.Empty);
        }

        Debug.Assert(fromString.Count > 0);

        static string SimpleQuote(string s)
        {
            if (s.Contains('"'))
            {
                s = s.Replace("\\", "\\\\")
                     .Replace("\"", "\\\"")
                     ;
            }
            return $"\"{s}\"";
        }

        var suggestions = EditDistance.ClosestMatches(3, Transform(name), maxDiff: 5, candidates: fromString.Keys)
            .Select(SimpleQuote).ToArray();
        if (suggestions.Length == 0)
        {
            var all = StringListCombiner.EnglishOr().Combine(fromString.Keys.Select(SimpleQuote).ToArray());
            return (null, $"Invalid value {SimpleQuote(name)}! You need to select either {all}");
        }
        else
        {
            var mean = StringListCombiner.EnglishOr().Combine(suggestions);
            return (null, $"Invalid value {SimpleQuote(name)}! Perhaps you meant {mean}?");
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

        Debug.Assert(ret.toString.Count > 0, "Missing entries");

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

    public EnumStringAttribute(string primaryName, params string[] otherNames)
    {
        PrimaryName = primaryName;
        OtherNames = otherNames;
    }
}

internal class EnumTypeConverter<T> : TypeConverter
    where T : struct
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
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

    public override bool CanConvertTo(ITypeDescriptorContext? context, [NotNullWhen(true)] Type? destinationType)
    {
        return base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        return destinationType == typeof(string) && value != null && value is T
            ? ReflectedValues<T>.Converter.EnumToString((T)value)
            : base.ConvertTo(context, culture, value, destinationType);
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

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
