using System.Collections.Immutable;
using System.Xml;
using Workbench.Utils;

namespace Workbench.Doxygen
{
    internal static class Doxygen
    {
        public static Index.DoxygenType ParseIndex(string dir)
        {
            var path = Path.Join(dir, "index.xml");
            XmlDocument doc = new();
            doc.Load(path);
            var root = doc.ElementsNamed("doxygenindex").First();
            var parsed = new Index.DoxygenType(new Compound.CompoundLoader(dir), root);
            return parsed;
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
        public CompoundType[] compounds { get; }

        public DoxygenType(Compound.CompoundLoader dir, XmlElement el)
        {
            compounds = el.ElementsNamed("compound").Select(x => new CompoundType(dir, x)).ToArray();
        }
    }

    class CompoundType
    {
        public string name { get; }
        public MemberType[] members { get; }
        public string refid { get; }
        public CompoundKind kind { get; }

        public Compound.DoxygenType Compund { get; }

        public override string ToString()
        {
            return $"{kind} {name} ({refid})";
        }

        public CompoundType(Compound.CompoundLoader dir, XmlElement el)
        {
            name = el.GetTextOfSubElement("name");
            refid = el.GetAttributeString("refid");

            Compund = dir.Load(refid);

            kind = ParseKind(el.GetAttributeString("kind"));
            members = el.ElementsNamed("member").Select(x => new MemberType(x)).ToArray();
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

        public MemberType(XmlElement el)
        {
            name = el.GetTextOfSubElement("name");
            refid = el.GetAttributeString("refid");
            kind = ParseKind(el.GetAttributeString("kind"));
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

namespace Workbench.Doxygen.Compund
{
}
