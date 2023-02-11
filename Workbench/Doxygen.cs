using System.Collections.Immutable;
using System.Xml;

namespace Workbench.Doxygen
{
    internal static class Doxygen
    {
        public static Index.DoxygenType ParseIndex(string path)
        {
            XmlDocument doc = new();
            doc.Load(path);
            var root = doc.ElementsNamed("doxygenindex").First();
            return Index.DoxygenType.Parse(root);
        }
    }
}

namespace Workbench.Doxygen.Index
{

    internal enum CompoundKind
    {
        Class, Struct, Union, Interface, Protocol, Category, Exception,
        File, Namespace, Group, Page, Example, Dir, Type, Concept,
    }

    internal enum MemberKind
    {
        Define, Property, Event, Variable, Typedef, Enum, Enumvalue,
        Function, Signal, Prototype, Friend, Dcop, Slot,
    }



    class DoxygenType
    {
        public ImmutableArray<CompoundType> compounds { get; }

        public DoxygenType(ImmutableArray<CompoundType> compounds)
        {
            this.compounds = compounds;
        }

        public static DoxygenType Parse(XmlElement el)
        {
            var compunds = el.ElementsNamed("compound").Select(CompoundType.Parse).ToImmutableArray();
            return new DoxygenType(compunds);
        }
    }

    class CompoundType
    {
        public string name { get; }
        public ImmutableArray<MemberType> members { get; }
        public string refid { get; }
        public CompoundKind kind { get; }

        public override string ToString()
        {
            return $"{kind} {name} ({refid})";
        }

        public CompoundType(string name, ImmutableArray<MemberType> members, string refid, CompoundKind kind)
        {
            this.name = name;
            this.members = members;
            this.refid = refid;
            this.kind = kind;
        }

        public static CompoundType Parse(XmlElement el)
        {
            var name = el.GetTextOfSubElement("name");
            var refid = el.GetAttributeString("refid");
            var kind = el.GetAttributeString("kind");
            var members = el.ElementsNamed("member").Select(MemberType.Parse).ToImmutableArray();
            return new CompoundType(name, members, refid, ParseKind(kind));
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
        public string name { get; }
        public string refid { get; }
        public MemberKind kind { get; }

        public override string ToString()
        {
            return $"{kind} {name} ({refid})";
        }

        public MemberType(string name, string refid, MemberKind kind)
        {
            this.name = name;
            this.refid = refid;
            this.kind = kind;
        }

        public static MemberType Parse(XmlElement el)
        {
            var name = el.GetTextOfSubElement("name");
            var refid = el.GetAttributeString("refid");
            var kind = el.GetAttributeString("kind");
            return new MemberType(name, refid, ParseKind(kind));
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
                "enumvalue" => MemberKind.Enumvalue,
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

}
