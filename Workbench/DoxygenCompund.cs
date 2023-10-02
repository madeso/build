using System.Xml;
using Workbench.Utils;

namespace Workbench.Doxygen.Compound;

// work in progress doxygen xml compound parser


class CompoundLoader
{
    string dir;

    public CompoundLoader(string dir)
    {
        this.dir = dir;
    }

    Dictionary<string, DoxygenType> cache = new();

    public DoxygenType Load(string id)
    {
        if(cache.TryGetValue(id, out var type))
        {
            return type;
        }

        var path = Path.Join(dir, id + ".xml");
        XmlDocument doc = new();
        doc.Load(path);
        var root = doc.ElementsNamed("doxygen").First();
        var parsed = new DoxygenType(root);

        cache.Add(id, parsed);
        return parsed;
    }
}


class DoxygenType
{
    public DoxygenType(XmlElement el)
    {
        ListOfDefinitions = el.ElementsNamed("compounddef").Select(x => new compounddefType(x)).ToArray();
    }

    public compounddefType[] ListOfDefinitions {get;}

    public compounddefType Compound
    {
        get
        {
            if(ListOfDefinitions.Length != 1)
            {
                throw new Exception("Invalid compund");
            }
            return ListOfDefinitions[0];
        }
    }
}

  class compounddefType
  {
    public compounddefType(XmlElement el)
    {
        Compoundname = el.GetTextOfSubElement("compoundname");
        Title = el.GetTextOfSubElementOrNull("title");
        
        Incdepgraph = el.GetFirstElementTypeOrNull("incdepgraph", x=>new graphType(x));
        Invincdepgraph = el.GetFirstElementTypeOrNull("invincdepgraph", x=>new graphType(x));
        Templateparamlist = el.GetFirstElementTypeOrNull("templateparamlist", x=>new templateparamlistType(x));
        // Tableofcontents = el.GetFirstElementTypeOrNull("tableofcontents", x=>new tableofcontentsType(x));
        Requiresclause = el.GetFirstElementTypeOrNull("requiresclause", x=>new linkedTextType(x));
        Initializer = el.GetFirstElementTypeOrNull("initializer", x=>new linkedTextType(x));
        Briefdescription = el.GetFirstElementTypeOrNull("briefdescription", x=>new descriptionType(x));
        Detaileddescription = el.GetFirstElementTypeOrNull("detaileddescription", x=>new descriptionType(x));
        Inheritancegraph = el.GetFirstElementTypeOrNull("inheritancegraph", x=>new graphType(x));
        Collaborationgraph = el.GetFirstElementTypeOrNull("collaborationgraph", x=>new graphType(x));
        Programlisting = el.GetFirstElementTypeOrNull("programlisting", x=>new listingType(x));
        Location = el.GetFirstElementTypeOrNull("location", x=>new locationType(x));
        Listofallmembers = el.GetFirstElementTypeOrNull("listofallmembers", x=>new listofallmembersType(x));

        Basecompoundref = el.ElementsNamed("basecompoundref").Select(x => new compoundRefType(x)).ToArray();
        Derivedcompoundref = el.ElementsNamed("derivedcompoundref").Select(x => new compoundRefType(x)).ToArray();
        Includes = el.ElementsNamed("includes").Select(x => new incType(x)).ToArray();
        Includedby = el.ElementsNamed("includedby").Select(x => new incType(x)).ToArray();
        Innerdir = el.ElementsNamed("innerdir").Select(x => new refType(x)).ToArray();
        Innerfile = el.ElementsNamed("innerfile").Select(x => new refType(x)).ToArray();
        Innerclass = el.ElementsNamed("innerclass").Select(x => new refType(x)).ToArray();
        Innerconcept = el.ElementsNamed("innerconcept").Select(x => new refType(x)).ToArray();
        Innernamespace = el.ElementsNamed("innernamespace").Select(x => new refType(x)).ToArray();
        Innerpage = el.ElementsNamed("innerpage").Select(x => new refType(x)).ToArray();
        Innergroup = el.ElementsNamed("innergroup").Select(x => new refType(x)).ToArray();
        Sectiondef = el.ElementsNamed("sectiondef").Select(x => new sectiondefType(x)).ToArray();

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
    public string Compoundname {get;}
    public string? Title {get;}
    public graphType? Incdepgraph {get;}
    public graphType? Invincdepgraph {get;}
    public templateparamlistType? Templateparamlist {get;}
    // public tableofcontentsType? Tableofcontents {get;}
    public linkedTextType? Requiresclause {get;}
    public linkedTextType? Initializer {get;}
    public descriptionType? Briefdescription {get;}
    public descriptionType? Detaileddescription {get;}
    public graphType? Inheritancegraph {get;}
    public graphType? Collaborationgraph {get;}
    public listingType? Programlisting {get;}
    public locationType? Location {get;}
    public listofallmembersType? Listofallmembers {get;}
    public compoundRefType[] Basecompoundref {get;}
    public compoundRefType[] Derivedcompoundref {get;}
    public incType[] Includes {get;}
    public incType[] Includedby {get;}
    public refType[] Innerdir {get;}
    public refType[] Innerfile {get;}
    public refType[] Innerclass {get;}
    public refType[] Innerconcept {get;}
    public refType[] Innernamespace {get;}
    public refType[] Innerpage {get;}
    public refType[] Innergroup {get;}
    public sectiondefType[] Sectiondef {get;}

    // <xsd:element name="qualifier" minOccurs="0" maxOccurs="unbounded" />

    // attributes
    public string Id {get;}
    public DoxCompoundKind Kind {get;}
    public DoxProtectionKind? Prot {get;}
    public DoxLanguage? Language {get;}
    public DoxBool? Final {get;}
    public DoxBool? Inline {get;}
    public DoxBool? Sealed {get;}
    public DoxBool? Abstract {get;}
  }

  class listofallmembersType
  {
    public listofallmembersType(XmlElement el)
    {
        member = el.ElementsNamed("member").Select(x => new memberRefType(x)).ToArray();
    }

    // nodes
    public memberRefType[] member {get;}
  }

  class memberRefType
  {
    public memberRefType(XmlElement el)
    {
        scope = el.GetTextOfSubElement("scope");
        name = el.GetTextOfSubElement("name");
        
        refid = el.GetAttributeString("refid");
        prot = el.GetAttributeEnum<DoxProtectionKind>("prot");
        virt = el.GetAttributeEnum<DoxVirtualKind>("virt");
        ambiguityscope = el.GetAttributeStringOrNull("ambiguityscope") ?? string.Empty;
    }

    // nodes
    public string scope {get;}
    public string name {get;}
    
    // attributes
    public string refid {get;}
    public DoxProtectionKind prot {get;}
    public DoxVirtualKind virt {get;}
    public string ambiguityscope {get;}
  }

  class docHtmlOnlyType
  {
    public docHtmlOnlyType(XmlElement el)
    {
        Extension = el.GetFirstText();
        throw new NotImplementedException();
    }

    public string Extension {get;}

    // attributes
    string block { get; }
  }

  class compoundRefType
  {
    public compoundRefType(XmlElement el)
    {
        Extension = el.GetFirstText();

        refid = el.GetAttributeStringOrNull("refid");
        prot = el.GetAttributeEnum<DoxProtectionKind>("prot");
        virt = el.GetAttributeEnum<DoxVirtualKind>("virt");
}

    public string Extension {get;}

    // attributes
    public string? refid {get;}
    public DoxProtectionKind prot {get;}
    public DoxVirtualKind virt {get;}
  }

  class reimplementType
  {
    public reimplementType(XmlElement el)
    {
        Extension = el.GetFirstText();
        refid = el.GetAttributeString("refid");
    }

    public string Extension {get;}

    // attributes
    public string refid { get; }
  }

  class incType
  {
    public incType(XmlElement el)
    {
        Extension = el.GetFirstText();

        refid = el.GetAttributeStringOrNull("refid");
        local = el.GetAttributeEnum<DoxBool>("local");
}

    public string Extension {get;}

    // attributes
    public string? refid {get;}
    public DoxBool local {get;}
  }

  class refType
  {
    public refType(XmlElement el)
    {
        Extension = el.GetFirstText();

        refid = el.GetAttributeString("refid");
        prot = el.GetAttributeEnumOrNull<DoxProtectionKind>("prot");
        inline = el.GetAttributeEnumOrNull<DoxBool>("inline");
    }

    public string Extension {get;}

    // attributes
    public string refid {get;}
    public DoxProtectionKind? prot {get;}
    public DoxBool? inline {get;}
  }

  class refTextType
  {
    public refTextType(XmlElement el)
    {
        Extension = el.GetFirstText();

        refid = el.GetAttributeString("refid");
        kindref = el.GetAttributeEnum<DoxRefKind>("kindref");
        external = el.GetAttributeStringOrNull("external");
        tooltip = el.GetAttributeStringOrNull("tooltip");
    }

    public string Extension {get;}

    // attributes
    public string refid {get;}
    public DoxRefKind kindref {get;}
    public string? external {get;}
    public string? tooltip {get;}
  }

  class sectiondefType
  {
    public sectiondefType(XmlElement el)
    {
        header = el.GetTextOfSubElementOrNull("header");
        description = el.GetFirstElementTypeOrNull<descriptionType>("description", x => new descriptionType(x));
        memberdef = el.ElementsNamed("memberdef").Select(x => new memberdefType(x)).ToArray();
        kind = el.GetAttributeEnum<DoxSectionKind>("kind");
    }

    // elements
    public string? header {get;}
    public descriptionType? description {get;}
    public memberdefType[] memberdef {get;}

    // attributes
    public DoxSectionKind kind {get;}
  }

  class memberdefType
  {
    public memberdefType(XmlElement el)
    {
        Name = el.GetTextOfSubElement("name");
    
        Definition = el.GetTextOfSubElementOrNull("definition");
        Argsstring = el.GetTextOfSubElementOrNull("argsstring");
        Qualifiedname = el.GetTextOfSubElementOrNull("qualifiedname");
        Read = el.GetTextOfSubElementOrNull("read");
        Write = el.GetTextOfSubElementOrNull("write");
        Bitfield = el.GetTextOfSubElementOrNull("bitfield");

        Qualifier = el.ElementsNamed("qualifier").Select(x => x.GetSmartText()).ToArray();

        //

        Location = el.GetFirstElementTypeOrNull("location", x => new locationType(x));

        Templateparamlist = el.GetFirstElementTypeOrNull("templateparamlist", x => new templateparamlistType(x));
        Type = el.GetFirstElementTypeOrNull("type", x => new linkedTextType(x));
        Requiresclause = el.GetFirstElementTypeOrNull("requiresclause", x => new linkedTextType(x));
        Initializer = el.GetFirstElementTypeOrNull("initializer", x => new linkedTextType(x));
        Exceptions = el.GetFirstElementTypeOrNull("exceptions", x => new linkedTextType(x));
        Briefdescription = el.GetFirstElementTypeOrNull("briefdescription", x => new descriptionType(x));
        Detaileddescription = el.GetFirstElementTypeOrNull("detaileddescription", x => new descriptionType(x));
        Inbodydescription = el.GetFirstElementTypeOrNull("inbodydescription", x => new descriptionType(x));
    
        Reimplements = el.ElementsNamed("reimplements").Select(x => new reimplementType(x)).ToArray();
        Reimplementedby = el.ElementsNamed("reimplementedby").Select(x => new reimplementType(x)).ToArray();
        Param = el.ElementsNamed("param").Select(x => new paramType(x)).ToArray();
        Enumvalue = el.ElementsNamed("enumvalue").Select(x => new enumvalueType(x)).ToArray();
        References = el.ElementsNamed("references").Select(x => new referenceType(x)).ToArray();
        Referencedby = el.ElementsNamed("referencedby").Select(x => new referenceType(x)).ToArray();

        // attributes
        Kind = el.GetAttributeEnum<DoxMemberKind>("kind");
        Id = el.GetAttributeString("id");
        Prot = el.GetAttributeEnum<DoxProtectionKind>("prot");
        Static = el.GetAttributeEnum<DoxBool>("static");

        Strong = el.GetAttributeEnumOrNull<DoxBool>("strong");
        Const = el.GetAttributeEnumOrNull<DoxBool>("const");
        Explicit = el.GetAttributeEnumOrNull<DoxBool>("explicit");
        Inline = el.GetAttributeEnumOrNull<DoxBool>("inline");
        Refqual = el.GetAttributeEnumOrNull<DoxRefQualifierKind>("refqual");
        Virt = el.GetAttributeEnumOrNull<DoxVirtualKind>("virt");
        Volatile = el.GetAttributeEnumOrNull<DoxBool>("volatile");
        Mutable = el.GetAttributeEnumOrNull<DoxBool>("mutable");
        Noexcept = el.GetAttributeEnumOrNull<DoxBool>("noexcept");
        Constexpr = el.GetAttributeEnumOrNull<DoxBool>("constexpr");
        Readable = el.GetAttributeEnumOrNull<DoxBool>("readable");
        Writable = el.GetAttributeEnumOrNull<DoxBool>("writable");
        Initonly = el.GetAttributeEnumOrNull<DoxBool>("initonly");
        Settable = el.GetAttributeEnumOrNull<DoxBool>("settable");
        Privatesettable = el.GetAttributeEnumOrNull<DoxBool>("privatesettable");
        Protectedsettable = el.GetAttributeEnumOrNull<DoxBool>("protectedsettable");
        Gettable = el.GetAttributeEnumOrNull<DoxBool>("gettable");
        Privategettable = el.GetAttributeEnumOrNull<DoxBool>("privategettable");
        Protectedgettable = el.GetAttributeEnumOrNull<DoxBool>("protectedgettable");
        Final = el.GetAttributeEnumOrNull<DoxBool>("final");
        Sealed = el.GetAttributeEnumOrNull<DoxBool>("sealed");
        New = el.GetAttributeEnumOrNull<DoxBool>("new");
        Add = el.GetAttributeEnumOrNull<DoxBool>("add");
        Remove = el.GetAttributeEnumOrNull<DoxBool>("remove");
        Raise = el.GetAttributeEnumOrNull<DoxBool>("raise");
        Optional = el.GetAttributeEnumOrNull<DoxBool>("optional");
        Required = el.GetAttributeEnumOrNull<DoxBool>("required");
        Accessor = el.GetAttributeEnumOrNull<DoxAccessor>("accessor");
        Attribute = el.GetAttributeEnumOrNull<DoxBool>("attribute");
        Property = el.GetAttributeEnumOrNull<DoxBool>("property");
        Readonly = el.GetAttributeEnumOrNull<DoxBool>("readonly");
        Bound = el.GetAttributeEnumOrNull<DoxBool>("bound");
        Removable = el.GetAttributeEnumOrNull<DoxBool>("removable");
        Constrained = el.GetAttributeEnumOrNull<DoxBool>("constrained");
        Transient = el.GetAttributeEnumOrNull<DoxBool>("transient");
        Maybevoid = el.GetAttributeEnumOrNull<DoxBool>("maybevoid");
        Maybedefault = el.GetAttributeEnumOrNull<DoxBool>("maybedefault");
        Maybeambiguous = el.GetAttributeEnumOrNull<DoxBool>("maybeambiguous");
    }

    // elements
    // missing types.. string?
    public string Name {get;}
    
    public string? Definition {get;}
    public string? Argsstring {get;}
    public string? Qualifiedname {get;}
    public string? Read {get;}
    public string? Write {get;}
    public string? Bitfield {get;}

    public string[] Qualifier {get;}

    //

    public locationType? Location {get;}

    public templateparamlistType? Templateparamlist {get;}
    public linkedTextType? Type {get;}
    public linkedTextType? Requiresclause {get;}
    public linkedTextType? Initializer {get;}
    public linkedTextType? Exceptions {get;}
    public descriptionType? Briefdescription {get;}
    public descriptionType? Detaileddescription {get;}
    public descriptionType? Inbodydescription {get;}
    
    public reimplementType[] Reimplements {get;}
    public reimplementType[] Reimplementedby {get;}
    public paramType[] Param {get;}
    public enumvalueType[] Enumvalue {get;}
    public referenceType[] References {get;}
    public referenceType[] Referencedby {get;}

    // attributes
    public DoxMemberKind Kind {get;}
    public string Id {get;}
    public DoxProtectionKind Prot {get;}
    public DoxBool Static {get;}
    
    public DoxBool? Strong {get;}
    public DoxBool? Const {get;}
    public DoxBool? Explicit {get;}
    public DoxBool? Inline {get;}
    public DoxRefQualifierKind? Refqual {get;}
    public DoxVirtualKind? Virt {get;}
    public DoxBool? Volatile {get;}
    public DoxBool? Mutable {get;}
    public DoxBool? Noexcept {get;}
    public DoxBool? Constexpr {get;}


    // Qt property -->
    public DoxBool? Readable {get;}
    public DoxBool? Writable {get;}
    
    // C++/CLI variable -->
    public DoxBool? Initonly {get;}
    
    // C++/CLI and C# property -->
    public DoxBool? Settable {get;}
    public DoxBool? Privatesettable {get;}
    public DoxBool? Protectedsettable {get;}
    public DoxBool? Gettable {get;}
    public DoxBool? Privategettable {get;}
    public DoxBool? Protectedgettable {get;}
    
    // C++/CLI function -->
    public DoxBool? Final {get;}
    public DoxBool? Sealed {get;}
    public DoxBool? New {get;}
    
    // C++/CLI event -->
    public DoxBool? Add {get;}
    public DoxBool? Remove {get;}
    public DoxBool? Raise {get;}
    
    // Objective-C 2.0 protocol method -->
    public DoxBool? Optional {get;}
    public DoxBool? Required {get;}
    
    // Objective-C 2.0 property accessor -->
    public DoxAccessor? Accessor {get;}
    
    // UNO IDL -->
    public DoxBool? Attribute {get;}
    public DoxBool? Property {get;}
    public DoxBool? Readonly {get;}
    public DoxBool? Bound {get;}
    public DoxBool? Removable {get;}
    public DoxBool? Constrained {get;}
    public DoxBool? Transient {get;}
    public DoxBool? Maybevoid {get;}
    public DoxBool? Maybedefault {get;}
    public DoxBool? Maybeambiguous {get;}
  }

  class descriptionType
  {
    public descriptionType(XmlElement el)
    {
        title = el.GetTextOfSubElementOrNull("title");
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

    public Node[] Nodes {get;}

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
            // Value = new docParaType(x);
        }
        // public docParaType Value {get;}
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
    public string? title {get;}
  }

  class enumvalueType
  {
    public enumvalueType(XmlElement el)
    {
        name = el.GetTextOfSubElement("name");
        
        initializer = el.GetFirstElementTypeOrNull("initializer", x => new linkedTextType(x));
        briefdescription = el.GetFirstElementTypeOrNull("briefdescription", x => new descriptionType(x));
        detaileddescription = el.GetFirstElementTypeOrNull("detaileddescription", x => new descriptionType(x));

        id = el.GetAttributeString("id");
        prot = el.GetAttributeEnum<DoxProtectionKind>("prot");
    }

    // elements
    public string name {get;}

    // mixed?
    public linkedTextType? initializer {get;}
    public descriptionType? briefdescription {get;}
    public descriptionType? detaileddescription {get;}

    // attributes
    public string id {get;}
    public DoxProtectionKind prot {get;}
  }

  class templateparamlistType
  {
    public templateparamlistType(XmlElement el)
    {
        param = el.ElementsNamed("param").Select(x => new paramType(x)).ToArray();
    }

    // elements
    public paramType[] param {get;}
  }


class paramType
{
    public paramType(XmlElement el)
    {
        attributes = el.GetTextOfSubElementOrNull("attributes");
        declname = el.GetTextOfSubElementOrNull("declname");
        defname = el.GetTextOfSubElementOrNull("defname");
        array = el.GetTextOfSubElementOrNull("array");

        type = el.GetFirstElementTypeOrNull("type", x=> new linkedTextType(x));
        defval = el.GetFirstElementTypeOrNull("defval", x=> new linkedTextType(x));
        typeconstraint = el.GetFirstElementTypeOrNull("typeconstraint", x=> new linkedTextType(x));
        briefdescription = el.GetFirstElementTypeOrNull("briefdescription", x=> new descriptionType(x));
    }

    // elements

    // missing type... string?
    string? attributes;
    string? declname;
    string? defname;
    string? array;

    public linkedTextType? type { get; }
    public linkedTextType? defval { get; }
    public linkedTextType? typeconstraint { get; }
    public descriptionType? briefdescription { get; }
}

class linkedTextType
  {
    public linkedTextType(XmlElement el)
    {
        Nodes = el.MapChildren<Node>(
            x=> new Text(x),
            x => x.Name switch { "ref" => new Ref(x), _ => throw new Exception("invalid type")}
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
        public refTextType Value { get; }

        public Ref(XmlElement value)
        {
            Value = new refTextType(value);
        }

        public override string ToString()
        {
            return Value.Extension;
        }
    }
  }

  class graphType
  {
    public graphType(XmlElement el)
    {
        Nodes = el.ElementsNamed("node").Select(x => new nodeType(x)).ToArray();
    }

    // elements
      public nodeType[] Nodes {get;}
  }

class nodeType
{
    public nodeType(XmlElement el)
    {
        id = el.GetAttribute("id");
        label = el.GetTextOfSubElement("label");
        link = el.GetFirstElementTypeOrNull("link", x => new linkType(x));
        childnode = el.ElementsNamed("childnode").Select(x => new childnodeType(x)).ToArray();
    }


    public string label { get; } // unspecified type
    public linkType? link { get; }

    public childnodeType[] childnode { get; }

    // attributes
    public string id { get; }
}


class childnodeType
  {
    public childnodeType(XmlElement el)
    {
        edgelabel = el.ElementsNamed("edgelabel").Select(x=>x.GetFirstText()).ToArray();
        refid = el.GetAttributeString("refid");
        relation = el.GetAttributeEnum<DoxGraphRelation>("relation");
    }

    // elements
    string[] edgelabel {get;} // unspecified type

    // attributes
    public string refid {get;}
    public DoxGraphRelation relation {get;}
  }


  class linkType
  {
    public linkType(XmlElement el)
    {
        refid = el.GetAttributeString("refid");
        external = el.GetAttributeStringOrNull("external");
    }

    // attributes
    public string refid {get;}
    public string? external {get;}
  }


  

  class listingType
  {
    public listingType(XmlElement el)
    {
        // throw new NotImplementedException();
    }

    // elements
    // codelineType[] codeline;

    // attributes
    public string? filename {get;}
  }

  class codelineType
  {
    public codelineType(XmlElement el)
    {
        throw new NotImplementedException();
    }

    // elements
    // highlightType[] highlight;

    // attributes
    public int lineno {get;}
    public string refid {get;}
    public DoxRefKind refkind {get;}
    public DoxBool external {get;}
  }

  class highlightType
  {
    public highlightType(XmlElement el)
    {
        throw new NotImplementedException();
    }

    // mixed="true
    // <xsd:choice minOccurs="0" maxOccurs="unbounded">
    //   <xsd:element name="sp" type="spType" />
    //   <xsd:element name="ref" type="refTextType" />
    // </xsd:choice>

    // attributes
    public DoxHighlightClass Class {get;} // class
  }

  class spType // mixed
    {
    public spType(XmlElement el)
    {
        throw new NotImplementedException();
    }

    // attributes
    public int? value {get;}
  }

  class referenceType// mixed class
  {
    public referenceType(XmlElement el)
    {
        throw new NotImplementedException();
    }

    // attributes
    public string refid {get;}
    public string? compoundref {get;}
    public int startline {get;}
    public int endline {get;}
  }

  class locationType
  {
    public locationType(XmlElement el)
    {
        file = el.GetAttributeString("file");
        line = el.GetAttributeIntOrNull("line");


        column = el.GetAttributeIntOrNull("column");
        declfile = el.GetAttributeStringOrNull("declfile");
        declline = el.GetAttributeIntOrNull("declline");
        declcolumn = el.GetAttributeIntOrNull("declcolumn");


        bodyfile = el.GetAttributeStringOrNull("bodyfile");
        bodystart = el.GetAttributeIntOrNull("bodystart");
        bodyend = el.GetAttributeIntOrNull("bodyend");
    }

    // attributes
    public string file {get;}
    public int? line {get;}
    public int? column {get;}
    public string? declfile {get;}
    public int? declline {get;}
    public int? declcolumn {get;}
    public string? bodyfile {get;}
    public int? bodystart {get;}
    public int? bodyend {get;}
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
            "bold" => docMarkupType.Parse(el),
            "s" => docMarkupType.Parse(el),
            "strike" => docMarkupType.Parse(el),
            "underline" => docMarkupType.Parse(el),
            "emphasis" => docMarkupType.Parse(el),
            "computeroutput" => docMarkupType.Parse(el),
            "subscript" => docMarkupType.Parse(el),
            "superscript" => docMarkupType.Parse(el),
            "center" => docMarkupType.Parse(el),
            "small" => docMarkupType.Parse(el),
            "cite" => docMarkupType.Parse(el),
            "del" => docMarkupType.Parse(el),
            "ins" => docMarkupType.Parse(el),
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
            "linebreak" => docEmptyType.Parse(el),
            "nonbreakablespace" => docEmptyType.Parse(el),
            "iexcl" => docEmptyType.Parse(el),
            "cent" => docEmptyType.Parse(el),
            "pound" => docEmptyType.Parse(el),
            "curren" => docEmptyType.Parse(el),
            "yen" => docEmptyType.Parse(el),
            "brvbar" => docEmptyType.Parse(el),
            "sect" => docEmptyType.Parse(el),
            "umlaut" => docEmptyType.Parse(el),
            "copy" => docEmptyType.Parse(el),
            "ordf" => docEmptyType.Parse(el),
            "laquo" => docEmptyType.Parse(el),
            "not" => docEmptyType.Parse(el),
            "shy" => docEmptyType.Parse(el),
            "registered" => docEmptyType.Parse(el),
            "macr" => docEmptyType.Parse(el),
            "deg" => docEmptyType.Parse(el),
            "plusmn" => docEmptyType.Parse(el),
            "sup2" => docEmptyType.Parse(el),
            "sup3" => docEmptyType.Parse(el),
            "acute" => docEmptyType.Parse(el),
            "micro" => docEmptyType.Parse(el),
            "para" => docEmptyType.Parse(el),
            "middot" => docEmptyType.Parse(el),
            "cedil" => docEmptyType.Parse(el),
            "sup1" => docEmptyType.Parse(el),
            "ordm" => docEmptyType.Parse(el),
            "raquo" => docEmptyType.Parse(el),
            "frac14" => docEmptyType.Parse(el),
            "frac12" => docEmptyType.Parse(el),
            "frac34" => docEmptyType.Parse(el),
            "iquest" => docEmptyType.Parse(el),
            "Agrave" => docEmptyType.Parse(el),
            "Aacute" => docEmptyType.Parse(el),
            "Acirc" => docEmptyType.Parse(el),
            "Atilde" => docEmptyType.Parse(el),
            "Aumlaut" => docEmptyType.Parse(el),
            "Aring" => docEmptyType.Parse(el),
            "AElig" => docEmptyType.Parse(el),
            "Ccedil" => docEmptyType.Parse(el),
            "Egrave" => docEmptyType.Parse(el),
            "Eacute" => docEmptyType.Parse(el),
            "Ecirc" => docEmptyType.Parse(el),
            "Eumlaut" => docEmptyType.Parse(el),
            "Igrave" => docEmptyType.Parse(el),
            "Iacute" => docEmptyType.Parse(el),
            "Icirc" => docEmptyType.Parse(el),
            "Iumlaut" => docEmptyType.Parse(el),
            "ETH" => docEmptyType.Parse(el),
            "Ntilde" => docEmptyType.Parse(el),
            "Ograve" => docEmptyType.Parse(el),
            "Oacute" => docEmptyType.Parse(el),
            "Ocirc" => docEmptyType.Parse(el),
            "Otilde" => docEmptyType.Parse(el),
            "Oumlaut" => docEmptyType.Parse(el),
            "times" => docEmptyType.Parse(el),
            "Oslash" => docEmptyType.Parse(el),
            "Ugrave" => docEmptyType.Parse(el),
            "Uacute" => docEmptyType.Parse(el),
            "Ucirc" => docEmptyType.Parse(el),
            "Uumlaut" => docEmptyType.Parse(el),
            "Yacute" => docEmptyType.Parse(el),
            "THORN" => docEmptyType.Parse(el),
            "szlig" => docEmptyType.Parse(el),
            "agrave" => docEmptyType.Parse(el),
            "aacute" => docEmptyType.Parse(el),
            "acirc" => docEmptyType.Parse(el),
            "atilde" => docEmptyType.Parse(el),
            "aumlaut" => docEmptyType.Parse(el),
            "aring" => docEmptyType.Parse(el),
            "aelig" => docEmptyType.Parse(el),
            "ccedil" => docEmptyType.Parse(el),
            "egrave" => docEmptyType.Parse(el),
            "eacute" => docEmptyType.Parse(el),
            "ecirc" => docEmptyType.Parse(el),
            "eumlaut" => docEmptyType.Parse(el),
            "igrave" => docEmptyType.Parse(el),
            "iacute" => docEmptyType.Parse(el),
            "icirc" => docEmptyType.Parse(el),
            "iumlaut" => docEmptyType.Parse(el),
            "eth" => docEmptyType.Parse(el),
            "ntilde" => docEmptyType.Parse(el),
            "ograve" => docEmptyType.Parse(el),
            "oacute" => docEmptyType.Parse(el),
            "ocirc" => docEmptyType.Parse(el),
            "otilde" => docEmptyType.Parse(el),
            "oumlaut" => docEmptyType.Parse(el),
            "divide" => docEmptyType.Parse(el),
            "oslash" => docEmptyType.Parse(el),
            "ugrave" => docEmptyType.Parse(el),
            "uacute" => docEmptyType.Parse(el),
            "ucirc" => docEmptyType.Parse(el),
            "uumlaut" => docEmptyType.Parse(el),
            "yacute" => docEmptyType.Parse(el),
            "thorn" => docEmptyType.Parse(el),
            "yumlaut" => docEmptyType.Parse(el),
            "fnof" => docEmptyType.Parse(el),
            "Alpha" => docEmptyType.Parse(el),
            "Beta" => docEmptyType.Parse(el),
            "Gamma" => docEmptyType.Parse(el),
            "Delta" => docEmptyType.Parse(el),
            "Epsilon" => docEmptyType.Parse(el),
            "Zeta" => docEmptyType.Parse(el),
            "Eta" => docEmptyType.Parse(el),
            "Theta" => docEmptyType.Parse(el),
            "Iota" => docEmptyType.Parse(el),
            "Kappa" => docEmptyType.Parse(el),
            "Lambda" => docEmptyType.Parse(el),
            "Mu" => docEmptyType.Parse(el),
            "Nu" => docEmptyType.Parse(el),
            "Xi" => docEmptyType.Parse(el),
            "Omicron" => docEmptyType.Parse(el),
            "Pi" => docEmptyType.Parse(el),
            "Rho" => docEmptyType.Parse(el),
            "Sigma" => docEmptyType.Parse(el),
            "Tau" => docEmptyType.Parse(el),
            "Upsilon" => docEmptyType.Parse(el),
            "Phi" => docEmptyType.Parse(el),
            "Chi" => docEmptyType.Parse(el),
            "Psi" => docEmptyType.Parse(el),
            "Omega" => docEmptyType.Parse(el),
            "alpha" => docEmptyType.Parse(el),
            "beta" => docEmptyType.Parse(el),
            "gamma" => docEmptyType.Parse(el),
            "delta" => docEmptyType.Parse(el),
            "epsilon" => docEmptyType.Parse(el),
            "zeta" => docEmptyType.Parse(el),
            "eta" => docEmptyType.Parse(el),
            "theta" => docEmptyType.Parse(el),
            "iota" => docEmptyType.Parse(el),
            "kappa" => docEmptyType.Parse(el),
            "lambda" => docEmptyType.Parse(el),
            "mu" => docEmptyType.Parse(el),
            "nu" => docEmptyType.Parse(el),
            "xi" => docEmptyType.Parse(el),
            "omicron" => docEmptyType.Parse(el),
            "pi" => docEmptyType.Parse(el),
            "rho" => docEmptyType.Parse(el),
            "sigmaf" => docEmptyType.Parse(el),
            "sigma" => docEmptyType.Parse(el),
            "tau" => docEmptyType.Parse(el),
            "upsilon" => docEmptyType.Parse(el),
            "phi" => docEmptyType.Parse(el),
            "chi" => docEmptyType.Parse(el),
            "psi" => docEmptyType.Parse(el),
            "omega" => docEmptyType.Parse(el),
            "thetasym" => docEmptyType.Parse(el),
            "upsih" => docEmptyType.Parse(el),
            "piv" => docEmptyType.Parse(el),
            "bull" => docEmptyType.Parse(el),
            "hellip" => docEmptyType.Parse(el),
            "prime" => docEmptyType.Parse(el),
            "Prime" => docEmptyType.Parse(el),
            "oline" => docEmptyType.Parse(el),
            "frasl" => docEmptyType.Parse(el),
            "weierp" => docEmptyType.Parse(el),
            "imaginary" => docEmptyType.Parse(el),
            "real" => docEmptyType.Parse(el),
            "trademark" => docEmptyType.Parse(el),
            "alefsym" => docEmptyType.Parse(el),
            "larr" => docEmptyType.Parse(el),
            "uarr" => docEmptyType.Parse(el),
            "rarr" => docEmptyType.Parse(el),
            "darr" => docEmptyType.Parse(el),
            "harr" => docEmptyType.Parse(el),
            "crarr" => docEmptyType.Parse(el),
            "lArr" => docEmptyType.Parse(el),
            "uArr" => docEmptyType.Parse(el),
            "rArr" => docEmptyType.Parse(el),
            "dArr" => docEmptyType.Parse(el),
            "hArr" => docEmptyType.Parse(el),
            "forall" => docEmptyType.Parse(el),
            "part" => docEmptyType.Parse(el),
            "exist" => docEmptyType.Parse(el),
            "empty" => docEmptyType.Parse(el),
            "nabla" => docEmptyType.Parse(el),
            "isin" => docEmptyType.Parse(el),
            "notin" => docEmptyType.Parse(el),
            "ni" => docEmptyType.Parse(el),
            "prod" => docEmptyType.Parse(el),
            "sum" => docEmptyType.Parse(el),
            "minus" => docEmptyType.Parse(el),
            "lowast" => docEmptyType.Parse(el),
            "radic" => docEmptyType.Parse(el),
            "prop" => docEmptyType.Parse(el),
            "infin" => docEmptyType.Parse(el),
            "ang" => docEmptyType.Parse(el),
            "and" => docEmptyType.Parse(el),
            "or" => docEmptyType.Parse(el),
            "cap" => docEmptyType.Parse(el),
            "cup" => docEmptyType.Parse(el),
            "int" => docEmptyType.Parse(el),
            "there4" => docEmptyType.Parse(el),
            "sim" => docEmptyType.Parse(el),
            "cong" => docEmptyType.Parse(el),
            "asymp" => docEmptyType.Parse(el),
            "ne" => docEmptyType.Parse(el),
            "equiv" => docEmptyType.Parse(el),
            "le" => docEmptyType.Parse(el),
            "ge" => docEmptyType.Parse(el),
            "sub" => docEmptyType.Parse(el),
            "sup" => docEmptyType.Parse(el),
            "nsub" => docEmptyType.Parse(el),
            "sube" => docEmptyType.Parse(el),
            "supe" => docEmptyType.Parse(el),
            "oplus" => docEmptyType.Parse(el),
            "otimes" => docEmptyType.Parse(el),
            "perp" => docEmptyType.Parse(el),
            "sdot" => docEmptyType.Parse(el),
            "lceil" => docEmptyType.Parse(el),
            "rceil" => docEmptyType.Parse(el),
            "lfloor" => docEmptyType.Parse(el),
            "rfloor" => docEmptyType.Parse(el),
            "lang" => docEmptyType.Parse(el),
            "rang" => docEmptyType.Parse(el),
            "loz" => docEmptyType.Parse(el),
            "spades" => docEmptyType.Parse(el),
            "clubs" => docEmptyType.Parse(el),
            "hearts" => docEmptyType.Parse(el),
            "diams" => docEmptyType.Parse(el),
            "OElig" => docEmptyType.Parse(el),
            "oelig" => docEmptyType.Parse(el),
            "Scaron" => docEmptyType.Parse(el),
            "scaron" => docEmptyType.Parse(el),
            "Yumlaut" => docEmptyType.Parse(el),
            "circ" => docEmptyType.Parse(el),
            "tilde" => docEmptyType.Parse(el),
            "ensp" => docEmptyType.Parse(el),
            "emsp" => docEmptyType.Parse(el),
            "thinsp" => docEmptyType.Parse(el),
            "zwnj" => docEmptyType.Parse(el),
            "zwj" => docEmptyType.Parse(el),
            "lrm" => docEmptyType.Parse(el),
            "rlm" => docEmptyType.Parse(el),
            "ndash" => docEmptyType.Parse(el),
            "mdash" => docEmptyType.Parse(el),
            "lsquo" => docEmptyType.Parse(el),
            "rsquo" => docEmptyType.Parse(el),
            "sbquo" => docEmptyType.Parse(el),
            "ldquo" => docEmptyType.Parse(el),
            "rdquo" => docEmptyType.Parse(el),
            "bdquo" => docEmptyType.Parse(el),
            "dagger" => docEmptyType.Parse(el),
            "Dagger" => docEmptyType.Parse(el),
            "permil" => docEmptyType.Parse(el),
            "lsaquo" => docEmptyType.Parse(el),
            "rsaquo" => docEmptyType.Parse(el),
            "euro" => docEmptyType.Parse(el),
            "tm" => docEmptyType.Parse(el),
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

  class docParaType// mixed class
  {
    <xsd:group ref="docCmdGroup" minOccurs="0" maxOccurs="unbounded" />
  }

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


enum DoxBool
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
    IDL,

    [EnumString("Java")]
    Java,

    [EnumString("C#")]
    Csharp,

    [EnumString("D")]
    D,

    [EnumString("PHP")]
    PHP,

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
    XML,

    [EnumString("SQL")]
    SQL,

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
