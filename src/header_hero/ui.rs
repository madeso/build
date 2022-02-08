use std::path::{Path, PathBuf};
use crate::
{
    core,
    header_hero::data,
    header_hero::parser,
    header_hero::html
};

///////////////////////////////////////////////////////////////////////////////////////////////////
// MainForm

//*


pub fn ScanAndGenerate(input: &data::UserInput, root: &Path)
{
    let mut _project = data::Project::new(input);
    let mut scanner = parser::Scanner::new();
    let mut feedback = parser::ProgressFeedback::new();
    scanner.Rescan(&mut _project, &mut feedback);
    GenerateReport(root, &_project, &scanner);
}

// */







///////////////////////////////////////////////////////////////////////////////////////////////////
// ReportForm

// Data.Project _project;
// Parser.Scanner _scanner;
// Parser.Analytics _analytics;

fn GenerateReport(root: &Path, _project: &data::Project, scanner: &parser::Scanner)
{
    {
        let mut html = String::new();
        html::begin(&mut html, "Errors");
        for s in &scanner.Errors
        {
            html.push_str(&format!("<p>{}</p>", s));
        }
        html.push_str("<h1>Unhandled extensions</h1>");
        for (ext, count) in scanner.missing_ext.most_common().iter()
        {
            html.push_str(&format!("<p>{} {}</p>", ext, count));
        }
        html::end(&mut html);

        let path = core::join(root, "errors.html");
        core::write_string_to_file_or(&path, &html).unwrap();
    }
    
    {
        let mut html = String::new();
        html::begin(&mut html, "Missing");
        for s in &scanner.NotFound // .OrderBy(s => s)
        {
            let origin = &scanner.NotFoundOrigins[s];
            html.push_str(&format!("<p>{} from {}</p>", s, origin.display()))
        }
        html::end(&mut html);

        let path = core::join(root, "missing.html");
        core::write_string_to_file_or(&path, &html).unwrap();
    }

    let _analytics = parser::Analyze(_project);
    parser::GenerateCss(root);
    parser::GenerateIndex(root, _project, &_analytics);

    for (f, _) in &_project.Files
    {
        WriteInspect(root, f, _project, &_analytics);
    }
}


fn WriteInspect(root: &Path, file: &Path,  _project: &data::Project, _analytics: &parser::Analytics)
{
    let mut html = String::new();

    let display_name = html::get_filename(file).unwrap();

    html::begin(&mut html, &format!("Inspecting - {}", display_name));

    // todo(Gustav): merge included and included by... they share alot of logic...

    {
        html.push_str("<div id=\"included_by\">\n");
        html.push_str("<h2>Included by</h>\n");

        let file_buf = file.to_path_buf();
        let mut included : Vec<PathBuf> = _project.Files.iter().filter(|kvp| { kvp.1.AbsoluteIncludes.contains(&file_buf)}).map(|kvp| {kvp.0.to_path_buf()} ).collect();
        included.sort_by_key(|s| { _analytics.Items[s].AllIncludedBy.len()});
        included.reverse();

        for s in included
        {
            let display_filename = html::inspect_filename_link(&s).unwrap();
            let display_count    = _analytics.Items[&s].AllIncludedBy.len();
            let display_lines    = _analytics.Items[&s].TotalIncludeLines;
            
            html.push_str(&format!("<p>{} {} {}</p>", display_filename, display_count, display_lines));
        }

        html.push_str("</div>\n")
    }
    
    {
        html.push_str("<div id=\"file\">\n");
        html.push_str("<h2>File</h>\n");

        let projectFile = &_project.Files[file];
        let analyticsFile = &_analytics.Items[file];
        let fileLines = projectFile.Lines;
        let directLines : usize = projectFile.AbsoluteIncludes.iter().map(|f| {_project.Files[f].Lines}).sum();
        let directCount = projectFile.AbsoluteIncludes.len();
        let totalLines = analyticsFile.TotalIncludeLines;
        let totalCount = analyticsFile.AllIncludes.len();
        
        html.push_str(&format!("<h2>{}</h2>\n", display_name));
        html.push_str(&format!("<p>Lines: {}</p>\n", fileLines));
        html.push_str(&format!("<p>Direct Includes: {0} lines, {1} files</p>\n", directLines, directCount));
        html.push_str(&format!("<p>Total Includes: {0} lines, {1} files</p>\n", totalLines, totalCount));

        html.push_str("</div>\n")
    }

    {
        html.push_str("<div id=\"includes\">\n");
        html.push_str("<h2>Includes</h>\n");

        let mut included = _project.Files[file].AbsoluteIncludes.clone();
        included.sort_by_key(|s| { _analytics.Items[s].AllIncludes.len()});
        included.reverse();
        
        for s in included
        {
            let display_filename = html::inspect_filename_link(&s).unwrap();
            let display_count    = _analytics.Items[&s].AllIncludes.len();
            let display_lines    = _analytics.Items[&s].TotalIncludeLines;
            
            html.push_str(&format!("<p>{} {} {}</p>", display_filename, display_count, display_lines));
        }

        html.push_str("</div>\n")
    }

    html::end(&mut html);

    let filename = html::safe_inspect_filename(file).unwrap();
    let path = core::join(root, &filename);
    core::write_string_to_file_or(&path, &html).unwrap();
}
