﻿using System.Collections.Immutable;
using System.Xml;
using Workbench.Shared.Extensions;

namespace Workbench.Shared.Doxygen;


internal enum CompoundKind
{
    Class, Struct, Union, Interface, Protocol, Category, Exception,
    File, Namespace, Group, Page, Example, Dir, Type, Concept,
}

internal enum MemberKind
{
    Define, Property, Event, Variable, Typedef, Enum, EnumValue,
    Function, Signal, Prototype, Friend, Dcop, Slot,
}



internal class DoxygenType
{
    public CompoundType[] Compounds { get; }
    public ImmutableDictionary<string, CompoundType> refidLookup { get; }

    public DoxygenType(CompoundLoader dir, XmlElement el)
    {
        Compounds = el.ElementsNamed("compound").Select(x => new CompoundType(dir, x)).ToArray();

        refidLookup = Compounds.ToImmutableDictionary(c => c.RefId);
    }
}

class CompoundType
{
    public string Name { get; }
    public MemberType[] Members { get; }
    public string RefId { get; }
    public CompoundKind Kind { get; }

    public ParsedDoxygenFile DoxygenFile { get; }

    public override string ToString()
    {
        return $"{Kind} {Name} ({RefId})";
    }

    public CompoundType(CompoundLoader dir, XmlElement el)
    {
        Name = el.GetTextOfSubElement("name");
        RefId = el.GetAttributeString("refid");

        DoxygenFile = dir.Load(RefId);

        Kind = ParseKind(el.GetAttributeString("kind"));
        Members = el.ElementsNamed("member").Select(x => new MemberType(x)).ToArray();
    }

    private static CompoundKind ParseKind(string v)
    {
        return v switch
        {
            "class" => CompoundKind.Class,
            "struct" => CompoundKind.Struct,
            "union" => CompoundKind.Union,
            "interface" => CompoundKind.Interface,
            "protocol" => CompoundKind.Protocol,
            "category" => CompoundKind.Category,
            "exception" => CompoundKind.Exception,
            "file" => CompoundKind.File,
            "namespace" => CompoundKind.Namespace,
            "group" => CompoundKind.Group,
            "page" => CompoundKind.Page,
            "example" => CompoundKind.Example,
            "dir" => CompoundKind.Dir,
            "type" => CompoundKind.Type,
            "concept" => CompoundKind.Concept,
            _ => throw new NotImplementedException(),
        };
    }
}

class MemberType
{
    public string Name { get; }
    public string RefId { get; }
    public MemberKind Kind { get; }

    public override string ToString()
    {
        return $"{Kind} {Name} ({RefId})";
    }

    public MemberType(XmlElement el)
    {
        Name = el.GetTextOfSubElement("name");
        RefId = el.GetAttributeString("refid");
        Kind = ParseKind(el.GetAttributeString("kind"));
    }

    private static MemberKind ParseKind(string v)
    {
        return v switch
        {
            "define" => MemberKind.Define,
            "property" => MemberKind.Property,
            "event" => MemberKind.Event,
            "variable" => MemberKind.Variable,
            "typedef" => MemberKind.Typedef,
            "enum" => MemberKind.Enum,
            "enumvalue" => MemberKind.EnumValue,
            "function" => MemberKind.Function,
            "signal" => MemberKind.Signal,
            "prototype" => MemberKind.Prototype,
            "friend" => MemberKind.Friend,
            "dcop" => MemberKind.Dcop,
            "slot" => MemberKind.Slot,
            _ => throw new NotImplementedException(),
        };
    }
}