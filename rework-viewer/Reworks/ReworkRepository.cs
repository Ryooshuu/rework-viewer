using System.Text.RegularExpressions;
using CliWrap;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using rework_viewer.Database;
using rework_viewer.Extensions;

namespace rework_viewer.Reworks;

public class ReworkRepository
{
    private readonly Storage storage;
    private readonly RealmAccess realm;

    public ReworkRepository(Storage storage, RealmAccess realm)
    {
        this.storage = storage;
        this.realm = realm;
    }

    /// <summary>
    /// Clones, checkouts, and builds a given repository for it to be used as a rework.
    /// </summary>
    /// <remarks>
    /// Since the osu! codebase is large, this will be a massive blocking call, and will take up a lot of space until it is built.
    /// It is best to run this method under a background task and left to wait until it is finished.
    /// </remarks>
    /// <param name="url">The URL to clone the repository from. Can also be directed to a branch or commit.</param>
    /// <param name="type">The ruleset to build.</param>
    /// <param name="populate">Populates data inside the database.</param>
    public async Task<RealmRework> GetRework(string url, RulesetType type, Action<RealmRework>? populate = null)
    {
        var repo = await cloneRepo(url);
        var dll = await buildRepo(repo, type);

        var manager = new ReworkManager(storage, realm);
        RealmRework rework = null!;

        manager.PresentImport = imports =>
        {
            foreach (var import in imports)
            {
                if (populate != null)
                    import.PerformWrite(populate);

                rework = import.Value.Detach();
            }
        };
        
        await manager.Import(dll);
        cleanup(repo);

        return rework;
    }

    private async Task<string> cloneRepo(string url)
    {
        var githubRegex = new Regex(@"^(https:\/\/github\.com\/[^\/]+\/[^\/]+)\/?([^\/]+)?\/?([^\/]+)?(\/commits\/(.*))?$");

        var match = githubRegex.Match(url);
        if (!match.Success)
            throw new InvalidOperationException($"Couldn't clone {url} as it's an invalid URL.");

        var repo = match.Groups[1].Value;
        var target = match.Groups[2].Value;
        var targetInfo = match.Groups[3].Value;
        var prCommit = match.Groups[4].Value;

        var targetDir = Path.Combine(Path.GetTempPath(), @"rework-viewer", Guid.NewGuid().ToString());

        Logger.Log($"Cloning {url}...");

        try
        {
            var cloneResult = await Cli.Wrap("git")
                                 .WithArguments(new[] { "clone", "--filter=tree:0", repo, targetDir })
                                 .WithValidation(CommandResultValidation.None)
                                 .ExecuteWithLogging();

            if (cloneResult.ExitCode != 0)
                throw new Exception("Cloning failed");

            switch (target)
            {
                case "pull":
                    var prCheckoutResult = await Cli.Wrap("gh")
                                              .WithArguments(new[] { "pr", "checkout", targetInfo })
                                              .WithWorkingDirectory(targetDir)
                                              .ExecuteWithLogging();

                    if (prCheckoutResult.ExitCode != 0)
                        throw new Exception($"Unable to check out PR: {targetInfo}");

                    if (!string.IsNullOrEmpty(prCommit))
                    {
                        var prCheckoutResult2 = await Cli.Wrap("git")
                                                   .WithArguments(new[] { "checkout", prCommit })
                                                   .WithWorkingDirectory(targetDir)
                                                   .ExecuteWithLogging();

                        if (prCheckoutResult2.ExitCode != 0)
                            throw new Exception($"Unable to check out PR commit: {prCommit}");
                    }
                    
                    break;
                
                case "commit":
                case "tree":
                    var checkoutResult = await Cli.Wrap("git")
                                            .WithArguments(new[] { "checkout", targetInfo })
                                            .WithWorkingDirectory(targetDir)
                                            .ExecuteWithLogging();

                    if (checkoutResult.ExitCode != 0)
                        throw new Exception($"Couldn't checkout into {target}: {targetInfo}");
                    
                    break;
                
                case "":
                    break;
                
                default:
                    throw new Exception($"Not a valid repository url: {url}");
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Couldn't clone repository.");
            cleanup(targetDir);
            throw;
        }

        return targetDir;
    }

    private async Task<string> buildRepo(string path, RulesetType type)
    {
        var ruleset = $"osu.Game.Rulesets.{type.GetDescription()}";

        await Cli.Wrap("dotnet")
           .WithArguments(new[] { "build", ruleset, "-c", "Release" })
           .WithWorkingDirectory(path)
           .ExecuteWithLogging();

        var dllPath = Path.Combine(path, ruleset, "bin", "Release");
        var netVersion = Directory.GetDirectories(dllPath)[0];
        dllPath = Path.Combine(dllPath, netVersion, ruleset + ".dll");

        return dllPath;
    }

    private void cleanup(string path)
    {
        foreach (string subdirectory in Directory.EnumerateDirectories(path))
        {
            cleanup(subdirectory);
        }

        foreach (string fileName in Directory.EnumerateFiles(path))
        {
            var fileInfo = new FileInfo(fileName)
            {
                Attributes = FileAttributes.Normal
            };
            fileInfo.Delete();
        }

        Directory.Delete(path);
    }
}
