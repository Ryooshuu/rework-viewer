using JetBrains.Annotations;
using osu.Framework.Testing;
using Realms;
using rework_viewer.Database;

namespace rework_viewer.Reworks;

[ExcludeFromDynamicCompile]
[Serializable]
[MapTo("Ruleset")]
public class RealmRuleset : RealmObject, IHasGuidPrimaryKey, IEquatable<RealmRuleset>
{
    [PrimaryKey]
    public Guid ID { get; set; }

    public string Name { get; set; } = string.Empty;

    public IList<RealmRework> Reworks { get; } = null!;

    [UsedImplicitly]
    public RealmRuleset()
    {
        ID = Guid.NewGuid();
    }
    
    public bool Equals(RealmRuleset? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other == null) return false;

        return ID == other.ID;
    }
}
