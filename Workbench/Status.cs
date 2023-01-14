﻿using Workbench.CMake;

namespace Workbench;

internal static class Status
{
    private static void print_found_list(Printer printer, string name, List<Found> list)
    {
        var found = Found.first_value_or_none(list) ?? "<None>";
        printer.Info($"{name}: {found}");
        foreach (var f in list)
        {
            printer.Info($"    {f}");
        }
    }

    internal static void HandleStatus(Printer printer, CompileCommands.MainCommandSettings cc)
    {
        print_found_list(printer, "cmake", CmakeTools.ListAll(printer).ToList());

        var root = Environment.CurrentDirectory;
        printer.Info($"Root: {root}");

        var project_build_folder = CompileCommands.Utils.find_build_root(root);
        if (project_build_folder is null)
        {
            printer.error("unable to find build folder");
        }
        else
        {
            printer.Info($"Project build folder: {project_build_folder}");
        }

        var ccs = cc.get_argument_or_none_with_cwd();
        if (ccs != null)
        {
            printer.Info($"Compile commands: {ccs}");
        }
        else
        {
            printer.Info("Compile commands: <NONE>");
        }
    }
}
