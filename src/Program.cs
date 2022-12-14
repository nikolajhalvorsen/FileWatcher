using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using static System.IO.NotifyFilters;

namespace FileWatcher;

internal class Program
{
    private static Dictionary<string, long> MaxFileSizes { get; } = new();

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Watches a folder for file changes.")
        {
            TreatUnmatchedTokensAsErrors = true
        };

        rootCommand.AddOption(
            new Option<string>(
                new[] { "--folder", "-fo" },
                "The folder to watch.")
            {
                IsRequired = true,
                Name = "Folder"
            });
        rootCommand.AddOption(
            new Option<string>(
                new[] { "--filter", "-fi" },
                description: "The filter to watch.",
                getDefaultValue: () => "*.*")
            {
                Name = "Filter"
            });
        rootCommand.AddOption(
            new Option<bool>(
                new[] { "--watchChanged", "-ch" },
                description: "Whether or not to watch for changes.",
                getDefaultValue: () => true)
            {
                Name = "WatchChanged"
            });
        rootCommand.AddOption(
            new Option<bool>(
                new[] { "--watchCreated", "-cr" },
                description: "Whether or not to watch for creates.",
                getDefaultValue: () => false)
            {
                Name = "WatchCreated"
            });
        rootCommand.AddOption(
            new Option<bool>(
                new[] { "--watchDeleted", "-de" },
                description: "Whether or not to watch for deletes.",
                getDefaultValue: () => false)
            {
                Name = "WatchDeleted"
            });
        rootCommand.AddOption(
            new Option<bool>(
                new[] { "--watchRenamed", "-re" },
                description: "Whether or not to watch for renames.",
                getDefaultValue: () => false)
            {
                Name = "WatchRenamed"
            });
        rootCommand.AddOption(
            new Option<bool>(
                new[] { "--includeSubdirectories", "-is" },
                description: "Whether or not to watch subdirectories.",
                getDefaultValue: () => false)
            {
                Name = "IncludeSubdirectories"
            });


        rootCommand.Handler = CommandHandler.Create<string, string, bool, bool, bool, bool, bool>(Watch);

        return await rootCommand.InvokeAsync(args);
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed) return;

        if (!File.Exists(e.FullPath))
        {
            return;
        }

        var lengthB = new FileInfo(e.FullPath).Length;
        var lengthKb = lengthB / 1024;
        var lengthMb = lengthKb / 1024;
        var lengthGb = lengthMb / 1024;

        if (!MaxFileSizes.ContainsKey(e.FullPath))
        {
            MaxFileSizes.Add(e.FullPath, lengthB);
            Console.WriteLine(
                $"{DateTime.Now:s} New size of {e.FullPath}: {lengthB} B, {lengthKb} kB, {lengthMb} MB, {lengthGb} GB");
        }
        else
        {
            if (MaxFileSizes[e.FullPath] < lengthB)
            {
                MaxFileSizes[e.FullPath] = lengthB;
                Console.WriteLine(
                    $"{DateTime.Now:s} Changed: {e.FullPath}; Size: {lengthB} B, {lengthKb} kB, {lengthMb} MB, {lengthGb} GB");
            }
        }
    }

    private static void OnCreated(object sender, FileSystemEventArgs e)
    {
        var value = $"Created: {e.FullPath}";
        Console.WriteLine(value);
    }

    private static void OnDeleted(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"Deleted: {e.FullPath}");
    }

    private static void OnError(object sender, ErrorEventArgs e)
    {
        PrintException(e.GetException());
    }

    private static void OnRenamed(object sender, RenamedEventArgs e)
    {
        Console.WriteLine("Renamed:");
        Console.WriteLine($"    Old: {e.OldFullPath}");
        Console.WriteLine($"    New: {e.FullPath}");
    }

    private static void PrintException(Exception? ex)
    {
        while (true)
        {
            if (ex == null) return;
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine("Stacktrace:");
            Console.WriteLine(ex.StackTrace);
            Console.WriteLine();
            ex = ex.InnerException;
        }
    }

    private static void PrintFileSizes(Dictionary<string, long> dict)
    {
        if (!dict.Keys.Any())
        {
            Console.WriteLine("No files.");
        }

        foreach (var key in dict.Keys)
        {
            var lengthB = dict[key];
            var lengthKb = lengthB / 1024;
            var lengthMb = lengthKb / 1024;
            var lengthGb = lengthMb / 1024;

            Console.WriteLine($"{key}: {lengthB} B, {lengthKb} kB, {lengthMb} MB, {lengthGb} GB");
        }
    }

    // ReSharper disable FlagArgument
    // ReSharper disable once MethodTooLong
    // ReSharper disable once TooManyArguments
    private static void Watch(
        string folder,
        string filter,
        bool watchChanged,
        bool watchCreated,
        bool watchDeleted,
        bool watchRenamed,
        bool includeSubdirectories)
    {
        Console.WriteLine($"Folder: {folder}");
        Console.WriteLine($"Filter: {filter}");
        Console.WriteLine($"Include Subdirectories: {includeSubdirectories}");
        Console.WriteLine();

        using var watcher = new FileSystemWatcher(folder);

        // ReSharper disable once ComplexConditionExpression
        watcher.NotifyFilter =
            FileName |
            DirectoryName |
            Attributes |
            Size |
            LastWrite |
            LastAccess |
            CreationTime |
            Security;

        if (watchChanged) watcher.Changed += OnChanged;
        if (watchCreated) watcher.Created += OnCreated;
        if (watchDeleted) watcher.Deleted += OnDeleted;
        if (watchRenamed) watcher.Renamed += OnRenamed;

        watcher.EnableRaisingEvents = true;
        watcher.Error += OnError;
        watcher.Filter = filter;
        watcher.IncludeSubdirectories = includeSubdirectories;

        var fileNames = Directory.GetFiles(
            watcher.Path,
            watcher.Filter,
            includeSubdirectories
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly).ToList();

        var initialFileSizes = fileNames.Select(x => new { FileName = x, FileSize = new FileInfo(x).Length })
            .ToDictionary(x => x.FileName, x => x.FileSize);

        Console.WriteLine("# Initial File Sizes");

        PrintFileSizes(initialFileSizes);

        Console.WriteLine("Press enter to exit.");
        Console.ReadLine();

        if (!MaxFileSizes.Keys.Any())
        {
            Console.WriteLine("No changes.");
            return;
        }

        Console.WriteLine("# Maximum File Sizes");

        PrintFileSizes(MaxFileSizes);
    }
}