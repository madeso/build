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


pub fn scan_and_generate(input: &data::UserInput, root: &Path)
{
    let mut project = data::Project::new(input);
    let mut scanner = parser::Scanner::new();
    let mut feedback = parser::ProgressFeedback::new();
    scanner.rescan(&mut project, &mut feedback);
    generate_report(root, &project, &scanner);
}

// */







///////////////////////////////////////////////////////////////////////////////////////////////////
// ReportForm

// Data.Project project;
// Parser.Scanner scanner;
// Parser.Analytics analytics;

fn generate_report(root: &Path, project: &data::Project, scanner: &parser::Scanner)
{
    {
        let mut html = String::new();
        html::begin(&mut html, "Errors");
        for s in &scanner.errors
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
        for s in &scanner.not_found // .OrderBy(s => s)
        {
            let origin = &scanner.not_found_origins[s];
            html.push_str(&format!("<p>{} from {}</p>", s, html::inspect_filename_link(origin).unwrap() ))
        }
        html::end(&mut html);

        let path = core::join(root, "missing.html");
        core::write_string_to_file_or(&path, &html).unwrap();
    }

    let analytics = parser::analyze(project);
    parser::write_css_file(root);
    parser::generate_index_page(root, project, &analytics);

    for (f, _) in &project.scanned_files
    {
        write_inspection_page(root, f, project, &analytics);
    }
}


fn write_inspect_header_table<LengthFun>
(
    html: &mut String,
    analytics: &parser::Analytics,
    included: &mut Vec<PathBuf>,
    class: &str,
    header: &str,
    length_fun: LengthFun
) where LengthFun: Fn(&parser::ItemAnalytics) -> usize
{
    html.push_str(&format!("<div id=\"{}\">\n", class));
    html.push_str(&format!("<h2>{}</h2>\n", header));

    included.sort_by_key(|s| { length_fun(&analytics.file_to_data[s])});
    included.reverse();

    html.push_str("<table class=\"list\">\n");
    html.push_str("<tr>  <th class=\"file\">File</th>  <th>Count</th>  <th>Lines</th>  </tr>\n");
    
    for s in included
    {
        let display_filename = html::inspect_filename_link(&s).unwrap();
        let display_count    = rust::num_format(length_fun(&analytics.file_to_data[s]));
        let display_lines    = rust::num_format(analytics.file_to_data[s].total_included_lines);
        
        html.push_str(&format!("<tr><td class=\"file\">{}</td> <td class=\"num\">{}</td> <td class=\"num\">{}</td></tr>", display_filename, display_count, display_lines));
    }

    html.push_str("</table>\n");
    html.push_str("</div>\n");
}


fn write_inspection_page(root: &Path, file: &Path,  project: &data::Project, analytics: &parser::Analytics)
{
    let mut html = String::new();

    let display_name = html::get_filename(file).unwrap();

    html::begin(&mut html, &format!("Inspecting - {}", display_name));

    write_inspect_header_table
    (
        &mut html,
        analytics,
        &mut project.scanned_files.iter().filter(|kvp| { kvp.1.absolute_includes.contains(&file.to_path_buf())}).map(|kvp| {kvp.0.to_path_buf()} ).collect(),
        "included_by", &format!("Theese include {}", display_name),
        |it| { it.all_included_by.len() }
    );
    
    {
        html.push_str("<div id=\"file\">\n");

        let project_file = &project.scanned_files[file];
        let analytics_file = &analytics.file_to_data[file];
        let file_lines = rust::num_format(project_file.number_of_lines);
        let direct_lines = rust::num_format(project_file.absolute_includes.iter().map(|f| {project.scanned_files[f].number_of_lines}).sum());
        let direct_count = rust::num_format(project_file.absolute_includes.len());
        let total_lines = rust::num_format(analytics_file.total_included_lines);
        let total_count = rust::num_format(analytics_file.all_includes.len());
        
        html.push_str(&format!("<h2>{}</h2>\n", display_name));

        html.push_str("<table class=\"summary\">");

        html.push_str(         "<tr>  <th></th>                 <th>Lines</th>              <th>Files</th>           </tr>\n");
        html.push_str(&format!("<tr>  <th>Lines:</th>           <td class=\"num\">{}</td>   <td class=\"num\">1</td> </tr>\n", file_lines));
        html.push_str(&format!("<tr>  <th>Direct Includes:</th> <td class=\"num\">{0}</td>  <td class=\"num\">{1}</td>  </tr>\n", direct_lines, direct_count));
        html.push_str(&format!("<tr>  <th>Total Includes:</th>  <td class=\"num\">{0}</td>  <td class=\"num\">{1}</td> </tr>\n", total_lines, total_count));

        html.push_str("</table>");

        html.push_str("</div>\n")
    }

    write_inspect_header_table
    (
        &mut html,
        analytics,
        &mut project.scanned_files[file].absolute_includes.clone(),
        "includes", &format!("{} includes theese", display_name),
        |it| { it.all_includes.len() }
    );

    html::end(&mut html);

    let filename = html::safe_inspect_filename(file).unwrap();
    let path = core::join(root, &filename);
    core::write_string_to_file_or(&path, &html).unwrap();
}
