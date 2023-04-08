using JetBrains.Annotations;
using Newtonsoft.Json;
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

    [Ignored]
    public RulesetType Type
    {
        get => (RulesetType) TypeInt;
        set => TypeInt = (int) value;
    }
    
    [JsonIgnore]
    public int TypeInt { get; set; } = (int) RulesetType.Standard;

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
