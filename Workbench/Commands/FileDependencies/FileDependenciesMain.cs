﻿using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Config;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.FileDependencies;


internal static class FileDeps
{
    public record ConnectionEntry(Fil File)
    {
        public int Commits { get; set; }
        public ColCounter<Fil> Counter { get; } = new();

        // count is from counter
        public float CalculateFactor(int count)
        {
            return count / (float)Commits;
        }
    }

    // todo(Gustav): is this working correctly???
    public static async Task<Dictionary<Fil, ConnectionEntry>> ExtractGraphData(Executor exec, Vfs vfs, Dir cwd, Fil git_path, Log log)
    {
        // todo(Gustav): make a list and move to argument
        var external_folder = cwd.GetSubDirs("external");

        // collect commits
        var commits = (await Shared.Git.LogAsync(exec, cwd, git_path, cwd).ToListAsync()).ToImmutableArray();
        var latest_commit = commits[0].Hash;

        // collect files (collect is slow, so cache!
        var cache = JsonUtil.GetOrNull<GitFile>(vfs, GitFile.GetPath(cwd), log);
        var collected_before = cache?.File;
        if (collected_before == null || cache == null || cache.LatestCommit != latest_commit)
        {
            AnsiConsole.WriteLine("Collecting git history...");
            var collected_before_git = await SpectreExtensions.Progress().MapArrayAsync(commits, async commit =>
            {
                var files = await Shared.Git.FilesInCommitAsync(exec, cwd, git_path, cwd, commit.Hash);
                var ret = new { Commit = commit, Files = files };
                return ($"Listing files for {commit.Hash}...", ret);
            });

            var new_files = collected_before_git.ToDictionary(
                x => x.Commit.Hash,
                x => x.Files.Select(f => f.File).ToList()
            );
            collected_before = new_files;
            cache = new GitFile
            {
                LatestCommit = latest_commit,
                File = new_files
            };

            AnsiConsole.WriteLine("Saving git cache");
            JsonUtil.Save(vfs, GitFile.GetPath(cwd), cache);
        }
        else
        {
            AnsiConsole.WriteLine("Loaded git cache");
        }

        var numbers_before = collected_before.Values.Select(c => c.Count).Sum();
        AnsiConsole.MarkupLineInterpolated($"Found {collected_before.Count} commits yielding {numbers_before} files!");

        // remove files that are no longer valid and remove commits that no longer have files
        var collected = collected_before
                .SelectMany(pair => pair.Value.Select(y => new
                {
                    Commit = pair.Key,
                    File = y
                }))
                .Where(x => x.File.Exists(vfs))
                .Where(f => f.File.IsInFolder(external_folder) == false)
                .GroupBy(x => x.Commit, (_, x) => x.ToImmutableArray())
                .Select(x => new { x[0].Commit, Files = x.Select(y => y.File).ToImmutableArray() })
                .ToImmutableArray()
            ;

        // print new status
        // bug: wont track renames
        var numbers = collected_before.Select(c => c.Value.Count).Sum();
        AnsiConsole.MarkupLineInterpolated($"Found {collected.Length} commits yielding {numbers} files!");

        // foreach file, foreach commit, ColCount of other files, get probability given counts of commits
        AnsiConsole.WriteLine("Counting commits...");
        var counters = collected
            .SelectMany(x => x.Files)
            .ToHashSet()
            .ToDictionary(x => x, file => new ConnectionEntry(file));
        foreach (var ent in collected)
        {
            var files = ent.Files.Distinct().ToImmutableArray();
            foreach (var f in files)
            {
                counters[f].Commits += 1;
            }

            foreach (var (from, to) in files.Permutation())
            {
                counters[from].Counter.AddOne(to);
            }
        }

        return counters;
    }
}

internal sealed class ListInfoCommand : AsyncCommand<ListInfoCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [CommandOption("--min-commits")]
        [DefaultValue(2)]
        public int MinCommits { get; init; }
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();
        var paths = new Config.RealPaths();
        var exec = new SystemExecutor();

        return await CliUtil.PrintErrorsAtExitAsync(async log => await Run(exec, vfs, paths, cwd, arg, log));
    }

    private static async Task<int> Run(Executor exec, Vfs vfs, Config.Paths paths, Dir cwd, Arg arg, Log log)
    {
        var git_path = paths.GetGitExecutable(vfs, cwd, log);
        if (git_path == null)
        {
            return -1;
        }

        var counters = await FileDeps.ExtractGraphData(exec, vfs, cwd, git_path, log);

        var commits = counters.Values
            .Where(c => c.Commits >= arg.MinCommits)
            .OrderBy(x => x.Commits)
            .GroupBy(x => x.Commits, (count, b) => new {Count=count, Files=b.Count()})
            .OrderByDescending(x => x.Count)
            ;

        AnsiConsole.Write(new BarChart()
            .Width(60)
            .Label("[green bold underline]Number of times a files has appeared in a commit[/]")
            .CenterLabel()
            .AddItems(commits, item => new BarChartItem(
                item.Count.ToString(), item.Files, Color.Yellow)));

        // todo(Gustav): display factor
        const int FACTOR_GROUP = 100;
        var factors = counters.Values
            .Where(c => c.Commits >= arg.MinCommits)
            .SelectMany(x => x.Counter.Items.Select(kvp => new {From=x.File, To=kvp.Key, Factor=x.CalculateFactor(kvp.Value)}))
            .Select(f => new {f.From, f.To, Factor=(int)Math.Floor(f.Factor * FACTOR_GROUP)})
            .OrderBy(x => x.Factor)
            .GroupBy(x=>x.Factor, (factor, links) => new {Factor=(float) factor/FACTOR_GROUP, Links=links.ToImmutableArray()})
            .OrderByDescending(x => x.Factor)
            .ToImmutableArray()
            ;
        AnsiConsole.Write(new BarChart()
            .Width(60)
            .Label("[green bold underline]Factors[/]")
            .CenterLabel()
            .AddItems(factors, item => new BarChartItem(
                (item.Factor * 100).ToString(CultureInfo.InvariantCulture), item.Links.Length, Color.Blue)));

        foreach (var f in factors.Where(x => x.Factor > 1))
        {
            AnsiConsole.WriteLine($"Factor: {f.Factor}");
            foreach (var e in f.Links.Take(10))
            {
                AnsiConsole.WriteLine($"{e.From} -> {e.To} ({e.Factor})");
            }
            AnsiConsole.WriteLine();
        }

        return 0;
    }
}

internal sealed class GitFilesCommand : AsyncCommand<GitFilesCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Output file")]
        [CommandArgument(2, "[output]")]
        public string OutputFile { get; init; } = string.Empty;

        [CommandOption("--min-commits")]
        [DefaultValue(2)]
        public int MinCommits { get; init; }

        [CommandOption("--min-factor")]
        [DefaultValue(0.8)]
        public float MinFactor { get; init; }
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var cwd = Dir.CurrentDirectory;
        var paths = new Config.RealPaths();
        var vfs = new VfsDisk();
        var exec = new SystemExecutor();

        return await CliUtil.PrintErrorsAtExitAsync(async log => await Run(exec, vfs, paths, cwd, arg, log));
    }

    private static async Task<int> Run(Executor exec, Vfs vfs, Config.Paths paths, Dir cwd, Arg arg, Log log)
    {
        var git_path = paths.GetGitExecutable(vfs, cwd, log);
        if (git_path == null)
        {
            return -1;
        }

        var counters = await FileDeps.ExtractGraphData(exec, vfs, cwd, git_path, log);

        // draw graphviz with links with probability count, remove nodes with no links
        AnsiConsole.WriteLine("Collecting graphviz");
        var gv = new Graphviz();
        foreach (var (file_path, targets) in counters)
        {
            if (targets.Commits < arg.MinCommits)
            {
                continue;
            }

            var src = file_path;
            foreach(var (dst, count) in targets.Counter.Items)
            {
                Debug.Assert(targets.Commits >= 1);
                var factor = targets.CalculateFactor(count);

                var src_rel = src.GetRelative(cwd);
                var dst_rel = dst.GetRelative(cwd);

                if (factor < arg.MinFactor)
                {
                    continue;
                }

                var src_node = gv.GetOrCreate(src_rel);
                var dst_node = gv.GetOrCreate(dst_rel);
                gv.AddEdge(src_node, dst_node);
            }
        }

        var gvf = Cli.ToSingleFile(cwd, arg.OutputFile, "file-dependencies.html");
        AnsiConsole.WriteLine($"Writing graphviz to {gvf} with {gv.NodeCount} nodes and {gv.EdgeCount} edges");
        await gv.SmartWriteFileAsync(exec, vfs, paths, cwd, gvf, log);

        AnsiConsole.WriteLine("Done!");
        return 0;
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, root =>
        {
            root.SetDescription("Find connection between files by analyzing git commits");
            
            root.AddCommand<ListInfoCommand>("info").WithDescription("Print info to help reduce irrelevant links");
            root.AddCommand<GitFilesCommand>("write-graph").WithDescription("Write a doxygen graph");
        });
    }
}
