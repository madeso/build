use std::path::{Path, PathBuf};

use crate::
{
    core
};


pub fn begin(str: &mut String, title: &str)
{
    begin_x(str, title, true);
}

pub fn begin_nojoin(str: &mut String, title: &str)
{
    begin_x(str, title, false);
}

fn begin_x(str: &mut String, title: &str, use_join: bool)
{
    str.push_str
    (
        r###"
<!DOCTYPE html>
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

<div id="root">

<nav id="main">
    <ol>
        <li><a href="index.html">Index</a></li>
        <li><a href="missing.html">Missing</a></li>
        <li><a href="errors.html">Error</a></li>
    </ol>
</nav>

<div id="page">
<div id="content">

<h1>"###
    );
    str.push_str(title);
    str.push_str("</h1>\n");

    if use_join
    {
        str.push_str("<div id=\"body\">\n");
    }
    else
    {
        str.push_str("<div id=\"body_single\">\n");
    }
}

pub fn end(str: &mut String)
{
    str.push_str
    (
        r###"
</div>
</div>
</div>
</div>
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
        let safe = safe_string(file);
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


fn path_to_css_file(root: &Path) -> PathBuf { core::join(root, "header_hero_report.css") }


pub fn write_css_file(root: &Path)
{
    core::write_string_to_file_or(&path_to_css_file(root), CSS_SOURCE).unwrap();
}

const CSS_SOURCE: &str = r###"

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
    font-family: Consolas, "Andale Mono WT", "Andale Mono", "Lucida Console", "Lucida Sans Typewriter", "DejaVu Sans Mono", "Bitstream Vera Sans Mono", "Liberation Mono", "Nimbus Mono L", Monaco, "Courier New", Courier, monospace;
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


"###;
