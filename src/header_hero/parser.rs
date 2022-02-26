///////////////////////////////////////////////////////////////////////////////////////////////////
// Parser

use std::io;
use std::path::{Path, PathBuf};
use std::collections::{HashSet, HashMap};

use crate::
{
    rust,
    core,
    header_hero::data,
    header_hero::html
};


enum States { Start, Hash, Include, AngleBracket, Quote }
enum ParseResult { Ok, Error }

pub struct Result
{
    pub system_includes: Vec<String>,
    pub local_includes: Vec<String>,
    pub number_of_lines: usize,
}

fn canonicalize_or_default(p: &Path) -> PathBuf
{
    match p.canonicalize()
    {
        Ok(r) => r,
        Err(_) => p.to_path_buf()
    }
}

fn substr(s: &str, start: usize, len: usize) -> &str
{
    &s[start..start+len]
}

impl Result
{
    pub fn new() -> Result
    {
        Result
        {
            system_includes: Vec::<String>::new(),
            local_includes: Vec::<String>::new(),
            number_of_lines: 0
        }
    }

    fn parse_line(&mut self, line: &str) -> ParseResult
    {
        let mut i: usize = 0;
        let mut path_start: usize = 0;
        let mut state = States::Start;

        loop
        {
            if i >= line.len()
            {
                return ParseResult::Error;
            }

            let c = &line[i..i+1];
            i += 1;
            
            if c == " " || c == "\t"
            {
                // pass
            }
            else
            {
                match state
                {
                    States::Start =>
                    {
                        if c == "#"
                        {
                            state = States::Hash;
                        }
                        else if c == "/"
                        {
                            if i >= line.len()
                            {
                                return ParseResult::Error;
                            }
                            if &line[i..i+1] == "/"
                            {
                                // Matched C++ style comment
                                return ParseResult::Ok;
                            }
                        }
                        else
                        {
                            return ParseResult::Error;
                        }
                    },
                    States::Hash =>
                    {
                        i -= 1;
                        if rust::find_start_at_str(line, i, "include").unwrap_or(i+1) == i
                        {
                            i += 7;
                            state = States::Include;
                        }
                        else
                        {
                            // Matched preprocessor other than #include
                            return ParseResult::Ok;
                        }
                    },
                    States::Include =>
                    {
                        if c == "<"
                        {
                            path_start = i;
                            state = States::AngleBracket;
                        }
                        else if c == "\""
                        {
                            path_start = i;
                            state = States::Quote;
                        }
                        else
                        {
                            return ParseResult::Error;
                        }
                    },
                    States::AngleBracket =>
                    {
                        if c == ">"
                        {
                            self.system_includes.push(substr(line, path_start, i-path_start-1).to_string());
                            return ParseResult::Ok;
                        }
                    },
                    States::Quote =>
                    {
                        if c == "\""
                        {
                            self.local_includes.push(substr(line, path_start, i-path_start-1).to_string());
                            return ParseResult::Ok;
                        }
                    }
                }
            }
        }
    }
}


/// Simple parser... only looks for #include lines. Does not take #defines or comments into account.
pub fn parse_file(fi: &Path, errors: &mut Vec<String>) -> Result
{
    let mut res = Result::new();

    if let Ok(lines) = core::read_file_to_lines(fi)
    {
        res.number_of_lines = lines.len();
        for line in lines
        {
            if line.contains('#') && line.contains("include")
            {
                match res.parse_line(&line)
                {
                    ParseResult::Error =>
                        errors.push(format!("Could not parse line: {} in file: {}", line, fi.display())),
                    _ => {}
                }
            }
        }
    }
    else
    {
        errors.push(format!("Unable to open file {}", fi.display()));
    }

    res
}
    


///////////////////////////////////////////////////////////////////////////////////////////////////
// Analytics



pub struct ItemAnalytics
{
    pub all_includes: HashSet<PathBuf>,
    pub total_included_lines: usize,
    pub all_included_by: HashSet<PathBuf>,
    pub translation_units_included_by: HashSet<PathBuf>,
    pub is_analyzed: bool,
}

impl ItemAnalytics
{
    pub fn new() -> ItemAnalytics
    {
        ItemAnalytics
        {
            all_includes: HashSet::<PathBuf>::new(),
            total_included_lines: 0,
            all_included_by: HashSet::<PathBuf>::new(),
            translation_units_included_by: HashSet::<PathBuf>::new(),
            is_analyzed: false,
        }
    }
}

pub struct Analytics
{
    pub file_to_data: HashMap<PathBuf, ItemAnalytics>
}

pub fn analyze(project: &data::Project) -> Analytics
{
    let mut analytics = Analytics::new();
    for file in project.scanned_files.keys()
    {
        analytics.analyze(file, project);
    }
    analytics
}

impl Analytics
{
    pub fn new() -> Analytics
    {
        Analytics
        {
            file_to_data: HashMap::<PathBuf, ItemAnalytics>::new()
        }
    }

    fn add_trans(&mut self, inc: &Path, path: &Path)
    {
        if let Some(it) = self.file_to_data.get_mut(&inc.to_path_buf())
        {
            it.translation_units_included_by.insert(path.to_path_buf());
        }
    }
    
    fn add_all_inc(&mut self, inc: &Path, path: &Path)
    {
        if let Some(it) = self.file_to_data.get_mut(&inc.to_path_buf())
        {
            it.all_included_by.insert(path.to_path_buf());
        }
    }

    fn analyze(&mut self, path: &Path, project: &data::Project)
    {
        if let Some(ret) = self.file_to_data.get_mut(path)
        {
            assert!(ret.is_analyzed);
            return;
        }

        let mut ret = ItemAnalytics::new();
        ret.is_analyzed = true;

        let sf = &project.scanned_files[path];
        for include in &sf.absolute_includes
        {
            if include == path { continue; }
            
            let is_tu = data::is_translation_unit(path);
            
            self.analyze(include, project);

            let all_includes = 
                if let Some(ai) = self.file_to_data.get_mut(&include.to_path_buf())
                {
                    ret.all_includes.insert(include.to_path_buf());
                    ai.all_included_by.insert(path.to_path_buf());
                    
                    if is_tu
                    {
                        ai.translation_units_included_by.insert(path.to_path_buf());
                    }


                    let union: HashSet<_> = ret.all_includes.union(&ai.all_includes).collect();
                    ret.all_includes = union.iter().map(|x| {x.to_path_buf()}).collect();

                    Some(ai.all_includes.clone())
                }
                else
                {
                    None
                }
            ;

            for inc in &all_includes.unwrap()
            {
                self.add_all_inc(inc, path);
                if is_tu
                {
                    self.add_trans(inc, path);
                }
            }
        }

        ret.total_included_lines = ret.all_includes.iter().map(|f|{project.scanned_files[f].number_of_lines}).sum();
        
        self.file_to_data.insert(path.to_path_buf(), ret);
    }
}



///////////////////////////////////////////////////////////////////////////////////////////////////
// Report


fn order_by_descending(v: &mut Vec::<(&PathBuf, usize)>)
{
    v.sort_by_key(|kvp| { kvp.1});
    v.reverse();
}

fn add_project_table_summary(sb: &mut String, table: &[(&str, String)] )
{
    sb.push_str("<div id=\"summary\">\n");
    sb.push_str("<table class=\"summary\">\n");
    for (label, value) in table
    {
        sb.push_str(&format!("  <tr><th>{0}:</th> <td>{1}</td></tr>\n", label, value));
    }
    sb.push_str("</table>\n");
    sb.push_str("</div>\n");
}

fn add_file_table(sb: &mut String, id: &str, header: &str, count_list: &[(&PathBuf, usize)])
{
    sb.push_str(&format!("<div id=\"{0}\">\n", id));
    sb.push_str(&format!("<a name=\"{0}\"></a>", id));
    sb.push_str(&format!("<h2>{0}</h2>\n\n", header));

    sb.push_str("<table class=\"list\">\n");
    for (path_to_file, count) in count_list
    {
        sb.push_str(&format!(
            "  <tr><td class=\"num\">{1}</td> <td class=\"file\">{0}</td></tr>\n", html::inspect_filename_link(path_to_file).unwrap(), rust::num_format(*count)
        ));
    }
    sb.push_str("</table>\n");
    sb.push_str("</div>\n");
}



fn path_to_index_file(root: &Path) -> PathBuf { core::join(root, "index.html") }
fn path_to_css_file(root: &Path) -> PathBuf { core::join(root, "header_hero_report.css") }


pub fn generate_index_page(root: &Path, project: &data::Project, analytics: &Analytics)
{
    let mut sb = String::new();

    html::begin(&mut sb, "Report");

    // Summary
    {
        let pch_lines : usize = project.scanned_files.iter()
            .filter(|kvp| -> bool {kvp.1.is_precompiled})
            .map(|kvp| -> usize {kvp.1.number_of_lines})
            .sum();
        let super_total_lines : usize = project.scanned_files.iter()
            .map(|kvp| -> usize {kvp.1.number_of_lines})
            .sum();
        let total_lines = super_total_lines - pch_lines;
        let total_parsed : usize = analytics.file_to_data.iter()
            .filter(|kvp| {data::is_translation_unit(kvp.0) && !project.scanned_files[kvp.0].is_precompiled})
            .map(|kvp| -> usize {kvp.1.total_included_lines + project.scanned_files[kvp.0].number_of_lines}).sum();
        let factor = total_parsed as f64 / total_lines as f64;
        let table =
        [
            ("Files", rust::num_format(project.scanned_files.len())),
            ("Total lines", rust::num_format(total_lines)),
            ("Total precompiled", format!("{0} (<a href=\"#pch\">list</a>)", rust::num_format(pch_lines))),
            ("Total parsed", rust::num_format(total_parsed)),
            ("Blowup factor", format!("{0:0.00} (<a href=\"#largest\">largest</a>, <a href=\"#hubs\">hubs</a>)", factor) ),
        ];
        add_project_table_summary(&mut sb, &table);
    }

    {
        let mut most: Vec<(&PathBuf, usize)> = analytics.file_to_data.iter()
            .map(|kvp| {(kvp.0, project.scanned_files[kvp.0].number_of_lines * kvp.1.translation_units_included_by.len())})
            .filter(|kvp| { !project.scanned_files[kvp.0].is_precompiled})
            .filter(|kvp| { kvp.1 > 0})
            .collect();
        order_by_descending(&mut most);
        add_file_table(&mut sb, "largest", "Biggest Contributors", &most);
    }

    {
        let mut hubs: Vec<(&PathBuf, usize)> = analytics.file_to_data.iter()
            .map(|kvp| {(kvp.0, kvp.1.all_includes.len() * kvp.1.translation_units_included_by.len())})
            .filter(|kvp| {kvp.1 > 0})
            .collect();
        order_by_descending(&mut hubs);
        add_file_table(&mut sb, "hubs", "Header Hubs", &hubs);
    }

    {
        let mut pch: Vec<(&PathBuf, usize)> = project.scanned_files.iter()
            .filter(|kvp| {kvp.1.is_precompiled})
            .map(|kvp| {(kvp.0, kvp.1.number_of_lines)})
            .collect();
        order_by_descending(&mut pch);
        add_file_table(&mut sb, "pch", "Precompiled Headers", &pch);
    }

    html::end(&mut sb);

    core::write_string_to_file_or(&path_to_index_file(root), &sb).unwrap();
}


///////////////////////////////////////////////////////////////////////////////////////////////////
// Scanner

pub struct ProgressFeedback
{
}

impl ProgressFeedback
{
    pub fn new() -> ProgressFeedback
    {
        ProgressFeedback{}
    }
    fn update_title(&self, new_title: &str)
    {
        println!("{}", new_title);
    }
    fn update_message(&self, new_message: &str)
    {
        println!("  {}", new_message);
    }
    fn update_count(&self, _new_count: usize) {}
    fn next_item(&self) {}
}

pub struct Scanner
{
    file_queue: HashSet<PathBuf>,
    scan_queue: Vec<PathBuf>,
    system_includes: HashMap<String, PathBuf>,
    is_scanning_pch: bool,
    pub errors: Vec<String>,
    pub not_found_origins: HashMap<String, Vec<PathBuf> >,
    pub missing_ext: counter::Counter<String, usize>,
}

impl Scanner
{
    pub fn new() -> Scanner
    {
        Scanner
        {
            file_queue: HashSet::<PathBuf>::new(),
            scan_queue: Vec::<PathBuf>::new(),
            system_includes: HashMap::<String, PathBuf>::new(),
            is_scanning_pch: false,
            errors: Vec::<String>::new(),
            not_found_origins: HashMap::<String, Vec<PathBuf> >::new(),
            missing_ext: counter::Counter::<String, usize>::new(),
        }
    }

    pub fn rescan(&mut self, project: &mut data::Project, feedback: &mut ProgressFeedback)
    {
        feedback.update_title("Scanning precompiled header...");
        for sf in &mut project.scanned_files.values_mut()
        {
            sf.is_touched = false;
            sf.is_precompiled = false;
        }

        // scan everything that goes into precompiled header
        self.is_scanning_pch = true;
        if let Some(inc) = project.precompiled_header.clone()
        {
            if inc.exists()
            {
                self.scan_file(project, &inc);
                while !self.scan_queue.is_empty()
                {
                    let to_scan = self.scan_queue.clone();
                    self.scan_queue.clear();
                    for fi in to_scan
                    {
                        self.scan_file(project, &fi);
                    }
                }
                self.file_queue.clear();
            }
        }
        self.is_scanning_pch = false;

        feedback.update_title("Scanning directories...");
        for dir in &project.scan_directories
        {
            feedback.update_message(&format!("{}", dir.display()));
            self.scan_directory(dir, feedback);
        }

        feedback.update_title("Scanning files...");

        let mut dequeued = 0;
        
        while !self.scan_queue.is_empty()
        {
            dequeued += self.scan_queue.len();
            let to_scan = self.scan_queue.clone();
            self.scan_queue.clear();
            for fi in &to_scan
            {
                feedback.update_count(dequeued + self.scan_queue.len());
                feedback.next_item();
                feedback.update_message(&format!("{}", fi.display()));
                self.scan_file(project, fi);
            }
        }
        self.file_queue.clear();
        self.system_includes.clear();

        project.scanned_files.retain(|_, value| {value.is_touched});
    }
    
    fn scan_directory(&mut self, dir: &Path, feedback: &ProgressFeedback)
    {
        if self.please_scan_directory(dir, feedback).is_err()
        {
            self.errors.push(format!("Cannot descend into {}", dir.display()));
        }
    }
    fn please_scan_directory(&mut self, dir: &Path, feedback: &ProgressFeedback) -> io::Result<()>
    {
        feedback.update_message(&format!("{}", dir.display()));

        for entry in dir.read_dir()?
        {
            let e = entry?.path();
            if e.is_file()
            {
                let file = e;
                let ext = file.extension().unwrap().to_str().unwrap();
                if data::is_translation_unit_extension(ext)
                {
                    self.add_to_queue(&file, &canonicalize_or_default(&file));
                }
                else
                {
                    // println!("invalid extension {}", ext);
                    self.missing_ext[&ext.to_string()] += 1;
                }
            }
            else
            {
                let subdir = e;
                self.scan_directory(&subdir, feedback);
            }
        }

        Ok(())
    }

    fn add_to_queue(&mut self, inc: &Path, abs: &Path)
    {
        if !self.file_queue.contains(abs)
        {
            self.file_queue.insert(abs.to_path_buf());
            self.scan_queue.push(inc.to_path_buf());
        }
    }

    fn scan_file(&mut self, project: &mut data::Project, p: &Path)
    {
        let path = canonicalize_or_default(p);
        // todo(Gustav): add last scan feature!!!
        if project.scanned_files.contains_key(&path) // && project.LastScan > path.LastWriteTime && !self.is_scanning_pch
        {
            let mut sf : data::SourceFile = project.scanned_files.get(&path).unwrap().clone();
            self.please_scan_file(project, &path, &mut sf);
            project.scanned_files.insert(path, sf);
        }
        else
        {
            let res = parse_file(&path, &mut self.errors);
            let mut sf = data::SourceFile::new();
            sf.number_of_lines = res.number_of_lines;
            sf.local_includes = res.local_includes;
            sf.system_includes = res.system_includes;
            sf.is_precompiled = self.is_scanning_pch;
            self.please_scan_file(project, &path, &mut sf);
            project.scanned_files.insert(path, sf);
        }
    }
    
    fn please_scan_file(&mut self, project: &mut data::Project, path: &Path, sf: &mut data::SourceFile)
    {
        sf.is_touched = true;
        sf.absolute_includes.clear();

        let local_dir = path.parent().unwrap();
        for s in &sf.local_includes
        {
            let inc = core::join(local_dir, s);
            let abs = canonicalize_or_default(&inc);
            // found a header that's part of PCH during regular scan: ignore it
            if !self.is_scanning_pch && project.scanned_files.contains_key(&abs) && project.scanned_files[&abs].is_precompiled
            {
                touch_file(project, &abs);
                continue;
            }
            if !inc.exists()
            {
                if !sf.system_includes.contains(s)
                {
                    sf.system_includes.push(s.to_string());
                }
                continue;
            }
            sf.absolute_includes.push(abs.clone());
            self.add_to_queue(&inc, &abs);
            // self.errors.push(format!("Exception: \"{0}\" for #include \"{1}\"", e.Message, s));
        }

        for s in &sf.system_includes
        {
            if self.system_includes.contains_key(s)
            {
                let abs = &self.system_includes[s];
                // found a header that's part of PCH during regular scan: ignore it
                if !self.is_scanning_pch && project.scanned_files.contains_key(abs) && project.scanned_files[abs].is_precompiled
                {
                    touch_file(project, abs);
                    continue;
                }
                sf.absolute_includes.push(abs.clone());
            }
            else
            {
                let mut found : Option<PathBuf> = None;

                for dir in &project.include_directories
                {
                    let f = core::join(dir, s);
                    if f.exists()
                    {
                        found = Some(f);
                        break;
                    }
                    found = None;
                }

                if let Some(found_path) = found
                {
                    let abs = canonicalize_or_default(&found_path);
                    // found a header that's part of PCH during regular scan: ignore it
                    if !self.is_scanning_pch && project.scanned_files.contains_key(&abs) && project.scanned_files[&abs].is_precompiled
                    {
                        touch_file(project, &abs);
                        continue;
                    }

                    sf.absolute_includes.push(abs.to_path_buf());
                    self.system_includes.insert(s.to_string(), abs.to_path_buf());
                    self.add_to_queue(&found_path, &abs);
                }
                else if self.not_found_origins.contains_key(s) == false
                {
                    let file_list = Vec::from([path.to_path_buf()]);
                    self.not_found_origins.insert(s.to_string(), file_list);
                }
                else if let Some(file_list) = self.not_found_origins.get_mut(s)
                {
                    file_list.push(path.to_path_buf());
                }
            }
        }

        // Only treat each include as done once. Since we completely ignore preprocessor, for patterns like
        // this we'd end up having same file in includes list multiple times. Let's assume that all includes use
        // pragma once or include guards and are only actually parsed just once.
        //   #if FOO
        //   #include <bar>
        //   #else
        //   #include <bar>
        //   #endif
        sf.absolute_includes.sort();
        sf.absolute_includes.dedup();
    }
}

fn touch_file(project: &mut data::Project, abs: &Path)
{
    if let Some(p) = project.scanned_files.get_mut(abs)
    {
        p.is_touched = true;
    }
}
