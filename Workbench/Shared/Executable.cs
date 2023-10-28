﻿namespace Workbench.Shared;


public record Executable(string Name)
{
    public string FriendlyName => $"{Name} executable";
    public string ListName => $"{Name} executables";
}

public static class DefaultExecutables
{
    public static readonly Executable Git = new ("git");
    public static readonly Executable ClangFormat = new ("clang-format");
    public static readonly Executable ClangTidy = new ("clang-tidy");
}