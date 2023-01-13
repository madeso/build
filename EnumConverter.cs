using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

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
        foreach(var name in otherNames)
        {
            fromString.Add(Transform(name), value);
        }

        return this;
    }

    public string EnumToString(T value)
    {
        if(toString.TryGetValue(value, out var str))
        {
            return str;
        }

        Debug.Assert(false, "missing value");
        return $"<BUG: missing converter for {value}>";
    }

    public (T?, string) StringToEnum(string name)
    {
        if(fromString.TryGetValue(Transform(name), out var ret))
        {
            return (ret, string.Empty);
        }

        Debug.Assert(fromString.Count > 0);

        var suggestions = StringListCombiner.EnglishOr().combine(EditDistance.ClosestMatches(3, name, fromString.Keys));
        return (default(T), $"Invalid value {name}, did you mean {suggestions}?");
    }

    private static string Transform(string name)
    {
        return name.Trim().ToLowerInvariant();
    }
}

// todo(Gustav): add attribute types
// todo(Gustav): add code to grab a converter from reflecting a enum



class EnumTypeConverter<T> : TypeConverter
    where T : Enum
{
    internal EnumConverter<T> Data { get; set; } = new EnumConverter<T>();

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        Console.WriteLine("can convert from");
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        Console.WriteLine($"Convert from: {value}");
        var casted = value as string;
        if(casted == null)
        {
            Console.WriteLine("Value is null");
            return base.ConvertFrom(context, culture, value);
        }

        Console.WriteLine("parsing...");
        var (ret, error) = Data.StringToEnum(casted);
        if(ret == null || string.IsNullOrEmpty(error) == false)
        {
            Console.WriteLine($"throwing error... {error}");
            throw new NotSupportedException(error);
        }

        Console.WriteLine("parsing ok");
        return ret;
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, [NotNullWhen(true)] Type? destinationType)
    {
        Console.WriteLine("Can convert to");
        return base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        Console.WriteLine("Convert to");
        return destinationType == typeof(string) && value != null && value is T
            ? Data.EnumToString((T)value)
            : base.ConvertTo(context, culture, value, destinationType);
    }
}
