use std::path::{Path, PathBuf};
use crate::
{
    core,
    rust,
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


fn write_inspect_header_table<LengthFun>
(
    html: &mut String,
    _project: &data::Project,
    _analytics: &parser::Analytics,
    included: &mut Vec<PathBuf>,
    class: &str,
    header: &str,
    length_fun: LengthFun
) where LengthFun: Fn(&parser::ItemAnalytics) -> usize
{
    html.push_str(&format!("<div id=\"{}\">\n", class));
    html.push_str(&format!("<h2>{}</h>\n", header));

    // let mut included = _project.Files[file].AbsoluteIncludes.clone();
    included.sort_by_key(|s| { length_fun(&_analytics.Items[s])});
    included.reverse();

    html.push_str("<table class=\"list\">\n");
    html.push_str("<tr>  <th>File</th>  <th>Count</th>  <th>Lines</th>  </tr>\n");
    
    for s in included
    {
        let display_filename = html::inspect_filename_link(&s).unwrap();
        let display_count    = rust::num_format(length_fun(&_analytics.Items[s]));
        let display_lines    = rust::num_format(_analytics.Items[s].TotalIncludeLines);
        
        html.push_str(&format!("<tr><td>{}</td> <td>{}</td> <td>{}</td></tr>", display_filename, display_count, display_lines));
    }

    html.push_str("</table>\n");
    html.push_str("</div>\n");
}


fn WriteInspect(root: &Path, file: &Path,  _project: &data::Project, _analytics: &parser::Analytics)
{
    let mut html = String::new();

    let display_name = html::get_filename(file).unwrap();

    html::begin(&mut html, &format!("Inspecting - {}", display_name));

    write_inspect_header_table
    (
        &mut html,
        _project,
        _analytics,
        &mut _project.Files.iter().filter(|kvp| { kvp.1.AbsoluteIncludes.contains(&file.to_path_buf())}).map(|kvp| {kvp.0.to_path_buf()} ).collect(),
        "included_by", "Included by",
        |it| { it.AllIncludedBy.len() }
    );
    
    {
        html.push_str("<div id=\"file\">\n");

        let projectFile = &_project.Files[file];
        let analyticsFile = &_analytics.Items[file];
        let fileLines = rust::num_format(projectFile.Lines);
        let directLines = rust::num_format(projectFile.AbsoluteIncludes.iter().map(|f| {_project.Files[f].Lines}).sum());
        let directCount = rust::num_format(projectFile.AbsoluteIncludes.len());
        let totalLines = rust::num_format(analyticsFile.TotalIncludeLines);
        let totalCount = rust::num_format(analyticsFile.AllIncludes.len());
        
        html.push_str(&format!("<h2>{}</h2>\n", display_name));

        html.push_str("<table class=\"summary\">");

        html.push_str(&format!("<tr>   <th>Lines:</td>            <td>{}</td>  </tr>\n", fileLines));
        html.push_str(&format!("<tr>   <th>Direct Includes:</td>  <td>{0} lines</td>   <td>{1} files</td>  </tr>\n", directLines, directCount));
        html.push_str(&format!("<tr>  <th>Total Includes:</td> <td>{0} lines</td> <td>{1} files</td> </tr>\n", totalLines, totalCount));

        html.push_str("</table>");

        html.push_str("</div>\n")
    }

    write_inspect_header_table
    (
        &mut html,
        _project,
        _analytics,
        &mut _project.Files[file].AbsoluteIncludes.clone(),
        "includes", "Includes",
        |it| { it.AllIncludes.len() }
    );

    html::end(&mut html);

    let filename = html::safe_inspect_filename(file).unwrap();
    let path = core::join(root, &filename);
    core::write_string_to_file_or(&path, &html).unwrap();
}
