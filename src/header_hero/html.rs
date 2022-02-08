use std::path::Path;


pub fn begin(str: &mut String, title: &str)
{
    str.push_str
    (
        r###"
<html>
<head>
    <link rel="stylesheet" type="text/css" media="screen" href="header_hero_report.css"/>
    <title>"###
    );
    str.push_str(title);
    str.push_str
    (
        r###"</title>
</head>
<body>

<nav class="main">
    <ol>
        <li><a href="index.html">Index</a></li>
        <li><a href="missing.html">Missing</a></li>
        <li><a href="errors.html">Error</a></li>
    </ol>
</tr>

<h1>"###
    );
    str.push_str(title);
    str.push_str("</h1>");
}

pub fn end(str: &mut String)
{
    str.push_str
    (
        r###"

</body>
</html>"###
    );
}


pub fn safe_string(str: &str) -> String
{
    // algorithm inspired by the description of the doxygen version
    // https://stackoverflow.com/a/30490482
    let mut buf = String::new();

    for c in str.chars()
    {
        match c
        {
            // '0' .. '9'
            '0' | '1' | '2' | '3' | '4' | '5' | '6'| '7' | '8' | '9' |
            // 'a'..'z'
            'a' | 'b' | 'c' | 'd' | 'e' | 'f' | 'g' | 'h' | 'i' | 'j' |
            'k' | 'l' | 'm' | 'n' | 'o' | 'p' | 'q' | 'r' | 's' | 't' |
            'u' | 'v' | 'w' | 'x' | 'y' | 'z' |
            // other safe characters...
            // is _ considered safe? we only care about one way translation
            // so it should be safe.... right?
            '-' | '_'
            => { buf.push(c); },
            // 'A'..'Z'
            // 'A'..'Z'
            'A' | 'B' | 'C' | 'D' | 'E' | 'F' | 'G' | 'H' | 'I' | 'J' |
            'K' | 'L' | 'M' | 'N' | 'O' | 'P' | 'Q' | 'R' | 'S' | 'T' |
            'U' | 'V' | 'W' | 'X' | 'Y' | 'Z'
            => { buf.push('_'); buf.push(c.to_ascii_lowercase()); },
            _ => { buf.push_str(&format!("_{}", u32::from(c))); }
        }
    }

    buf
}


pub fn safe_inspect_filename(path: &Path) -> Option<String>
{
    if let Some(file) = path.to_str()
    {
        let safe = safe_string(&file);
        Some(format!("inspect_{}.html", safe))
    }
    else
    {
        None
    }
}


pub fn get_filename(path: &Path) -> Option<String>
{
    let name = path.file_name()?.to_str()?;
    Some(name.to_string())
}


pub fn inspect_filename_link(path: &Path) -> Option<String>
{
    let file = safe_inspect_filename(path)?;
    let name = get_filename(path)?;

    Some(format!("<a href=\"{0}\">{1}</a>", file, name))
}
