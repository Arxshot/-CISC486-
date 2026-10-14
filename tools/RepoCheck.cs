// Checks run by `just check` and by GitHub on every pull request:
//   1. every file and folder in game/Assets has a .meta file, and every .meta has its asset
//   2. files that belong in Git LFS (images, audio, models...) were added with LFS
//   3. C# code is formatted to match .editorconfig
// `dotnet run tools/RepoCheck.cs -- format` fixes the formatting instead.
using System.Diagnostics;

const string Assets = "game/Assets/";

Directory.SetCurrentDirectory(Git("rev-parse", "--show-toplevel").Trim());
if (!Directory.Exists(Assets))
{
    Console.WriteLine("No game/Assets yet (see README). Nothing to check.");
    return 0;
}

string[] format = ["format", "whitespace", Assets, "--folder"];
if (args.Contains("format"))
{
    return Run("dotnet", format).Code;
}

var errors = new List<string>();
var files = Git("ls-files", "-z").Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

// 1. .meta files. Unity ignores names starting with '.' or ending with '~', so those need none.
var assets = files.Where(f => f.StartsWith(Assets) && !f.Split('/').Any(n => n.StartsWith('.') || n.EndsWith('~'))).ToList();
var folders = assets.SelectMany(Folders).ToHashSet();
foreach (var path in assets.Concat(folders).Order())
{
    if (path.EndsWith(".meta"))
    {
        var asset = path[..^5];
        if (!files.Contains(asset) && !folders.Contains(asset))
        {
            errors.Add($"{path} has no asset. Delete it, or commit the asset (empty folders can't be committed).");
        }
    }
    else if (!files.Contains(path + ".meta"))
    {
        errors.Add($"{path} has no .meta file. Open the project in Unity, then commit the .meta it creates.");
    }
}

// 2. Git LFS. A file added with LFS is stored as a small text "pointer".
foreach (var path in Git("ls-files", "-z", ":(attr:filter=lfs)").Split('\0', StringSplitOptions.RemoveEmptyEntries))
{
    if (!Git("cat-file", "-p", ":" + path).StartsWith("version https://git-lfs"))
    {
        errors.Add($"{path} was added without Git LFS. Run `just setup`, then `git rm --cached \"{path}\"` and `git add \"{path}\"`.");
    }
}

// 3. Formatting.
if (Run("dotnet", [.. format, "--verify-no-changes"]).Code != 0)
{
    errors.Add("C# formatting is off. Run `just format`, then `git add` the changes.");
}

foreach (var error in errors)
{
    Console.WriteLine($"error: {error}");
}
Console.WriteLine(errors.Count == 0 ? "All checks passed." : $"{errors.Count} problem(s) found.");
return errors.Count == 0 ? 0 : 1;

// "game/Assets/A/B/c.png" -> "game/Assets/A", "game/Assets/A/B"
static IEnumerable<string> Folders(string path)
{
    for (var i = path.IndexOf('/', Assets.Length); i >= 0; i = path.IndexOf('/', i + 1))
    {
        yield return path[..i];
    }
}

static string Git(params string[] arguments) => Run("git", arguments, capture: true).Output;

static (int Code, string Output) Run(string program, string[] arguments, bool capture = false)
{
    var start = new ProcessStartInfo(program, arguments) { RedirectStandardOutput = capture };
    using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {program}.");
    var output = capture ? process.StandardOutput.ReadToEnd() : "";
    process.WaitForExit();
    if (capture && process.ExitCode != 0)
    {
        throw new InvalidOperationException($"{program} {string.Join(' ', arguments)} failed.");
    }
    return (process.ExitCode, output);
}
