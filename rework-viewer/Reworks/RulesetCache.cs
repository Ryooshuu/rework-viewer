using System.Runtime.Loader;
using osu.Framework.IO.Stores;
using osu.Framework.Lists;
using osu.Framework.Logging;
using osu.Framework.Testing;
using osu.Game.Rulesets;
using rework_viewer.Database;
using rework_viewer.Extensions;
using LogLevel = osu.Framework.Logging.LogLevel;

namespace rework_viewer.Reworks;

public class RulesetCache
{
    private readonly WeakList<RulesetBackedRework> rulesetCache = new();

    private readonly IResourceStore<byte[]> files;

    public RulesetCache(IResourceStore<byte[]> files)
    {
        this.files = files;
    }

    public void Invalidate(RealmRework rework)
    {
        lock (rulesetCache)
        {
            var ruleset = rulesetCache.FirstOrDefault(r => r.Rework.Equals(rework));

            if (ruleset != null)
            {
                Logger.Log($"Invalidating ruleset backed rework cache for {rework}");
                unloadRework(ruleset);
                OnInvalidated?.Invoke(ruleset);
            }
        }
    }

    public event Action<RulesetBackedRework>? OnInvalidated;

    public virtual Ruleset GetRuleset(RealmRework rework)
    {
        lock (rulesetCache)
        {
            var ruleset = rulesetCache.FirstOrDefault(r => r.Rework.Equals(rework));

            if (ruleset != null)
                return ruleset.Ruleset;

            rework = rework.Detach();

            rulesetCache.Add(ruleset = new RulesetBackedRework(rework, files));

            return ruleset.Ruleset;
        }
    }

    private void unloadRework(RulesetBackedRework backedRework)
    {
        rulesetCache.Remove(backedRework);
        backedRework.Dispose();
    }

    [ExcludeFromDynamicCompile]
    public class RulesetBackedRework : IDisposable
    {
        public readonly RealmRework Rework;
        public Ruleset Ruleset = null!;

        private readonly IResourceStore<byte[]> files;
        private AssemblyLoadContext? assemblyLoadContext;

        public RulesetBackedRework(RealmRework rework, IResourceStore<byte[]> files)
        {
            this.files = files;
            Rework = rework.Detach();

            loadRuleset();
        }

        private void loadRuleset()
        {
            assemblyLoadContext = new AssemblyLoadContext("rulesets", true);

            var dllPath = Rework.GetFile(Rework.DllFile)!.File.GetStoragePath();
            var stream = files.GetStream(dllPath);

            var assembly = assemblyLoadContext.LoadFromStream(stream);

            try
            {
                var rulesetType = assembly.GetTypes().First(t => t.IsPublic && t.IsSubclassOf(typeof(Ruleset)));

                if (Activator.CreateInstance(rulesetType) is not Ruleset rulesetClass)
                    throw new NullReferenceException(assembly.GetName().Name!.Split('.').Last());

                if (Ruleset.CURRENT_RULESET_API_VERSION != rulesetClass.RulesetAPIVersionSupported)
                    throw new Exception($"Current ruleset API version is too new to support this ruleset. {rulesetClass.RulesetAPIVersionSupported} -> {Ruleset.CURRENT_RULESET_API_VERSION}");
                
                Ruleset = rulesetClass;
            }
            catch (Exception e)
            {
                logFailedLoad(assembly.GetName().Name!.Split('.').Last(), e);
            }
        }

        private void logFailedLoad(string name, Exception exception)
        {
            Logger.Log($"Could not load ruleset \"{name}\".", level: LogLevel.Error);
            Logger.Log($"Ruleset load failed: {exception}");
        }

        public void Dispose()
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (Ruleset == null)
                return;

            assemblyLoadContext?.Unload();
            Ruleset = null!;
        }
    }
}
