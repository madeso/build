using System.Collections.Immutable;
using System.Data;
using Spectre.Console;

namespace Workbench.Shared.Extensions;

public static class SpectreExtensions
{
    public static async Task RunArrayAsync<T>(this Progress progress, ImmutableArray<T> arr, Func<T, Task<string>> action)
    {
        if (arr.Length == 1)
        {
            await action(arr[0]);
        }
        else if(arr.Length > 1)
        {
            var initial_message = await action(arr[0]);
            await progress.StartAsync(async ctx =>
            {
                var index = 1;
                var task = ctx.AddTask(to_task_message(initial_message), maxValue: arr.Length-1);

                while (!ctx.IsFinished)
                {
                    // Simulate some work
                    var new_title = await action(arr[index]);
                    task.Description = to_task_message(new_title);

                    // Increment
                    task.Increment(1);
                    index += 1;
                }
            });
        }

        return;

        static string to_task_message(string message)
            => message.Length == 0 ? "Waiting..." : message;
    }


    public static async Task<List<TY>> MapArrayAsync<T, TY>(this Progress progress, ImmutableArray<T> arr, Func<T, Task<(string, TY)>> action)
    {
        var ret = new List<TY>();
        if (arr.Length == 1)
        {
            var (_, x) = await action(arr[0]);
            ret.Add(x);
        }
        else if(arr.Length > 1)
        {
            var (initial_message, initial_y) = await action(arr[0]);
            ret.Add(initial_y);

            await progress.StartAsync(async ctx =>
            {
                var index = 1;
                var task = ctx.AddTask(to_task_message(initial_message), maxValue: arr.Length-1);

                while (!ctx.IsFinished)
                {
                    // Simulate some work
                    var (new_title, new_y) = await action(arr[index]);
                    task.Description = to_task_message(new_title);
                    ret.Add(new_y);

                    // Increment
                    task.Increment(1);
                    index += 1;
                }
            });
        }

        return ret;

        static string to_task_message(string message)
            => message.Length == 0 ? "Waiting..." : message;
    }

    internal static Progress Progress()
    {
        return AnsiConsole.Progress()
            .AutoClear(true)
            .HideCompleted(true)
            .Columns(
                new SpinnerColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new TaskDescriptionColumn()
            );
    }
}