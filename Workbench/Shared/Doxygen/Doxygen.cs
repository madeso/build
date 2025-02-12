using System.Collections;
using System.Collections.Immutable;
using System.Xml.Serialization;
using Workbench.Shared.Doxygen.Compound;
using Workbench.Shared.Doxygen.Index;

namespace Workbench.Shared.Doxygen;

internal static class Doxygen
{
    public static DoxygenType ParseIndex(Dir dir)
    {
        var path = dir.GetFile("index.xml");

        var root = Xsd.Parse<Index.DoxygenType>(path.Path);
        var parsed = new DoxygenType(new CompoundLoader(dir), root);
        return parsed;
    }
}

internal static class Xsd
{
    internal static T Parse<T>(string path)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var stream = new FileStream(path, FileMode.Open);

        var ret = serializer.Deserialize(stream);
        if (ret == null) throw new Exception($"Parsed object is null reading {path}");
        return (T)ret;
    }
}

/// <summary>
/// Index type.
/// </summary>
internal class DoxygenType
{
    public CompoundType[] Compounds { get; }
    public ImmutableDictionary<string, CompoundType> refidLookup { get; }

    public DoxygenType(CompoundLoader dir, Index.DoxygenType root)
    {
        Compounds = root.compound.Select(x => new CompoundType(dir, x)).ToArray();
        refidLookup = Compounds.ToImmutableDictionary(c => c.RefId);
    }
}

internal class CompoundType(CompoundLoader dir, Index.CompoundType type)
{
    public string RefId => type.refid;
    public CompoundKind Kind => type.kind;

    public ParsedDoxygenFile DoxygenFile => dir.Load(RefId);
    public string Name => type.name;
}

internal class CompoundLoader(Dir dir)
{
    private readonly Dictionary<string, ParsedDoxygenFile> cache = new();

    public ParsedDoxygenFile Load(string id)
    {
        if (cache.TryGetValue(id, out var type))
        {
            return type;
        }

        var path = dir.GetFile($"{id}.xml");

        var root = Xsd.Parse<Workbench.Shared.Doxygen.Compound.DoxygenType>(path.Path);
        var parsed = new ParsedDoxygenFile(root);

        cache.Add(id, parsed);
        return parsed;
    }
}


internal class ParsedDoxygenFile
{
    public ParsedDoxygenFile(Compound.DoxygenType el)
    {
        ListOfDefinitions = el.compounddef.ToArray();
        if (ListOfDefinitions.Length != 1)
        {
            throw new Exception("Invalid structure");
        }
    }

    public compounddefType[] ListOfDefinitions { get; }

    public compounddefType FirstCompound
    {
        get
        {
            if (ListOfDefinitions.Length != 1)
            {
                throw new Exception("Invalid compund");
            }
            return ListOfDefinitions[0];
        }
    }
}

