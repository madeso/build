using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Workbench;

namespace Workbench;

internal class EnumConverter<T>
    where T : Enum
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

        var suggestions = StringListCombiner.EnglishOr()
            .combine(EditDistance.ClosestMatches(3, Transform(name), fromString.Keys.Select(x => Transform(x))));
        return (default(T), $"Invalid value {name}, did you mean {suggestions}?");
    }

    private static string Transform(string name)
    {
        return name.Trim().ToLowerInvariant();
    }

    public static EnumConverter<T> ReflectValues()
    {
        var ret = new EnumConverter<T>();

        foreach(var (att, val) in Reflect.PublicStaticValuesOf<T, EnumStringAttribute>(Reflect.Atributes<EnumStringAttribute>()))
        {
            ret.Add(val, att.PrimaryName, att.OtherNames);
        }

        Debug.Assert(ret.toString.Count > 0, "Missing entries");

        return ret;
    }
}

public class EnumStringAttribute : Attribute
{
    public string PrimaryName{get;}
    public string[] OtherNames { get; }

    public EnumStringAttribute(string primaryName, params string[] otherNames)
    {
        PrimaryName = primaryName;
        OtherNames = otherNames;
    }
}


class EnumTypeConverter<T> : TypeConverter
    where T : Enum
{
    private static readonly EnumConverter<T> Data = EnumConverter<T>.ReflectValues();

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        var casted = value as string;
        if (casted == null)
        {
            return base.ConvertFrom(context, culture, value);
        }

        var (ret, error) = Data.StringToEnum(casted);
        if (ret == null || string.IsNullOrEmpty(error) == false)
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
            ? Data.EnumToString((T)value)
            : base.ConvertTo(context, culture, value, destinationType);
    }
}
