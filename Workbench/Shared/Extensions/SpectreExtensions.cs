using System.Collections.Immutable;
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
}