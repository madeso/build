using Spectre.Console;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System;
using System.Xml;
using Workbench.Commands.CodeCity;
using Workbench.Commands.Hero;
using Workbench.Shared.Extensions;
using static Workbench.Shared.Doxygen.DescriptionType;

namespace Workbench.Shared.Doxygen;

// work in progress doxygen xml compound parser


class CompoundLoader
{
    readonly Dir dir;
    readonly Dictionary<string, ParsedDoxygenFile> cache = new();

    public CompoundLoader(Dir dir)
    {
        this.dir = dir;
    }

    public ParsedDoxygenFile Load(string id)
    {
        if (cache.TryGetValue(id, out var type))
        {
            return type;
        }

        var path = dir.GetFile($"{id}.xml");
        XmlDocument doc = new();
        doc.Load(path.Path);
        var root = doc.ElementsNamed("doxygen").First();
        var parsed = new ParsedDoxygenFile(root);

        cache.Add(id, parsed);
        return parsed;
    }
}


class ParsedDoxygenFile
{
    public ParsedDoxygenFile(XmlElement el)
    {
        ListOfDefinitions = el.ElementsNamed("compounddef").Select(x => new CompoundDef(x)).ToArray();
        if (ListOfDefinitions.Length != 1)
        {
            throw new Exception("Invalid structure");
        }
    }

    public CompoundDef[] ListOfDefinitions { get; }

    public CompoundDef FirstCompound
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

class CompoundDef
{
    public CompoundDef(XmlElement el)
    {
        CompoundName = el.GetTextOfSubElement("compoundname");
        Title = el.GetTextOfSubElementOrNull("title");

        Incdepgraph = el.GetFirstElementTypeOrNull("incdepgraph", x => new GraphType(x));
        Invincdepgraph = el.GetFirstElementTypeOrNull("invincdepgraph", x => new GraphType(x));
        Templateparamlist = el.GetFirstElementTypeOrNull("templateparamlist", x => new TemplateParamListType(x));
        // Tableofcontents = el.GetFirstElementTypeOrNull("tableofcontents", x=>new tableofcontentsType(x));
        Requiresclause = el.GetFirstElementTypeOrNull("requiresclause", x => new LinkedTextType(x));
        Initializer = el.GetFirstElementTypeOrNull("initializer", x => new LinkedTextType(x));
        Briefdescription = el.GetFirstElementTypeOrNull("briefdescription", x => new DescriptionType(x));
        Detaileddescription = el.GetFirstElementTypeOrNull("detaileddescription", x => new DescriptionType(x));
        Inheritancegraph = el.GetFirstElementTypeOrNull("inheritancegraph", x => new GraphType(x));
        Collaborationgraph = el.GetFirstElementTypeOrNull("collaborationgraph", x => new GraphType(x));
        Programlisting = el.GetFirstElementTypeOrNull("programlisting", x => new ListingType(x));
        Location = el.GetFirstElementTypeOrNull("location", x => new LocationType(x));
        Listofallmembers = el.GetFirstElementTypeOrNull("listofallmembers", x => new ListOfAllMembersType(x));

        BaseCompoundRefs = el.ElementsNamed("basecompoundref").Select(x => new CompoundRefType(x)).ToArray();
        DerivedCompoundRefs = el.ElementsNamed("derivedcompoundref").Select(x => new CompoundRefType(x)).ToArray();
        Includes = el.ElementsNamed("includes").Select(x => new IncType(x)).ToArray();
        IncludedBy = el.ElementsNamed("includedby").Select(x => new IncType(x)).ToArray();
        InnerDirs = el.ElementsNamed("innerdir").Select(x => new RefType(x)).ToArray();
        InnerFiles = el.ElementsNamed("innerfile").Select(x => new RefType(x)).ToArray();
        InnerClasses = el.ElementsNamed("innerclass").Select(x => new RefType(x)).ToArray();
        InnerConcepts = el.ElementsNamed("innerconcept").Select(x => new RefType(x)).ToArray();
        InnerNamespaces = el.ElementsNamed("innernamespace").Select(x => new RefType(x)).ToArray();
        InnerPages = el.ElementsNamed("innerpage").Select(x => new RefType(x)).ToArray();
        InnerGroups = el.ElementsNamed("innergroup").Select(x => new RefType(x)).ToArray();
        SectionDefs = el.ElementsNamed("sectiondef").Select(x => new SectiondefType(x)).ToArray();

        Id = el.GetAttributeString("id");
        Kind = el.GetAttributeEnum<DoxCompoundKind>("kind");
        Prot = el.GetAttributeEnumOrNull<DoxProtectionKind>("prot");
        Language = el.GetAttributeEnumOrNull<DoxLanguage>("language");
        Final = el.GetAttributeEnumOrNull<DoxBool>("final");
        Inline = el.GetAttributeEnumOrNull<DoxBool>("inline");
        Sealed = el.GetAttributeEnumOrNull<DoxBool>("sealed");
        Abstract = el.GetAttributeEnumOrNull<DoxBool>("abstract");
    }

    // elements
    public string CompoundName { get; }
    public string? Title { get; }
    public GraphType? Incdepgraph { get; }
    public GraphType? Invincdepgraph { get; }
    public TemplateParamListType? Templateparamlist { get; }
    // public tableofcontentsType? Tableofcontents {get;}
    public LinkedTextType? Requiresclause { get; }
    public LinkedTextType? Initializer { get; }
    public DescriptionType? Briefdescription { get; }
    public DescriptionType? Detaileddescription { get; }
    public GraphType? Inheritancegraph { get; }
    public GraphType? Collaborationgraph { get; }
    public ListingType? Programlisting { get; }
    public LocationType? Location { get; }
    public ListOfAllMembersType? Listofallmembers { get; }
    public CompoundRefType[] BaseCompoundRefs { get; }
    public CompoundRefType[] DerivedCompoundRefs { get; }
    public IncType[] Includes { get; }
    public IncType[] IncludedBy { get; }
    public RefType[] InnerDirs { get; }
    public RefType[] InnerFiles { get; }
    public RefType[] InnerClasses { get; }
    public RefType[] InnerConcepts { get; }
    public RefType[] InnerNamespaces { get; }
    public RefType[] InnerPages { get; }
    public RefType[] InnerGroups { get; }
    public SectiondefType[] SectionDefs { get; }

    // <xsd:element name="qualifier" minOccurs="0" maxOccurs="unbounded" />

    // attributes
    public string Id { get; }
    public DoxCompoundKind Kind { get; }
    public DoxProtectionKind? Prot { get; }
    public DoxLanguage? Language { get; }
    public DoxBool? Final { get; }
    public DoxBool? Inline { get; }
    public DoxBool? Sealed { get; }
    public DoxBool? Abstract { get; }
}

class ListOfAllMembersType
{
    public ListOfAllMembersType(XmlElement el)
    {
        Member = el.ElementsNamed("member").Select(x => new MemberRefType(x)).ToArray();
    }

    // nodes
    public MemberRefType[] Member { get; }
}

class MemberRefType
{
    public MemberRefType(XmlElement el)
    {
        Scope = el.GetTextOfSubElement("scope");
        Name = el.GetTextOfSubElement("name");

        RefId = el.GetAttributeString("refid");
        Protection = el.GetAttributeEnum<DoxProtectionKind>("prot");
        Virtual = el.GetAttributeEnum<DoxVirtualKind>("virt");
        AmbiguityScope = el.GetAttributeStringOrNull("ambiguityscope") ?? string.Empty;
    }

    // nodes
    public string Scope { get; }
    public string Name { get; }

    // attributes
    public string RefId { get; }
    public DoxProtectionKind Protection { get; }
    public DoxVirtualKind Virtual { get; }
    public string AmbiguityScope { get; }
}

class DocHtmlOnlyType
{
    public DocHtmlOnlyType(XmlElement el)
    {
        // Extension = el.GetFirstText();
        throw new NotImplementedException();
    }

    // public string Extension { get; }

    // attributes
    // string block { get; }
}

class CompoundRefType
{
    public CompoundRefType(XmlElement el)
    {
        Extension = el.GetFirstText();

        RefId = el.GetAttributeStringOrNull("refid");
        Protection = el.GetAttributeEnum<DoxProtectionKind>("prot");
        Virtual = el.GetAttributeEnum<DoxVirtualKind>("virt");
    }

    public string Extension { get; }

    // attributes
    public string? RefId { get; }
    public DoxProtectionKind Protection { get; }
    public DoxVirtualKind Virtual { get; }
}

class ReimplementType
{
    public ReimplementType(XmlElement el)
    {
        Extension = el.GetFirstText();
        RefId = el.GetAttributeString("refid");
    }

    public string Extension { get; }

    // attributes
    public string RefId { get; }
}

class IncType
{
    public IncType(XmlElement el)
    {
        Extension = el.GetFirstText();

        RefId = el.GetAttributeStringOrNull("refid");
        IsLocal = el.GetAttributeEnum<DoxBool>("local");
    }

    public string Extension { get; }

    // attributes
    public string? RefId { get; }
    public DoxBool IsLocal { get; }
}

class RefType
{
    public RefType(XmlElement el)
    {
        Extension = el.GetFirstText();

        RefId = el.GetAttributeString("refid");
        Protection = el.GetAttributeEnumOrNull<DoxProtectionKind>("prot");
        IsInline = el.GetAttributeEnumOrNull<DoxBool>("inline");
    }

    public string Extension { get; }

    // attributes
    public string RefId { get; }
    public DoxProtectionKind? Protection { get; }
    public DoxBool? IsInline { get; }
}

class RefTextType
{
    public RefTextType(XmlElement el)
    {
        Extension = el.GetFirstText();

        RefId = el.GetAttributeString("refid");
        KindRef = el.GetAttributeEnum<DoxRefKind>("kindref");
        External = el.GetAttributeStringOrNull("external");
        Tooltip = el.GetAttributeStringOrNull("tooltip");
    }

    public string Extension { get; }

    // attributes
    public string RefId { get; }
    public DoxRefKind KindRef { get; }
    public string? External { get; }
    public string? Tooltip { get; }
}

class SectiondefType
{
    public SectiondefType(XmlElement el)
    {
        Header = el.GetTextOfSubElementOrNull("header");
        Description = el.GetFirstElementTypeOrNull("description", x => new DescriptionType(x));
        MemberDef = el.ElementsNamed("memberdef").Select(x => new MemberDefinitionType(x)).ToArray();
        Kind = el.GetAttributeEnum<DoxSectionKind>("kind");
    }

    // elements
    public string? Header { get; }
    public DescriptionType? Description { get; }
    public MemberDefinitionType[] MemberDef { get; }

    // attributes
    public DoxSectionKind Kind { get; }
}

class MemberDefinitionType
{
    public MemberDefinitionType(XmlElement el)
    {
        Name = el.GetTextOfSubElement("name");

        Definition = el.GetTextOfSubElementOrNull("definition");
        ArgsString = el.GetTextOfSubElementOrNull("argsstring");
        QualifiedName = el.GetTextOfSubElementOrNull("qualifiedname");
        Read = el.GetTextOfSubElementOrNull("read");
        Write = el.GetTextOfSubElementOrNull("write");
        Bitfield = el.GetTextOfSubElementOrNull("bitfield");

        Qualifier = el.ElementsNamed("qualifier").Select(x => x.GetSmartText()).ToArray();

        //

        Location = el.GetFirstElementTypeOrNull("location", x => new LocationType(x));

        TemplateParamList = el.GetFirstElementTypeOrNull("templateparamlist", x => new TemplateParamListType(x));
        Type = el.GetFirstElementTypeOrNull("type", x => new LinkedTextType(x));
        RequiresClause = el.GetFirstElementTypeOrNull("requiresclause", x => new LinkedTextType(x));
        Initializer = el.GetFirstElementTypeOrNull("initializer", x => new LinkedTextType(x));
        Exceptions = el.GetFirstElementTypeOrNull("exceptions", x => new LinkedTextType(x));
        BriefDescription = el.GetFirstElementTypeOrNull("briefdescription", x => new DescriptionType(x));
        DetailedDescription = el.GetFirstElementTypeOrNull("detaileddescription", x => new DescriptionType(x));
        InBodyDescription = el.GetFirstElementTypeOrNull("inbodydescription", x => new DescriptionType(x));

        Reimplements = el.ElementsNamed("reimplements").Select(x => new ReimplementType(x)).ToArray();
        ReimplementedBy = el.ElementsNamed("reimplementedby").Select(x => new ReimplementType(x)).ToArray();
        Param = el.ElementsNamed("param").Select(x => new ParamType(x)).ToArray();
        EnumValues = el.ElementsNamed("enumvalue").Select(x => new EnumValueType(x)).ToArray();
        References = el.ElementsNamed("references").Select(x => new ReferenceType(x)).ToArray();
        ReferencedBy = el.ElementsNamed("referencedby").Select(x => new ReferenceType(x)).ToArray();

        // attributes
        Kind = el.GetAttributeEnum<DoxMemberKind>("kind");
        Id = el.GetAttributeString("id");
        Protection = el.GetAttributeEnum<DoxProtectionKind>("prot");
        IsStatic = el.GetAttributeEnum<DoxBool>("static");

        IsStrong = el.GetAttributeEnumOrNull<DoxBool>("strong");
        IsConst = el.GetAttributeEnumOrNull<DoxBool>("const");
        IsExplicit = el.GetAttributeEnumOrNull<DoxBool>("explicit");
        IsInline = el.GetAttributeEnumOrNull<DoxBool>("inline");
        RefQualifier = el.GetAttributeEnumOrNull<DoxRefQualifierKind>("refqual");
        Virtual = el.GetAttributeEnumOrNull<DoxVirtualKind>("virt");
        IsVolatile = el.GetAttributeEnumOrNull<DoxBool>("volatile");
        IsMutable = el.GetAttributeEnumOrNull<DoxBool>("mutable");
        IsNoexcept = el.GetAttributeEnumOrNull<DoxBool>("noexcept");
        IsConstexpr = el.GetAttributeEnumOrNull<DoxBool>("constexpr");
        Readable = el.GetAttributeEnumOrNull<DoxBool>("readable");
        Writable = el.GetAttributeEnumOrNull<DoxBool>("writable");
        IsInitonly = el.GetAttributeEnumOrNull<DoxBool>("initonly");
        IsSettable = el.GetAttributeEnumOrNull<DoxBool>("settable");
        IsPrivateSettable = el.GetAttributeEnumOrNull<DoxBool>("privatesettable");
        IsProtectedSettable = el.GetAttributeEnumOrNull<DoxBool>("protectedsettable");
        IsGetTable = el.GetAttributeEnumOrNull<DoxBool>("gettable");
        IsPrivateGetTable = el.GetAttributeEnumOrNull<DoxBool>("privategettable");
        IsProtectedGetTable = el.GetAttributeEnumOrNull<DoxBool>("protectedgettable");
        IsFinal = el.GetAttributeEnumOrNull<DoxBool>("final");
        IsSealed = el.GetAttributeEnumOrNull<DoxBool>("sealed");
        IsNew = el.GetAttributeEnumOrNull<DoxBool>("new");
        IsAdd = el.GetAttributeEnumOrNull<DoxBool>("add");
        IsRemove = el.GetAttributeEnumOrNull<DoxBool>("remove");
        IsRaise = el.GetAttributeEnumOrNull<DoxBool>("raise");
        IsOptional = el.GetAttributeEnumOrNull<DoxBool>("optional");
        IsRequired = el.GetAttributeEnumOrNull<DoxBool>("required");
        Accessor = el.GetAttributeEnumOrNull<DoxAccessor>("accessor");
        IsAttribute = el.GetAttributeEnumOrNull<DoxBool>("attribute");
        IsProperty = el.GetAttributeEnumOrNull<DoxBool>("property");
        IsReadonly = el.GetAttributeEnumOrNull<DoxBool>("readonly");
        IsBound = el.GetAttributeEnumOrNull<DoxBool>("bound");
        IsRemovable = el.GetAttributeEnumOrNull<DoxBool>("removable");
        IsConstrained = el.GetAttributeEnumOrNull<DoxBool>("constrained");
        IsTransient = el.GetAttributeEnumOrNull<DoxBool>("transient");
        IsMaybeVoid = el.GetAttributeEnumOrNull<DoxBool>("maybevoid");
        IsMaybeDefault = el.GetAttributeEnumOrNull<DoxBool>("maybedefault");
        IsMaybeAmbiguous = el.GetAttributeEnumOrNull<DoxBool>("maybeambiguous");
    }

    // elements
    // missing types.. string?
    public string Name { get; }

    public string? Definition { get; }
    public string? ArgsString { get; }
    public string? QualifiedName { get; }
    public string? Read { get; }
    public string? Write { get; }
    public string? Bitfield { get; }

    public string[] Qualifier { get; }

    //

    public LocationType? Location { get; }

    public TemplateParamListType? TemplateParamList { get; }
    public LinkedTextType? Type { get; }
    public LinkedTextType? RequiresClause { get; }
    public LinkedTextType? Initializer { get; }
    public LinkedTextType? Exceptions { get; }
    public DescriptionType? BriefDescription { get; }
    public DescriptionType? DetailedDescription { get; }
    public DescriptionType? InBodyDescription { get; }

    public ReimplementType[] Reimplements { get; }
    public ReimplementType[] ReimplementedBy { get; }
    public ParamType[] Param { get; }
    public EnumValueType[] EnumValues { get; }
    public ReferenceType[] References { get; }
    public ReferenceType[] ReferencedBy { get; }

    // attributes
    public DoxMemberKind Kind { get; }
    public string Id { get; }
    public DoxProtectionKind Protection { get; }
    public DoxBool IsStatic { get; }

    public DoxBool? IsStrong { get; }
    public DoxBool? IsConst { get; }
    public DoxBool? IsExplicit { get; }
    public DoxBool? IsInline { get; }
    public DoxRefQualifierKind? RefQualifier { get; }
    public DoxVirtualKind? Virtual { get; }
    public DoxBool? IsVolatile { get; }
    public DoxBool? IsMutable { get; }
    public DoxBool? IsNoexcept { get; }
    public DoxBool? IsConstexpr { get; }


    // Qt property -->
    public DoxBool? Readable { get; }
    public DoxBool? Writable { get; }

    // C++/CLI variable -->
    public DoxBool? IsInitonly { get; }

    // C++/CLI and C# property -->
    public DoxBool? IsSettable { get; }
    public DoxBool? IsPrivateSettable { get; }
    public DoxBool? IsProtectedSettable { get; }
    public DoxBool? IsGetTable { get; }
    public DoxBool? IsPrivateGetTable { get; }
    public DoxBool? IsProtectedGetTable { get; }

    // C++/CLI function -->
    public DoxBool? IsFinal { get; }
    public DoxBool? IsSealed { get; }
    public DoxBool? IsNew { get; }

    // C++/CLI event -->
    public DoxBool? IsAdd { get; }
    public DoxBool? IsRemove { get; }
    public DoxBool? IsRaise { get; }

    // Objective-C 2.0 protocol method -->
    public DoxBool? IsOptional { get; }
    public DoxBool? IsRequired { get; }

    // Objective-C 2.0 property accessor -->
    public DoxAccessor? Accessor { get; }

    // UNO IDL -->
    public DoxBool? IsAttribute { get; }
    public DoxBool? IsProperty { get; }
    public DoxBool? IsReadonly { get; }
    public DoxBool? IsBound { get; }
    public DoxBool? IsRemovable { get; }
    public DoxBool? IsConstrained { get; }
    public DoxBool? IsTransient { get; }
    public DoxBool? IsMaybeVoid { get; }
    public DoxBool? IsMaybeDefault { get; }
    public DoxBool? IsMaybeAmbiguous { get; }
}

class DescriptionType
{
    public DescriptionType(XmlElement el)
    {
        Title = el.GetTextOfSubElementOrNull("title");
        Nodes = el.MapChildren<Node>(x => new Text(x),
            x => x.Name switch
            {
                "para" => new Para(x),
                "internal" => new Internal(x),
                "sect1" => new Sect1(x),
                "sect2" => new Sect2(x),
                _ => throw new Exception("invalid type")
            }).ToArray();
    }

    public Node[] Nodes { get; }

    public interface Node
    {
    }

    public class Text : Node
    {
        public Text(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }

    public class Para : Node
    {
        public Para(XmlElement x)
        {
            Value = new docParaType(x);
        }
        public docParaType Value {get;}
    };
    public class Internal : Node
    {
        public Internal(XmlElement x)
        {
            // Value = new docInternalType(x);
        }
        // public docInternalType Value {get;}
    };
    public class Sect1 : Node
    {
        public Sect1(XmlElement x)
        {
            // Value = new docSect1Type(x);
        }
        // public docSect1Type Value {get;}
    };
    public class Sect2 : Node
    {
        public Sect2(XmlElement x)
        {
            // Value = new docSect2Type(x);
        }
        // public docSect2Type Value {get;}
    };

    // elements
    public string? Title { get; }
}

class EnumValueType
{
    public EnumValueType(XmlElement el)
    {
        Name = el.GetTextOfSubElement("name");

        Initializer = el.GetFirstElementTypeOrNull("initializer", x => new LinkedTextType(x));
        BriefDescription = el.GetFirstElementTypeOrNull("briefdescription", x => new DescriptionType(x));
        DetailedDescription = el.GetFirstElementTypeOrNull("detaileddescription", x => new DescriptionType(x));

        Id = el.GetAttributeString("id");
        Protection = el.GetAttributeEnum<DoxProtectionKind>("prot");
    }

    // elements
    public string Name { get; }

    // mixed?
    public LinkedTextType? Initializer { get; }
    public DescriptionType? BriefDescription { get; }
    public DescriptionType? DetailedDescription { get; }

    // attributes
    public string Id { get; }
    public DoxProtectionKind Protection { get; }
}

class TemplateParamListType
{
    public TemplateParamListType(XmlElement el)
    {
        Params = el.ElementsNamed("param").Select(x => new ParamType(x)).ToArray();
    }

    // elements
    public ParamType[] Params { get; }
}


class ParamType
{
    public ParamType(XmlElement el)
    {
        Attributes = el.GetTextOfSubElementOrNull("attributes");
        DeclName = el.GetTextOfSubElementOrNull("declname");
        DefName = el.GetTextOfSubElementOrNull("defname");
        Array = el.GetTextOfSubElementOrNull("array");

        Type = el.GetFirstElementTypeOrNull("type", x => new LinkedTextType(x));
        DefaultValue = el.GetFirstElementTypeOrNull("defval", x => new LinkedTextType(x));
        TypeConstraint = el.GetFirstElementTypeOrNull("typeconstraint", x => new LinkedTextType(x));
        BriefDescription = el.GetFirstElementTypeOrNull("briefdescription", x => new DescriptionType(x));
    }

    // elements

    // missing type... string?
    public string? Attributes { get; }
    public string? DeclName { get; }
    public string? DefName { get; }
    public string? Array { get; }

    public LinkedTextType? Type { get; }
    public LinkedTextType? DefaultValue { get; }
    public LinkedTextType? TypeConstraint { get; }
    public DescriptionType? BriefDescription { get; }
}

class LinkedTextType
{
    public LinkedTextType(XmlElement el)
    {
        Nodes = el.MapChildren<Node>(
            x => new Text(x),
            x => x.Name switch { "ref" => new Ref(x), _ => throw new Exception("invalid type") }
            ).ToArray();
    }

    public Node[] Nodes { get; }

    public override string ToString()
    {
        return string.Join("", Nodes.Select(x => x.ToString()));
    }

    public interface Node
    {
    }

    public class Text : Node
    {
        public string Value { get; }

        public Text(string value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public class Ref : Node
    {
        public RefTextType Value { get; }

        public Ref(XmlElement value)
        {
            Value = new RefTextType(value);
        }

        public override string ToString()
        {
            return Value.Extension;
        }
    }
}

class GraphType
{
    public GraphType(XmlElement el)
    {
        Nodes = el.ElementsNamed("node").Select(x => new NodeType(x)).ToArray();
    }

    // elements
    public NodeType[] Nodes { get; }
}

class NodeType
{
    public NodeType(XmlElement el)
    {
        Id = el.GetAttribute("id");
        Label = el.GetTextOfSubElement("label");
        Link = el.GetFirstElementTypeOrNull("link", x => new LinkType(x));
        ChildNodes = el.ElementsNamed("childnode").Select(x => new ChildNodeType(x)).ToArray();
    }


    public string Label { get; } // unspecified type
    public LinkType? Link { get; }

    public ChildNodeType[] ChildNodes { get; }

    // attributes
    public string Id { get; }
}


class ChildNodeType
{
    public ChildNodeType(XmlElement el)
    {
        // Edgelabel = el.ElementsNamed("edgelabel").Select(x => x.GetFirstText()).ToArray();
        RefId = el.GetAttributeString("refid");
        Relation = el.GetAttributeEnum<DoxGraphRelation>("relation");
    }

    // elements
    // string[] Edgelabel { get; } // unspecified type

    // attributes
    public string RefId { get; }
    public DoxGraphRelation Relation { get; }
}


class LinkType
{
    public LinkType(XmlElement el)
    {
        RefId = el.GetAttributeString("refid");
        External = el.GetAttributeStringOrNull("external");
    }

    // attributes
    public string RefId { get; }
    public string? External { get; }
}




class ListingType
{
    public ListingType(XmlElement el)
    {
        // throw new NotImplementedException();
    }

    // elements
    // codelineType[] codeline;

    // attributes
    // public string? filename { get; }
}

class CodelineType
{
    public CodelineType(XmlElement el)
    {
        throw new NotImplementedException();
    }

    // elements
    // highlightType[] highlight;

    // attributes
    // public int lineno { get; }
    // public string refid { get; }
    // public DoxRefKind refkind { get; }
    // public DoxBool external { get; }
}

class HighlightType
{
    public HighlightType(XmlElement el)
    {
        throw new NotImplementedException();
    }

    // mixed="true
    // <xsd:choice minOccurs="0" maxOccurs="unbounded">
    //   <xsd:element name="sp" type="spType" />
    //   <xsd:element name="ref" type="refTextType" />
    // </xsd:choice>

    // attributes
    // public DoxHighlightClass Class { get; } // class
}

class SpType // mixed
{
    public SpType(XmlElement el)
    {
        throw new NotImplementedException();
    }

    // attributes
    // public int? value { get; }
}

class ReferenceType// mixed class
{
    public ReferenceType(XmlElement el)
    {
        RefId = el.GetAttributeString("refid");
        CompoundReference = el.GetAttributeStringOrNull("compoundref");
        StartLine = el.GetAttributeIntOrNull("startline");
        EndLine = el.GetAttributeIntOrNull("endline");
        Name = el.InnerText;
    }

    // attributes
    public string RefId { get; }
    public string? CompoundReference { get; }
    public int? StartLine { get; }
    public int? EndLine { get; }

    public string Name { get; }
}

class LocationType
{
    public LocationType(XmlElement el)
    {
        File = el.GetAttributeString("file");
        Line = el.GetAttributeIntOrNull("line");


        Column = el.GetAttributeIntOrNull("column");
        DeclarationFile = el.GetAttributeStringOrNull("declfile");
        DeclarationLine = el.GetAttributeIntOrNull("declline");
        DeclarationColumn = el.GetAttributeIntOrNull("declcolumn");


        BodyFile = el.GetAttributeStringOrNull("bodyfile");
        BodyStart = el.GetAttributeIntOrNull("bodystart");
        BodyEnd = el.GetAttributeIntOrNull("bodyend");
    }

    // attributes
    public string File { get; }
    public int? Line { get; }
    public int? Column { get; }

    public string? DeclarationFile { get; }
    public int? DeclarationLine { get; }
    public int? DeclarationColumn { get; }

    public string? BodyFile { get; }
    public int? BodyStart { get; }
    public int? BodyEnd { get; }
}

#if false

  class docSect1Type// mixed class
  {
    <xsd:sequence>
      <xsd:element name="title" type="string" minOccurs="0" />
      <xsd:choice maxOccurs="unbounded">
        <xsd:element name="para" type="docParaType" minOccurs="0" maxOccurs="unbounded" />
        <xsd:element name="internal" type="docInternalS1Type" minOccurs="0"  maxOccurs="unbounded" />
        <xsd:element name="sect2" type="docSect2Type" minOccurs="0" maxOccurs="unbounded" />
      </xsd:choice>
    </xsd:sequence>

    // attributes
    public string id {get;}
  }

  class docSect2Type// mixed class
  {
    <xsd:sequence>
      <xsd:element name="title" type="string" />
      <xsd:choice maxOccurs="unbounded">
        <xsd:element name="para" type="docParaType" minOccurs="0" maxOccurs="unbounded" />
        <xsd:element name="sect3" type="docSect3Type" minOccurs="0" maxOccurs="unbounded" />
        <xsd:element name="internal" type="docInternalS2Type" minOccurs="0" />
      </xsd:choice>
    </xsd:sequence>

    // attributes
    public string id {get;}
  }

  class docSect3Type// mixed class
  {
    <xsd:sequence>
      <xsd:element name="title" type="string" />
      <xsd:choice maxOccurs="unbounded">
        <xsd:element name="para" type="docParaType" minOccurs="0" maxOccurs="unbounded" />
        <xsd:element name="sect4" type="docSect4Type" minOccurs="0" maxOccurs="unbounded" />
        <xsd:element name="internal" type="docInternalS3Type" minOccurs="0" />
      </xsd:choice>
    </xsd:sequence>

    // attributes
    public string id {get;}
  }

  class docSect4Type// mixed class
  {
    <xsd:sequence>
      <xsd:element name="title" type="string" />
      <xsd:choice maxOccurs="unbounded">
        <xsd:element name="para" type="docParaType" minOccurs="0" maxOccurs="unbounded" />
        <xsd:element name="internal" type="docInternalS4Type" minOccurs="0" />
      </xsd:choice>
    </xsd:sequence>

    // attributes
    public string id {get;}
  }

  class docInternalType// mixed class
  {
    <xsd:sequence>
      <xsd:element name="para"  type="docParaType"  minOccurs="0" maxOccurs="unbounded" />
      <xsd:element name="sect1" type="docSect1Type" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>
  }

  class docInternalS1Type// mixed class
  {
    <xsd:sequence>
      <xsd:element name="para"  type="docParaType"  minOccurs="0" maxOccurs="unbounded" />
      <xsd:element name="sect2" type="docSect2Type" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>
  }

  class docInternalS2Type// mixed class
  {
    <xsd:sequence>
      <xsd:element name="para"  type="docParaType"  minOccurs="0" maxOccurs="unbounded" />
      <xsd:element name="sect3" type="docSect3Type" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>
  }

  class docInternalS3Type// mixed class
  {
    <xsd:sequence>
      <xsd:element name="para"  type="docParaType"  minOccurs="0" maxOccurs="unbounded" />
      <xsd:element name="sect3" type="docSect4Type" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>
  }

  class docInternalS4Type// mixed class
  {
    <xsd:sequence>
      <xsd:element name="para"  type="docParaType"  minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>
  }
 
  interface docTitleCmdGroup
  {
    public static docTitleCmdGroup Parse(XmlElement el)
    {
        return el.Name switch
        {
            "ulink" => docURLLink.Parse(el),
            "htmlonly" => docHtmlOnlyType.Parse(el),
            "manonly" => string.Parse(el),
            "xmlonly" => string.Parse(el),
            "rtfonly" => string.Parse(el),
            "latexonly" => string.Parse(el),
            "docbookonly" => string.Parse(el),
            "image" => docImageType.Parse(el),
            "dot" => docDotMscType.Parse(el),
            "msc" => docDotMscType.Parse(el),
            "plantuml" => docPlantumlType.Parse(el),
            "anchor" => docAnchorType.Parse(el),
            "formula" => docFormulaType.Parse(el),
            "ref" => docRefTextType.Parse(el),
            "emoji" => docEmojiType.Parse(el),
            
    };
      }
    }

  
  
  class docTitleType// mixed class
  {
    <xsd:group ref="docTitleCmdGroup" minOccurs="0" maxOccurs="unbounded" />
  }

  class docSummaryType// mixed class
  {
    <xsd:group ref="docTitleCmdGroup" minOccurs="0" maxOccurs="unbounded" />
  }

  <xsd:group name="docCmdGroup">
    <xsd:choice>
      <xsd:group ref="docTitleCmdGroup"/>
      <xsd:element name="hruler" type="docEmptyType" />
      <xsd:element name="preformatted" type="docMarkupType" />
      <xsd:element name="programlisting" type="listingType" />
      <xsd:element name="verbatim" type="string" />
      <xsd:element name="javadocliteral" type="string" />
      <xsd:element name="javadoccode" type="string" />
      <xsd:element name="indexentry" type="docIndexEntryType" />
      <xsd:element name="orderedlist" type="docListType" />
      <xsd:element name="itemizedlist" type="docListType" />
      <xsd:element name="simplesect" type="docSimpleSectType" />
      <xsd:element name="title" type="docTitleType" />
      <xsd:element name="variablelist" type="docVariableListType" />
      <xsd:element name="table" type="docTableType" />
      <xsd:element name="heading" type="docHeadingType" />
      <xsd:element name="dotfile" type="docImageFileType" />
      <xsd:element name="mscfile" type="docImageFileType" />
      <xsd:element name="diafile" type="docImageFileType" />
      <xsd:element name="toclist" type="docTocListType" />
      <xsd:element name="language" type="docLanguageType" />
      <xsd:element name="parameterlist" type="docParamListType" />
      <xsd:element name="xrefsect" type="docXRefSectType" />
      <xsd:element name="copydoc" type="docCopyType" />
      <xsd:element name="details" type="docDetailsType" />
      <xsd:element name="blockquote" type="docBlockQuoteType" />
      <xsd:element name="parblock" type="docParBlockType" />
    </xsd:choice>
  </xsd:group>

#endif
  public class docParaType// mixed class
  {
      public List<docCmdGroup> Values { get; init; }= new();

      public docParaType(XmlElement x)
      {
          foreach (var e in x.ChildNodes)
          {
              Values.Add(docCmdGroupUtil.Create((XmlNode)e));
          }
      }
    // <xsd:group ref="docCmdGroup" minOccurs="0" maxOccurs="unbounded" />
  }

public interface docCmdGroup
{
}

public record docCmdGroupText(string Value) : docCmdGroup;

public record docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum Type) : docCmdGroup
{
    public static docCmdGroupEmptyType? Parse(string name)
        => name switch
        {
            "linebreak" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.linebreak),
            "nonbreakablespace" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.nonbreakablespace),
            "iexcl" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.iexcl),
            "cent" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.cent),
            "pound" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.pound),
            "curren" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.curren),
            "yen" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.yen),
            "brvbar" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.brvbar),
            "sect" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sect),
            "umlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.umlaut),
            "copy" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.copy),
            "ordf" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ordf),
            "laquo" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.laquo),
            "not" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.not),
            "shy" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.shy),
            "registered" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.registered),
            "macr" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.macr),
            "deg" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.deg),
            "plusmn" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.plusmn),
            "sup2" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sup2),
            "sup3" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sup3),
            "acute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.acute),
            "micro" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.micro),
            "para" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.para),
            "middot" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.middot),
            "cedil" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.cedil),
            "sup1" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sup1),
            "ordm" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ordm),
            "raquo" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.raquo),
            "frac14" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.frac14),
            "frac12" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.frac12),
            "frac34" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.frac34),
            "iquest" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.iquest),
            "Agrave" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Agrave),
            "Aacute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Aacute),
            "Acirc" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Acirc),
            "Atilde" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Atilde),
            "Aumlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Aumlaut),
            "Aring" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Aring),
            "AElig" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.AElig),
            "Ccedil" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Ccedil),
            "Egrave" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Egrave),
            "Eacute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Eacute),
            "Ecirc" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Ecirc),
            "Eumlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Eumlaut),
            "Igrave" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Igrave),
            "Iacute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Iacute),
            "Icirc" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Icirc),
            "Iumlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Iumlaut),
            "ETH" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ETH),
            "Ntilde" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Ntilde),
            "Ograve" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Ograve),
            "Oacute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Oacute),
            "Ocirc" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Ocirc),
            "Otilde" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Otilde),
            "Oumlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Oumlaut),
            "times" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.times),
            "Oslash" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Oslash),
            "Ugrave" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Ugrave),
            "Uacute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Uacute),
            "Ucirc" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Ucirc),
            "Uumlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Uumlaut),
            "Yacute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Yacute),
            "THORN" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.THORN),
            "szlig" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.szlig),
            "agrave" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.agrave),
            "aacute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.aacute),
            "acirc" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.acirc),
            "atilde" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.atilde),
            "aumlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.aumlaut),
            "aring" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.aring),
            "aelig" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.aelig),
            "ccedil" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ccedil),
            "egrave" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.egrave),
            "eacute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.eacute),
            "ecirc" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ecirc),
            "eumlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.eumlaut),
            "igrave" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.igrave),
            "iacute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.iacute),
            "icirc" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.icirc),
            "iumlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.iumlaut),
            "eth" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.eth),
            "ntilde" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ntilde),
            "ograve" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ograve),
            "oacute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.oacute),
            "ocirc" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ocirc),
            "otilde" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.otilde),
            "oumlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.oumlaut),
            "divide" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.divide),
            "oslash" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.oslash),
            "ugrave" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ugrave),
            "uacute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.uacute),
            "ucirc" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ucirc),
            "uumlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.uumlaut),
            "yacute" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.yacute),
            "thorn" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.thorn),
            "yumlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.yumlaut),
            "fnof" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.fnof),
            "Alpha" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Alpha),
            "Beta" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Beta),
            "Gamma" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Gamma),
            "Delta" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Delta),
            "Epsilon" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Epsilon),
            "Zeta" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Zeta),
            "Eta" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Eta),
            "Theta" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Theta),
            "Iota" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Iota),
            "Kappa" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Kappa),
            "Lambda" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Lambda),
            "Mu" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Mu),
            "Nu" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Nu),
            "Xi" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Xi),
            "Omicron" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Omicron),
            "Pi" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Pi),
            "Rho" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Rho),
            "Sigma" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Sigma),
            "Tau" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Tau),
            "Upsilon" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Upsilon),
            "Phi" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Phi),
            "Chi" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Chi),
            "Psi" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Psi),
            "Omega" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Omega),
            "alpha" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.alpha),
            "beta" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.beta),
            "gamma" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.gamma),
            "delta" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.delta),
            "epsilon" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.epsilon),
            "zeta" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.zeta),
            "eta" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.eta),
            "theta" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.theta),
            "iota" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.iota),
            "kappa" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.kappa),
            "lambda" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.lambda),
            "mu" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.mu),
            "nu" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.nu),
            "xi" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.xi),
            "omicron" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.omicron),
            "pi" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.pi),
            "rho" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.rho),
            "sigmaf" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sigmaf),
            "sigma" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sigma),
            "tau" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.tau),
            "upsilon" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.upsilon),
            "phi" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.phi),
            "chi" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.chi),
            "psi" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.psi),
            "omega" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.omega),
            "thetasym" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.thetasym),
            "upsih" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.upsih),
            "piv" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.piv),
            "bull" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.bull),
            "hellip" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.hellip),
            "prime" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.prime),
            "Prime" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Prime),
            "oline" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.oline),
            "frasl" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.frasl),
            "weierp" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.weierp),
            "imaginary" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.imaginary),
            "real" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.real),
            "trademark" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.trademark),
            "alefsym" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.alefsym),
            "larr" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.larr),
            "uarr" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.uarr),
            "rarr" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.rarr),
            "darr" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.darr),
            "harr" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.harr),
            "crarr" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.crarr),
            "lArr" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.lArr),
            "uArr" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.uArr),
            "rArr" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.rArr),
            "dArr" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.dArr),
            "hArr" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.hArr),
            "forall" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.forall),
            "part" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.part),
            "exist" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.exist),
            "empty" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.empty),
            "nabla" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.nabla),
            "isin" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.isin),
            "notin" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.notin),
            "ni" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ni),
            "prod" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.prod),
            "sum" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sum),
            "minus" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.minus),
            "lowast" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.lowast),
            "radic" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.radic),
            "prop" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.prop),
            "infin" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.infin),
            "ang" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ang),
            "and" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.and),
            "or" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.or),
            "cap" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.cap),
            "cup" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.cup),
            "int" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.int_),
            "there4" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.there4),
            "sim" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sim),
            "cong" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.cong),
            "asymp" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.asymp),
            "ne" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ne),
            "equiv" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.equiv),
            "le" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.le),
            "ge" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ge),
            "sub" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sub),
            "sup" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sup),
            "nsub" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.nsub),
            "sube" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sube),
            "supe" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.supe),
            "oplus" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.oplus),
            "otimes" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.otimes),
            "perp" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.perp),
            "sdot" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sdot),
            "lceil" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.lceil),
            "rceil" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.rceil),
            "lfloor" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.lfloor),
            "rfloor" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.rfloor),
            "lang" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.lang),
            "rang" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.rang),
            "loz" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.loz),
            "spades" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.spades),
            "clubs" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.clubs),
            "hearts" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.hearts),
            "diams" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.diams),
            "OElig" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.OElig),
            "oelig" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.oelig),
            "Scaron" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Scaron),
            "scaron" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.scaron),
            "Yumlaut" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Yumlaut),
            "circ" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.circ),
            "tilde" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.tilde),
            "ensp" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ensp),
            "emsp" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.emsp),
            "thinsp" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.thinsp),
            "zwnj" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.zwnj),
            "zwj" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.zwj),
            "lrm" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.lrm),
            "rlm" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.rlm),
            "ndash" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ndash),
            "mdash" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.mdash),
            "lsquo" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.lsquo),
            "rsquo" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.rsquo),
            "sbquo" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.sbquo),
            "ldquo" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.ldquo),
            "rdquo" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.rdquo),
            "bdquo" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.bdquo),
            "dagger" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.dagger),
            "Dagger" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.Dagger),
            "permil" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.permil),
            "lsaquo" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.lsaquo),
            "rsaquo" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.rsaquo),
            "euro" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.euro),
            "tm" => new docCmdGroupEmptyType(docCmdGroupEmptyTypeEnum.tm),
            _ => null
        };
}

public enum docCmdGroupEmptyTypeEnum
{
linebreak,
nonbreakablespace,
iexcl,
cent,
pound,
curren,
yen,
brvbar,
sect,
umlaut,
copy,
ordf,
laquo,
not,
shy,
registered,
macr,
deg,
plusmn,
sup2,
sup3,
acute,
micro,
para,
middot,
cedil,
sup1,
ordm,
raquo,
frac14,
frac12,
frac34,
iquest,
Agrave,
Aacute,
Acirc,
Atilde,
Aumlaut,
Aring,
AElig,
Ccedil,
Egrave,
Eacute,
Ecirc,
Eumlaut,
Igrave,
Iacute,
Icirc,
Iumlaut,
ETH,
Ntilde,
Ograve,
Oacute,
Ocirc,
Otilde,
Oumlaut,
times,
Oslash,
Ugrave,
Uacute,
Ucirc,
Uumlaut,
Yacute,
THORN,
szlig,
agrave,
aacute,
acirc,
atilde,
aumlaut,
aring,
aelig,
ccedil,
egrave,
eacute,
ecirc,
eumlaut,
igrave,
iacute,
icirc,
iumlaut,
eth,
ntilde,
ograve,
oacute,
ocirc,
otilde,
oumlaut,
divide,
oslash,
ugrave,
uacute,
ucirc,
uumlaut,
yacute,
thorn,
yumlaut,
fnof,
Alpha,
Beta,
Gamma,
Delta,
Epsilon,
Zeta,
Eta,
Theta,
Iota,
Kappa,
Lambda,
Mu,
Nu,
Xi,
Omicron,
Pi,
Rho,
Sigma,
Tau,
Upsilon,
Phi,
Chi,
Psi,
Omega,
alpha,
beta,
gamma,
delta,
epsilon,
zeta,
eta,
theta,
iota,
kappa,
lambda,
mu,
nu,
xi,
omicron,
pi,
rho,
sigmaf,
sigma,
tau,
upsilon,
phi,
chi,
psi,
omega,
thetasym,
upsih,
piv,
bull,
hellip,
prime,
Prime,
oline,
frasl,
weierp,
imaginary,
real,
trademark,
alefsym,
larr,
uarr,
rarr,
darr,
harr,
crarr,
lArr,
uArr,
rArr,
dArr,
hArr,
forall,
part,
exist,
empty,
nabla,
isin,
notin,
ni,
prod,
sum,
minus,
lowast,
radic,
prop,
infin,
ang,
and,
or,
cap,
cup,
int_,
there4,
sim,
cong,
asymp,
ne,
equiv,
le,
ge,
sub,
sup,
nsub,
sube,
supe,
oplus,
otimes,
perp,
sdot,
lceil,
rceil,
lfloor,
rfloor,
lang,
rang,
loz,
spades,
clubs,
hearts,
diams,
OElig,
oelig,
Scaron,
scaron,
Yumlaut,
circ,
tilde,
ensp,
emsp,
thinsp,
zwnj,
zwj,
lrm,
rlm,
ndash,
mdash,
lsquo,
rsquo,
sbquo,
ldquo,
rdquo,
bdquo,
dagger,
Dagger,
permil,
lsaquo,
rsaquo,
euro,
tm,
}

public record UnhandledNode(string Name) : docCmdGroup {}

public class docCmdGroupProgramListing : docCmdGroup
{
    public string? Language { get; }
    public List<string> Lines { get; } = new();
    public docCmdGroupProgramListing(XmlElement root)
    {
        var lang = root.GetAttributeStringOrNull("filename");
        if (lang is { Length: > 0 })
        {
            lang = lang[1..];
        }
        this.Language = lang;
        foreach (var xline in root.Elements())
        {
            if (xline.Name != "codeline") throw new NotImplementedException("not a codeline");
            var line = string.Empty;
            foreach (var code in xline.Elements())
            {
                if (code.Name != ("highlight")) throw new NotImplementedException("not a highlight");
                foreach (var x in code.ChildNodes)
                {
                    if (x is XmlText t)
                    {
                        line += t.Value ?? string.Empty;
                    }

                    if (x is XmlElement el)
                    {
                        switch (el.Name)
                        {
                            case "sp":
                                line += " ";
                                break;
                            case "ref":
                                line += el.InnerText; break;
                            default:
                                throw new NotImplementedException($"{el.Name} is unsupported code");
                        }
                    }
                }
            }
            Lines.Add(line);
        }
    }
}

public static class docCmdGroupUtil
{
    public static docCmdGroup Create(XmlNode x)
    {
        if (x is XmlText t)
        {
            return new docCmdGroupText(t.Value ?? string.Empty);
        }

        if (x is XmlElement el)
        {
            var empty = docCmdGroupEmptyType.Parse(el.Name);
            if (empty != null)
            {
                return empty;
            }

            if (el.Name == "programlisting")
            {
                return new docCmdGroupProgramListing(el);
            }

            switch (el.Name)
            {
                case "ulink":
                case "parameterlist":
                case "simplesect":
                case "itemizedlist":
                case "ref":
                case "verbatim":
                case "image":
                case "anchor":
                case "orderedlist":
                    return new UnhandledNode(el.Name);

                case "bold":
                case "s":
                case "strike":
                case "underline":
                case "emphasis":
                case "computeroutput":
                case "subscript":
                case "superscript":
                case "center":
                case "small":
                case "cite":
                case "del":
                case "ins":
                    // docMarkupType
                    return new UnhandledNode(el.Name);
            }

            throw new NotImplementedException($"{el.Name} is unhandled");
        }
        throw new NotImplementedException("node type is unhandled");
    }
}

#if false
  class docMarkupType// mixed class
  {
    <xsd:group ref="docCmdGroup" minOccurs="0" maxOccurs="unbounded" />
  }

  class docURLLink// mixed class
  {
    <xsd:group ref="docTitleCmdGroup" minOccurs="0" maxOccurs="unbounded" />

    // attributes
    public string url {get;}
  }

  class docAnchorType// mixed class
  {
    // attributes
    public string id {get;}
  }

  class docFormulaType// mixed class
  {
    // attributes
    public string id {get;}
  }

  class docIndexEntryType
  {
    <xsd:sequence>
      <xsd:element name="primaryie" type="string" />
      <xsd:element name="secondaryie" type="string" />
    </xsd:sequence>
  }

  class docListType
  {
    <xsd:sequence>
      <xsd:element name="listitem" type="docListItemType" maxOccurs="unbounded" />
    </xsd:sequence>

    // attributes
    public DoxOlType type {get;}
    public int start {get;}
  }

  class docListItemType
  {
    <xsd:sequence>
      <xsd:element name="para" type="docParaType" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>
    public int? value {get;}
  }

  class docSimpleSectType
  {
    <xsd:sequence>
      <xsd:element name="title" type="docTitleType" minOccurs="0" />
      <xsd:sequence minOccurs="0" maxOccurs="unbounded">
        <xsd:element name="para" type="docParaType" minOccurs="1" maxOccurs="unbounded" />
      </xsd:sequence>
    </xsd:sequence>

    // attributes
    public DoxSimpleSectKind kind {get;}
  }

  class docVarListEntryType
  {
    <xsd:sequence>
      <xsd:element name="term" type="docTitleType" />
    </xsd:sequence>
  }

  <xsd:group name="docVariableListGroup">
    <xsd:sequence>
      <xsd:element name="varlistentry" type="docVarListEntryType" />
      <xsd:element name="listitem" type="docListItemType" />
    </xsd:sequence>
  </xsd:group>

  class docVariableListType
  {
    <xsd:sequence>
      <xsd:group ref="docVariableListGroup" maxOccurs="unbounded" />
    </xsd:sequence>
  }

  class docRefTextType// mixed class
  {
    <xsd:group ref="docTitleCmdGroup" minOccurs="0" maxOccurs="unbounded" />

    // attributes
    public string refid {get;}
    public DoxRefKind kindref {get;}
    public string external {get;}
  }

  class docTableType
  {
    <xsd:sequence>
      <xsd:element name="caption" type="docCaptionType" minOccurs="0" maxOccurs="1" />
      <xsd:element name="row" type="docRowType" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>

    // attributes
    public int rows {get;}
    public int cols {get;}
    public string width {get;}
  }

  class docRowType
  {
    <xsd:sequence>
      <xsd:element name="entry" type="docEntryType" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>
  }

  class docEntryType
  {
    <xsd:sequence>
      <xsd:element name="para" type="docParaType" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>

    // attributes
    public DoxBool thead {get;}
    public int colspan {get;}
    public int rowspan {get;}
    public DoxAlign align {get;}
    public DoxVerticalAlign valign {get;}
    public string width {get;}
    public string class {get;}
    <xsd:anyAttribute processContents="skip"/>
  }

  class docCaptionType// mixed class
  {
    <xsd:group ref="docTitleCmdGroup" minOccurs="0" maxOccurs="unbounded" />

    // attributes
    public string id {get;} 
  }

  class docHeadingType// mixed class
  {
    <xsd:group ref="docTitleCmdGroup" minOccurs="0" maxOccurs="unbounded" />

    // attributes
    public int level {get;} // range 1-6
  }

  class docImageType// mixed class
  {
    <xsd:group ref="docTitleCmdGroup" minOccurs="0" maxOccurs="unbounded" />
    public DoxImageKind? type {get;}
    public string? name {get;}
    public string? width {get;}
    public string? height {get;}
    public string? alt {get;}
    public DoxBool? inline {get;}
    public string? caption {get;}
  }

  class docDotMscType// mixed class
  {
    <xsd:group ref="docTitleCmdGroup" minOccurs="0" maxOccurs="unbounded" />
    public string? name {get;}
    public string? width {get;}
    public string? height {get;}
    public string? caption {get;}
  }

  class docImageFileType// mixed class
  {
    <xsd:group ref="docTitleCmdGroup" minOccurs="0" maxOccurs="unbounded" />
    public string? name {get;} // The mentioned file will be located in the directory as specified by XML_OUTPUT
    public string? width {get;}
    public string? height {get;}
  }

  class docPlantumlType// mixed class
  {
    <xsd:group ref="docTitleCmdGroup" minOccurs="0" maxOccurs="unbounded" />
    public string? name {get;}
    public string? width {get;}
    public string? height {get;}
    public string? caption {get;}
    public DoxPlantumlEngine? engine {get;}
  }

  class docTocItemType// mixed class
  {
    <xsd:group ref="docTitleCmdGroup" minOccurs="0" maxOccurs="unbounded" />

    // attributes
    public string id {get;} 
  }

  class docTocListType
  {
    <xsd:sequence>
      <xsd:element name="tocitem" type="docTocItemType" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>
  }

  class docLanguageType
  {
    <xsd:sequence>
      <xsd:element name="para" type="docParaType" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>

    // attributes
    public string langid {get;} 
  }

  class docParamListType
  {
    <xsd:sequence>
      <xsd:element name="parameteritem" type="docParamListItem" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>

    // attributes
    public DoxParamListKind kind {get;} 
  }

  class docParamListItem
  {
    <xsd:sequence>
      <xsd:element name="parameternamelist" type="docParamNameList" minOccurs="0" maxOccurs="unbounded" />
      <xsd:element name="parameterdescription" type="descriptionType" />
    </xsd:sequence>
  }

  class docParamNameList
  {
    <xsd:sequence>
      <xsd:element name="parametertype" type="docParamType" minOccurs="0" maxOccurs="unbounded" />
      <xsd:element name="parametername" type="docParamName" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>
  }

  class docParamType// mixed class
  {
    <xsd:sequence>
      <xsd:element name="ref" type="refTextType" minOccurs="0" maxOccurs="1" />
    </xsd:sequence>
  }

  class docParamName// mixed class
  {
    <xsd:sequence>
      <xsd:element name="ref" type="refTextType" minOccurs="0" maxOccurs="1" />
    </xsd:sequence>
    public DoxParamDir? direction {get;}
  }

  class docXRefSectType
  {
    <xsd:sequence>
      <xsd:element name="xreftitle" type="string" minOccurs="0" maxOccurs="unbounded" />
      <xsd:element name="xrefdescription" type="descriptionType" />
    </xsd:sequence>

    // attributes
    public string id {get;} 
  }

  class docCopyType
  {
    <xsd:sequence>
      <xsd:element name="para" type="docParaType" minOccurs="0" maxOccurs="unbounded" />
      <xsd:element name="sect1" type="docSect1Type" minOccurs="0" maxOccurs="unbounded" />
      <xsd:element name="internal" type="docInternalType" minOccurs="0" />
    </xsd:sequence>

    // attributes
    public string link {get;} 
  }

  class docDetailsType
  {
    <xsd:sequence>
      <xsd:element name="summary" type="docSummaryType" minOccurs="0" maxOccurs="1" />
      <xsd:element name="para" type="docParaType" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>
  }

  class docBlockQuoteType
  {
    <xsd:sequence>
      <xsd:element name="para" type="docParaType" minOccurs="0" maxOccurs="unbounded" />
    </xsd:sequence>
  }

  class docParBlockType
  {
    public docParaType[] para {get;}
  }

  class docEmptyType
  {
  }

  class tableofcontentsType
  {
    // elements
     tableofcontentsKindType[] tocsect {get;}
  }

  class tableofcontentsKindType
  {
    // elements
    public string name {get;}
    public string reference {get;}
    tableofcontentsType[] tableofcontents;
  }

  class docEmojiType
  {
    // attributes
    public string name {get;}
    public string unicode {get;}
  }

// Simple types
#endif


internal enum DoxBool
{
    [EnumString("yes")]
    Yes,

    [EnumString("no")]
    No,
}



enum DoxGraphRelation
{
    [EnumString("include")]
    Include,

    [EnumString("usage")]
    Usage,

    [EnumString("template-instance")]
    TemplateInstance,

    [EnumString("public-inheritance")]
    PublicInheritance,

    [EnumString("protected-inheritance")]
    ProtectedInheritance,

    [EnumString("private-inheritance")]
    PrivateInheritance,

    [EnumString("type-constraint")]
    TypeConstraint,
}

enum DoxRefKind
{
    [EnumString("compound")]
    Compound,

    [EnumString("member")]
    Member,
}

enum DoxMemberKind
{
    [EnumString("define")]
    Define,

    [EnumString("property")]
    Property,

    [EnumString("event")]
    Event,

    [EnumString("variable")]
    Variable,

    [EnumString("typedef")]
    Typedef,

    [EnumString("enum")]
    Enum,

    [EnumString("function")]
    Function,

    [EnumString("signal")]
    Signal,

    [EnumString("prototype")]
    Prototype,

    [EnumString("friend")]
    Friend,

    [EnumString("dcop")]
    Dcop,

    [EnumString("slot")]
    Slot,

    [EnumString("interface")]
    Interface,

    [EnumString("service")]
    Service,
}

enum DoxProtectionKind
{
    [EnumString("public")]
    Public,

    [EnumString("protected")]
    Protected,

    [EnumString("private")]
    Private,

    [EnumString("package")]
    Package,
}

enum DoxRefQualifierKind
{
    [EnumString("lvalue")]
    Lvalue,

    [EnumString("rvalue")]
    Rvalue,
}

enum DoxLanguage
{
    [EnumString("Unknown")]
    Unknown,

    [EnumString("IDL")]
    Idl,

    [EnumString("Java")]
    Java,

    [EnumString("C#")]
    Csharp,

    [EnumString("D")]
    D,

    [EnumString("PHP")]
    Php,

    [EnumString("Objective-C")]
    ObjectiveC,

    [EnumString("C++")]
    Cpp,

    [EnumString("JavaScript")]
    JavaScript,

    [EnumString("Python")]
    Python,

    [EnumString("Fortran")]
    Fortran,

    [EnumString("VHDL")]
    VHDL,

    [EnumString("XML")]
    Xml,

    [EnumString("SQL")]
    Sql,

    [EnumString("Markdown")]
    Markdown,

    [EnumString("Slice")]
    Slice,

    [EnumString("Lex")]
    Lex,

}

enum DoxVirtualKind
{
    [EnumString("non-virtual")]
    NonVirtual,

    [EnumString("virtual")]
    Virtual,

    [EnumString("pure-virtual")]
    PureVirtual,
}

enum DoxCompoundKind
{
    [EnumString("class")]
    Class,

    [EnumString("struct")]
    Struct,

    [EnumString("union")]
    Union,

    [EnumString("interface")]
    Interface,

    [EnumString("protocol")]
    Protocol,

    [EnumString("category")]
    Category,

    [EnumString("exception")]
    Exception,

    [EnumString("service")]
    Service,

    [EnumString("singleton")]
    Singleton,

    [EnumString("module")]
    Module,

    [EnumString("type")]
    Type,

    [EnumString("file")]
    File,

    [EnumString("namespace")]
    Namespace,

    [EnumString("group")]
    Group,

    [EnumString("page")]
    Page,

    [EnumString("example")]
    Example,

    [EnumString("dir")]
    Dir,

    [EnumString("concept")]
    Concept,
}

enum DoxSectionKind
{
    [EnumString("user-defined")]
    UserDefined,

    [EnumString("public-type")]
    PublicType,

    [EnumString("public-func")]
    PublicFunc,

    [EnumString("public-attrib")]
    PublicAttrib,

    [EnumString("public-slot")]
    PublicSlot,

    [EnumString("signal")]
    Signal,

    [EnumString("dcop-func")]
    DcopFunc,

    [EnumString("property")]
    Property,

    [EnumString("event")]
    Event,

    [EnumString("public-static-func")]
    PublicStaticFunc,

    [EnumString("public-static-attrib")]
    PublicStaticAttrib,

    [EnumString("protected-type")]
    ProtectedType,

    [EnumString("protected-func")]
    ProtectedFunc,

    [EnumString("protected-attrib")]
    ProtectedAttrib,

    [EnumString("protected-slot")]
    ProtectedSlot,

    [EnumString("protected-static-func")]
    ProtectedStaticFunc,

    [EnumString("protected-static-attrib")]
    ProtectedStaticAttrib,

    [EnumString("package-type")]
    PackageType,

    [EnumString("package-func")]
    PackageFunc,

    [EnumString("package-attrib")]
    PackageAttrib,

    [EnumString("package-static-func")]
    PackageStaticFunc,

    [EnumString("package-static-attrib")]
    PackageStaticAttrib,

    [EnumString("private-type")]
    PrivateType,

    [EnumString("private-func")]
    PrivateFunc,

    [EnumString("private-attrib")]
    PrivateAttrib,

    [EnumString("private-slot")]
    PrivateSlot,

    [EnumString("private-static-func")]
    PrivateStaticFunc,

    [EnumString("private-static-attrib")]
    PrivateStaticAttrib,

    [EnumString("friend")]
    Friend,

    [EnumString("related")]
    Related,

    [EnumString("define")]
    Define,

    [EnumString("prototype")]
    Prototype,

    [EnumString("typedef")]
    Typedef,

    [EnumString("enum")]
    Enum,

    [EnumString("func")]
    Func,

    [EnumString("var")]
    Var,
}

enum DoxHighlightClass
{
    [EnumString("comment")]
    Comment,

    [EnumString("normal")]
    Normal,

    [EnumString("preprocessor")]
    Preprocessor,

    [EnumString("keyword")]
    Keyword,

    [EnumString("keywordtype")]
    Keywordtype,

    [EnumString("keywordflow")]
    Keywordflow,

    [EnumString("stringliteral")]
    Stringliteral,

    [EnumString("charliteral")]
    Charliteral,

    [EnumString("vhdlkeyword")]
    Vhdlkeyword,

    [EnumString("vhdllogic")]
    Vhdllogic,

    [EnumString("vhdlchar")]
    Vhdlchar,

    [EnumString("vhdldigit")]
    Vhdldigit,
}

enum DoxSimpleSectKind
{
    [EnumString("see")]
    See,

    [EnumString("return")]
    Return,

    [EnumString("author")]
    Author,

    [EnumString("authors")]
    Authors,

    [EnumString("version")]
    Version,

    [EnumString("since")]
    Since,

    [EnumString("date")]
    Date,

    [EnumString("note")]
    Note,

    [EnumString("warning")]
    Warning,

    [EnumString("pre")]
    Pre,

    [EnumString("post")]
    Post,

    [EnumString("copyright")]
    Copyright,

    [EnumString("invariant")]
    Invariant,

    [EnumString("remark")]
    Remark,

    [EnumString("attention")]
    Attention,

    [EnumString("par")]
    Par,

    [EnumString("rcs")]
    Rcs,
}

//   <xsd:simpleType name="DoxVersionNumber">
//     <xsd:restriction base="string">
//       <xsd:pattern value="\d+\.\d+.*" />
//     </xsd:restriction>
//   </xsd:simpleType>

enum DoxImageKind
{
    [EnumString("html")]
    Html,

    [EnumString("latex")]
    Latex,

    [EnumString("docbook")]
    Docbook,

    [EnumString("rtf")]
    Rtf,

    [EnumString("xml")]
    Xml,
}

enum DoxPlantumlEngine
{
    [EnumString("uml")]
    Uml,

    [EnumString("bpm")]
    Bpm,

    [EnumString("wire")]
    Wire,

    [EnumString("dot")]
    Dot,

    [EnumString("ditaa")]
    Ditaa,

    [EnumString("salt")]
    Salt,

    [EnumString("math")]
    Math,

    [EnumString("latex")]
    Latex,

    [EnumString("gantt")]
    Gantt,

    [EnumString("mindmap")]
    Mindmap,

    [EnumString("wbs")]
    Wbs,

    [EnumString("yaml")]
    Yaml,

    [EnumString("creole")]
    Creole,

    [EnumString("json")]
    Json,

    [EnumString("flow")]
    Flow,

    [EnumString("board")]
    Board,

    [EnumString("git")]
    Git,
}

enum DoxParamListKind
{
    [EnumString("param")]
    Param,

    [EnumString("retval")]
    Retval,

    [EnumString("exception")]
    Exception,

    [EnumString("templateparam")]
    Templateparam,
}

// enum DoxCharRange
// {
//       <xsd:pattern value="[aeiouncAEIOUNC]" />
// }

enum DoxParamDir
{
    [EnumString("in")]
    In,

    [EnumString("out")]
    Out,

    [EnumString("inout")]
    Inout,
}

enum DoxAccessor
{
    [EnumString("retain")]
    Retain,

    [EnumString("copy")]
    Copy,

    [EnumString("assign")]
    Assign,

    [EnumString("weak")]
    Weak,

    [EnumString("strong")]
    Strong,

    [EnumString("unretained")]
    Unretained,
}

enum DoxAlign
{
    [EnumString("left")]
    Left,

    [EnumString("right")]
    Right,

    [EnumString("center")]
    Center,
}

enum DoxVerticalAlign
{
    [EnumString("bottom")]
    Bottom,

    [EnumString("top")]
    Top,

    [EnumString("middle")]
    Middle,
}

enum DoxOlType
{
    [EnumString("1")]
    One,

    [EnumString("a")]
    LowerA,

    [EnumString("A")]
    UpperA,

    [EnumString("i")]
    LowerI,

    [EnumString("I")]
    UpperI,
}
