///////////////////////////////////////////////////////////////////////////////////////////////////
// Parser

#![allow(unused_variables)]
#![allow(non_snake_case)]
#![allow(dead_code)]
#![allow(non_upper_case_globals)]

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
    pub SystemIncludes: Vec<String>,
    pub LocalIncludes: Vec<String>,
    pub Lines: usize,
}

fn Canonicalize(p: &Path) -> PathBuf
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
            SystemIncludes: Vec::<String>::new(),
            LocalIncludes: Vec::<String>::new(),
            Lines: 0
        }
    }

    fn ParseLine(&mut self, line: &str) -> ParseResult
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
                            self.SystemIncludes.push(substr(line, path_start, i-path_start-1).to_string());
                            return ParseResult::Ok;
                        }
                    },
                    States::Quote =>
                    {
                        if c == "\""
                        {
                            self.LocalIncludes.push(substr(line, path_start, i-path_start-1).to_string());
                            return ParseResult::Ok;
                        }
                    }
                }
            }
        }
    }
}


/// Simple parser... only looks for #include lines. Does not take #defines or comments into account.
pub fn ParseFile(fi: &Path, errors: &mut Vec<String>) -> Result
{
    let mut res = Result::new();

    if let Ok(lines) = core::read_file_to_lines(fi)
    {
        res.Lines = lines.len();
        for line in lines
        {
            if line.contains("#") && line.contains("include")
            {
                match res.ParseLine(&line)
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
    pub AllIncludes: HashSet<PathBuf>,
    pub TotalIncludeLines: usize,
    pub AllIncludedBy: HashSet<PathBuf>,
    pub TranslationUnitsIncludedBy: HashSet<PathBuf>,
    pub Analyzed: bool,
}

impl ItemAnalytics
{
    pub fn new() -> ItemAnalytics
    {
        ItemAnalytics
        {
            AllIncludes: HashSet::<PathBuf>::new(),
            TotalIncludeLines: 0,
            AllIncludedBy: HashSet::<PathBuf>::new(),
            TranslationUnitsIncludedBy: HashSet::<PathBuf>::new(),
            Analyzed: false,
        }
    }
}

pub struct Analytics
{
    pub Items: HashMap<PathBuf, ItemAnalytics>
}

pub fn Analyze(project: &data::Project) -> Analytics
{
    let mut analytics = Analytics::new();
    for (file, _) in &project.Files
    {
        analytics.Analyze(&file, project);
    }
    analytics
}

impl Analytics
{
    pub fn new() -> Analytics
    {
        Analytics
        {
            Items: HashMap::<PathBuf, ItemAnalytics>::new()
        }
    }

    fn add_trans(&mut self, inc: &Path, path: &Path)
    {
        if let Some(it) = self.Items.get_mut(&inc.to_path_buf())
        {
            it.TranslationUnitsIncludedBy.insert(path.to_path_buf());
        }
    }
    
    fn add_all_inc(&mut self, inc: &Path, path: &Path)
    {
        if let Some(it) = self.Items.get_mut(&inc.to_path_buf())
        {
            it.AllIncludedBy.insert(path.to_path_buf());
        }
    }

    fn Analyze(&mut self, path: &Path, project: &data::Project)
    {
        if let Some(ret) = self.Items.get_mut(path)
        {
            assert_eq!(ret.Analyzed, true);
            return;
        }

        let mut ret = ItemAnalytics::new();
        ret.Analyzed = true;

        let sf = &project.Files[path];
        for include in &sf.AbsoluteIncludes
        {
            if include == path { continue; }
            
            let is_tu = data::IsTranslationUnitPath(path);
            
            self.Analyze(&include, project);

            // let ai = &self.Items[&include.to_path_buf()];
            let all_includes = 
                if let Some(ai) = self.Items.get_mut(&include.to_path_buf())
                {
                    ret.AllIncludes.insert(include.to_path_buf());
                    ai.AllIncludedBy.insert(path.to_path_buf());
                    
                    if is_tu
                    {
                        ai.TranslationUnitsIncludedBy.insert(path.to_path_buf());
                    }


                    let union: HashSet<_> = ret.AllIncludes.union(&ai.AllIncludes).collect();
                    ret.AllIncludes = union.iter().map(|x| {x.to_path_buf()}).collect();

                    Some(ai.AllIncludes.clone())
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

        ret.TotalIncludeLines = ret.AllIncludes.iter().map(|f|{project.Files[f].Lines}).sum();
        
        self.Items.insert(path.to_path_buf(), ret);
    }
}



///////////////////////////////////////////////////////////////////////////////////////////////////
// Report


fn OrderByDescending(v: &mut Vec::<(&PathBuf, usize)>)
{
    // |kvp| {kvp.1}
    v.sort_by_key(|kvp| { return kvp.1});
    v.reverse();
}

fn AppendSummary(sb: &mut String, count: &[(&str, String)] )
{
    sb.push_str("<div id=\"summary\">\n");
    sb.push_str("<table class=\"summary\">\n");
    for (Key, Value) in count
    {
        sb.push_str(&format!("  <tr><th>{0}:</th> <td>{1}</td></tr>\n", Key, Value));
    }
    sb.push_str("</table>\n");
    sb.push_str("</div>\n");
}

fn AppendFileList(sb: &mut String, id: &str, header: &str, count: &[(&PathBuf, usize)])
{
    sb.push_str(&format!("<div id=\"{0}\">\n", id));
    sb.push_str(&format!("<a name=\"{0}\"></a>", id));
    sb.push_str(&format!("<h2>{0}</h2>\n\n", header));

    sb.push_str("<table class=\"list\">\n");
    for (Key, Value) in count
    {
        sb.push_str(&format!(
            "  <tr><td class=\"num\">{1}</td> <td class=\"file\">{0}</td></tr>\n", html::inspect_filename_link(Key).unwrap(), rust::num_format(*Value)
        ));
    }
    sb.push_str("</table>\n");
    sb.push_str("</div>\n");
}



fn HtmlFile(root: &Path) -> PathBuf { core::join(root, "index.html") }
fn CssFile(root: &Path) -> PathBuf { core::join(root, "header_hero_report.css") }


pub fn GenerateCss(root: &Path)
{
    core::write_string_to_file_or(&CssFile(root), _css).unwrap();
}


pub fn GenerateIndex(root: &Path, project: &data::Project, analytics: &Analytics)
{
    let mut sb = String::new();

    html::begin(&mut sb, "Report");

    // Summary
    {
        let pch_lines : usize = project.Files.iter()
            .filter(|kvp| -> bool {kvp.1.Precompiled})
            .map(|kvp| -> usize {kvp.1.Lines})
            .sum();
        let super_total_lines : usize = project.Files.iter()
            .map(|kvp| -> usize {kvp.1.Lines})
            .sum();
        let total_lines = super_total_lines - pch_lines;
        let total_parsed : usize = analytics.Items.iter()
            .filter(|kvp| {data::IsTranslationUnitPath(kvp.0) && !project.Files[kvp.0].Precompiled})
            .map(|kvp| -> usize {kvp.1.TotalIncludeLines + project.Files[kvp.0].Lines}).sum();
        let factor = total_parsed as f64 / total_lines as f64;
        let table =
        [
            ("Files", format!("{0}", rust::num_format(project.Files.len()))),
            ("Total Lines", format!("{0}", rust::num_format(total_lines))),
            ("Total Precompiled", format!("{0} (<a href=\"#pch\">list</a>)", rust::num_format(pch_lines))),
            ("Total Parsed", format!("{0}", rust::num_format(total_parsed))),
            ("Blowup Factor", format!("{0:0.00} (<a href=\"#largest\">largest</a>, <a href=\"#hubs\">hubs</a>)", factor) ),
        ];
        AppendSummary(&mut sb, &table);
    }

    // analytics.Items: HashMap<PathBuf, ItemAnalytics>

    {
        let mut most: Vec<(&PathBuf, usize)> = analytics.Items.iter()
            .map(|kvp| {(kvp.0, project.Files[kvp.0].Lines * kvp.1.TranslationUnitsIncludedBy.len())})
            .filter(|kvp| { !project.Files[kvp.0].Precompiled})
            .filter(|kvp| { kvp.1 > 0})
            .collect();
        OrderByDescending(&mut most);
        AppendFileList(&mut sb, "largest", "Biggest Contributors", &most);
    }

    {
        let mut hubs: Vec<(&PathBuf, usize)> = analytics.Items.iter()
            .map(|kvp| {(kvp.0, kvp.1.AllIncludes.len() * kvp.1.TranslationUnitsIncludedBy.len())})
            .filter(|kvp| {kvp.1 > 0})
            .collect();
        OrderByDescending(&mut hubs);
        AppendFileList(&mut sb, "hubs", "Header Hubs", &hubs);
    }

    {
        let mut pch: Vec<(&PathBuf, usize)> = project.Files.iter()
            .filter(|kvp| {kvp.1.Precompiled})
            .map(|kvp| {(kvp.0, kvp.1.Lines)})
            .collect();
        OrderByDescending(&mut pch);
        AppendFileList(&mut sb, "pch", "Precompiled Headers", &pch);
    }

    html::end(&mut sb);

    core::write_string_to_file_or(&HtmlFile(root), &sb).unwrap();
}
		const _css: &'static str = r###"

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

td.num, span.num
{
    font-family: 'Courier New', Courier, monospace;
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

#summary
{
    grid-column: 1 / span 3;
    grid-row: auto/auto;
}

"###;



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
    fn update_title(&self, new_title_: &str)
    {
        println!("{}", new_title_);
    }
    fn update_message(&self, new_message_: &str)
    {
        println!("  {}", new_message_);
    }
    fn update_count(&self, new_count_: usize) {}
    fn next_item(&self) {}
}

pub struct Scanner
{
    _queued: HashSet<PathBuf>,
    _scan_queue: Vec<PathBuf>,
    _system_includes: HashMap<String, PathBuf>,
    _scanning_pch: bool,
    pub Errors: Vec<String>,
    pub NotFound: HashSet<String>,
    pub NotFoundOrigins: HashMap<String, PathBuf>,
    pub missing_ext: counter::Counter<String, usize>,
}

impl Scanner
{
    pub fn new() -> Scanner
    {
        Scanner
        {
            _queued: HashSet::<PathBuf>::new(),
            _scan_queue: Vec::<PathBuf>::new(),
            _system_includes: HashMap::<String, PathBuf>::new(),
            _scanning_pch: false,
            Errors: Vec::<String>::new(),
            NotFound: HashSet::<String>::new(),
            NotFoundOrigins: HashMap::<String, PathBuf>::new(),
            missing_ext: counter::Counter::<String, usize>::new(),
        }
    }

    pub fn Rescan(&mut self, _project: &mut data::Project, feedback: &mut ProgressFeedback)
    {
        feedback.update_title("Scanning precompiled header...");
        for (_, sf) in &mut _project.Files
        {
            sf.Touched = false;
            sf.Precompiled = false;
        }

        // scan everything that goes into precompiled header
        self._scanning_pch = true;
        if let Some(inc) = _project.PrecompiledHeader.clone()
        {
            if inc.exists()
            {
                self.ScanFile(_project, &inc);
                while self._scan_queue.len() > 0
                {
                    let to_scan = self._scan_queue.clone();
                    self._scan_queue.clear();
                    for fi in to_scan
                    {
                        self.ScanFile(_project, &fi);
                    }
                }
                self._queued.clear();
            }
        }
        self._scanning_pch = false;

        feedback.update_title("Scanning directories...");
        for dir in &_project.ScanDirectories
        {
            feedback.update_message(&format!("{}", dir.display()));
            self.ScanDirectory(&dir, feedback);
        }

        feedback.update_title("Scanning files...");

        let mut dequeued = 0;
        
        while self._scan_queue.len() > 0
        {
            dequeued += self._scan_queue.len();
            let to_scan = self._scan_queue.clone();
            self._scan_queue.clear();
            for fi in &to_scan
            {
                feedback.update_count(dequeued + self._scan_queue.len());
                feedback.next_item();
                feedback.update_message(&format!("{}", fi.display()));
                self.ScanFile(_project, fi);
            }
        }
        self._queued.clear();
        self._system_includes.clear();

        _project.Files.retain(|_, value| {value.Touched});
    }
    
    fn ScanDirectory(&mut self, dir: &Path, feedback: &ProgressFeedback)
    {
        if let Err(_) = self.ScanDirectoryImpl(dir, feedback)
        {
            self.Errors.push(format!("Cannot descend into {}", dir.display()));
        }
    }
    fn ScanDirectoryImpl(&mut self, dir: &Path, feedback: &ProgressFeedback) -> io::Result<()>
    {
        feedback.update_message(&format!("{}", dir.display()));

        for entry in dir.read_dir()?
        {
            let e = entry?.path();
            if e.is_file()
            {
                let file = e;
                let ext = file.extension().unwrap().to_str().unwrap();
                if data::IsTranslationUnitExtension(ext)
                {
                    self.Enqueue(&file, &Canonicalize(&file));
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
                self.ScanDirectory(&subdir, feedback);
            }
        }

        Ok(())
    }

    fn Enqueue(&mut self, inc: &Path, abs: &Path)
    {
        if !self._queued.contains(abs)
        {
            self._queued.insert(abs.to_path_buf());
            self._scan_queue.push(inc.to_path_buf());
        }
    }

    fn ScanFile(&mut self, _project: &mut data::Project, p: &Path)
    {
        let path = Canonicalize(p);
        // todo(Gustav): add last scan feature!!!
        if _project.Files.contains_key(&path) // && _project.LastScan > path.LastWriteTime && !self._scanning_pch
        {
            // let mut sf = &_project.Files.get_mut(&path).unwrap();
            let mut sf : data::SourceFile = _project.Files.get(&path).unwrap().clone();
            self.PleaseScanFile(_project, &path, &mut sf);
            _project.Files.insert(path, sf);
        }
        else
        {
            let res = ParseFile(&path, &mut self.Errors);
            let mut sf = data::SourceFile::new();
            sf.Lines = res.Lines;
            sf.LocalIncludes = res.LocalIncludes;
            sf.SystemIncludes = res.SystemIncludes;
            sf.Precompiled = self._scanning_pch;
            self.PleaseScanFile(_project, &path, &mut sf);
            _project.Files.insert(path, sf);
        }
    }
    
    fn PleaseScanFile(&mut self, _project: &mut data::Project, path: &Path, sf: &mut data::SourceFile)
    {
        sf.Touched = true;
        sf.AbsoluteIncludes.clear();

        let local_dir = path.parent().unwrap();
        for s in &sf.LocalIncludes
        {
            let inc = core::join(local_dir, &s);
            let abs = Canonicalize(&inc);
            // found a header that's part of PCH during regular scan: ignore it
            if !self._scanning_pch && _project.Files.contains_key(&abs) && _project.Files[&abs].Precompiled
            {
                touch_file(_project, &abs);
                continue;
            }
            if !inc.exists()
            {
                if !sf.SystemIncludes.contains(&s)
                {
                    sf.SystemIncludes.push(s.to_string());
                }
                continue;
            }
            sf.AbsoluteIncludes.push(abs.clone());
            self.Enqueue(&inc, &abs);
            // self.Errors.push(format!("Exception: \"{0}\" for #include \"{1}\"", e.Message, s));
        }

        for s in &sf.SystemIncludes
        {
            if self._system_includes.contains_key(s)
            {
                let abs = &self._system_includes[s];
                // found a header that's part of PCH during regular scan: ignore it
                if !self._scanning_pch && _project.Files.contains_key(abs) && _project.Files[abs].Precompiled
                {
                    touch_file(_project, abs);
                    continue;
                }
                sf.AbsoluteIncludes.push(abs.clone());
            }
            else
            {
                let mut found : Option<PathBuf> = None;

                for dir in &_project.IncludeDirectories
                {
                    let f = core::join(&dir, &s);
                    if f.exists()
                    {
                        found = Some(f);
                        break;
                    }
                    found = None;
                }

                if let Some(found_path) = found
                {
                    let abs = Canonicalize(&found_path);
                    // found a header that's part of PCH during regular scan: ignore it
                    if !self._scanning_pch && _project.Files.contains_key(&abs) && _project.Files[&abs].Precompiled
                    {
                        touch_file(_project, &abs);
                        continue;
                    }

                    sf.AbsoluteIncludes.push(abs.to_path_buf());
                    self._system_includes.insert(s.to_string(), abs.to_path_buf());
                    self.Enqueue(&found_path, &abs);
                }
                else
                {
                    if self.NotFound.insert(s.to_string())
                    {
                        self.NotFoundOrigins.insert(s.to_string(), path.to_path_buf());
                    }
                }
            }
            // self.Errors.push(format!("Exception: \"{0}\" for #include <{1}>", e.Message, s));
            
        }

        // Only treat each include as done once. Since we completely ignore preprocessor, for patterns like
        // this we'd end up having same file in includes list multiple times. Let's assume that all includes use
        // pragma once or include guards and are only actually parsed just once.
        //   #if FOO
        //   #include <bar>
        //   #else
        //   #include <bar>
        //   #endif
        sf.AbsoluteIncludes.sort();
        sf.AbsoluteIncludes.dedup();
    }
}

fn touch_file(_project: &mut data::Project, abs: &Path)
{
    if let Some(p) = _project.Files.get_mut(abs)
    {
        p.Touched = true;
    }
}
