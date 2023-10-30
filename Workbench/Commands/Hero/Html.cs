using Workbench.Shared;

namespace Workbench.Commands.Hero;

internal class Html
{
    public string Buffer = "";

    public void PushString(string s)
    {
        Buffer += s;
    }

    public void BeginJoin(string title)
    {
        Buffer +=
            $@"
<!DOCTYPE html>
<html>
<head>
    <link rel=""stylesheet"" type=""text/css"" media=""screen"" href=""header_hero_report.css""/>
    <title>{title}</title>
</head>
<body>

<div id=""root"">

<nav id=""main"">
    <ol>
        <li><a href=""index.html"">Index</a></li>
        <li><a href=""missing.html"">Missing</a></li>
        <li><a href=""errors.html"">Error</a></li>
    </ol>
</nav>

<div id=""page"">
<div id=""content"">

<h1>{title}</h1>
<div id=""body"">
";
    }

    public void End()
    {
        Buffer += @"
</div>
</div>
</div>
</div>
</body>
</html>";
    }


    public static string GetSafeString(string str)
    {
        // algorithm inspired by the description of the doxygen version
        // https://stackoverflow.com/a/30490482
        var buf = "";

        foreach (var c in str)
        {
            buf += c switch
            {
                // '0' .. '9'
                '0' or '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9' or
                // 'a'..'z'
                'a' or 'b' or 'c' or 'd' or 'e' or 'f' or 'g' or 'h' or 'i' or 'j' or
                'k' or 'l' or 'm' or 'n' or 'o' or 'p' or 'q' or 'r' or 's' or 't' or
                'u' or 'v' or 'w' or 'x' or 'y' or 'z' or
                // other safe characters...
                // is _ considered safe? we only care about one way translation
                // so it should be safe.... right?
                '-' or '_'
                => $"{c}",
                // 'A'..'Z'
                // 'A'..'Z'
                'A' or 'B' or 'C' or 'D' or 'E' or 'F' or 'G' or 'H' or 'I' or 'J' or
                'K' or 'L' or 'M' or 'N' or 'O' or 'P' or 'Q' or 'R' or 'S' or 'T' or
                'U' or 'V' or 'W' or 'X' or 'Y' or 'Z'
                => $"_{char.ToLowerInvariant(c)}",
                _ => $"_{(int)c}"
            };
        }

        return buf;
    }


    internal static string GetSafeInspectFilename(Fil path, string ext_with_dot)
    {
        return $"inspect_{GetSafeString(path.GetDisplay())}{ext_with_dot}";
    }

    public static string GetSafeInspectFilenameHtml(Fil path)
    {
        return GetSafeInspectFilename(path, ".html");
    }

    public static string GetSafeInspectFilenameWithoutHtml(Fil path)
    {
        return GetSafeInspectFilename(path, "");
    }


    public static string GetFilename(Dir? common, Dir root, Fil path)
    {
        if (common != null)
        {
            var rel = common.GetRelativeTo(path);
            if (rel.StartsWith("..") == false)
            {
                return rel;
            }
        }
        return root.GetRelativeTo(path);
    }


    public static string inspect_filename_link(Dir? common, Dir root, Fil path)
    {
        var file = GetSafeInspectFilenameHtml(path);
        var name = GetFilename(common, root, path);

        return $"<a href=\"{file}\">{name}</a>";
    }


    private static Fil GetPathToCssFile(Dir root) => root.GetFile("header_hero_report.css");


    public static void WriteCssFile(Dir root)
    {
        GetPathToCssFile(root).WriteAllText(CSS_SOURCE);
    }

    internal void WriteToFile(Fil path)
    {
        path.WriteAllText(Buffer);
    }

    private const string CSS_SOURCE = @"

/* Reset */

* {
    margin: 0;
    padding: 0;
    border: 0;
    outline: 0;
    font-weight: inherit;
    font-style: inherit;
    font-size: 100%;
    font-family: inherit;
    vertical-align: baseline;
}

body {
    line-height: 1;
    color: black;
    background: white;
}

ol, ul {
    list-style: none;
}

table {
    border-collapse: separate;
    border-spacing: 0;
}

caption, th, td {
    text-align: left;
    font-weight: normal;
}

a {
    text-decoration: none;
    color: #44f;
}

a:hover
{
    color: #00f;
}

body {
    background: #ddd;
    font-size: 12px;
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    font-weight: normal;
    overflow-y: scroll;
    margin: 0px;
}

h1 {
    font-size: 16px;
    margin: 10px 0px 10px 0px;
}

h2 {
    font-size: 16px;
    margin: 10px 0px 10px 0px;
}

h3 {
    font-size: 14px;
    margin: 10px 0px 10px 0px;
    color: red;
}

table.summary {
    margin-left: 10px;
}

td, th
{
    padding-left: 12px;
}

td:nth-child(1), th:nth-child(1)
{
    padding-left: 0px;
}

td:nth-child(3), th:nth-child(3)
{
    padding-left: 22px;
}


td:hover, tr:hover
{
    background-color: #eee;
}


table td
{
    text-align: right;
}

table th
{
    text-align: right;
    font-weight: bold;
}

table.summary th, td.file, th.file
{
    text-align: left;
}

td.num, span.num, div.code
{
    font-family: Consolas, ""Andale Mono WT"", ""Andale Mono"", ""Lucida Console"", ""Lucida Sans Typewriter"", ""DejaVu Sans Mono"", ""Bitstream Vera Sans Mono"", ""Liberation Mono"", ""Nimbus Mono L"", Monaco, ""Courier New"", Courier, monospace;
}

div#root {
    margin: 0px;
    padding: 0px;
}

nav#main {
    margin: 0px;
    padding: 10px;
}

nav#main {
    background-color: #aaa;
}

nav#main a {
    font-size: 16px;
}

nav li {
    display: inline;
    padding: 5px;
}

div#page,
nav#main ol {
    max-width: 805px;
    margin: auto;
    position: relative;
    display: block;
}

div#page {
    background-color: #fff;
    padding-top: 30px;
    padding-bottom: 90px;
}

div#content {
    margin: 12px;
}



#body
{
    display: grid;
    grid-template-columns: 1fr 1fr 1fr;
}


#included_by, #file, #includes
{
    grid-column: auto / auto;
    grid-row: auto/auto;
}

#summary, .missing
{
    grid-column: 1 / span 3;
    grid-row: auto/auto;
}

.missing ul
{
    padding-top: 3px;
    padding-bottom: 10px;
}

.missing li
{
    display: inline-block;
    padding: 2px 3px;
}


div.tidy-warnings div.code
{
    padding-bottom: 25px;
}

div.code
{
    white-space: pre;
    overflow-x: hidden;
    background: #fff;
    padding: 12px;
}

div.code:hover, div.code:focus
{
    width: 1600px;
    position: relative;
    left: -400px;
    white-space: pre-wrap;
}

span.tidy-markup-error
{
    color: red;
    font-weight: bold;
}

span.tidy-class-markup
{
    font-weight: bold;
}

span.tidy-file-name
{
    color: #aaa;
}

";

}
