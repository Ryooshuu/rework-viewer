using System.Text.Json.Serialization;
using osu.Framework.Testing;
using Realms;
using rework_viewer.Database;
using rework_viewer.Models;

namespace rework_viewer.Reworks;

[ExcludeFromDynamicCompile]
[MapTo("Rework")]
public class RealmRework : RealmObject, IHasGuidPrimaryKey, IHasRealmFiles, ISoftDelete, IHasNamedFiles, IEquatable<RealmRework>
{
    [PrimaryKey]
    public Guid ID { get; set; }
    
    public DateTimeOffset DateAdded { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // TODO: Maybe set this to an actual user one day?
    public string Author { get; set; } = string.Empty;

    public string Changelog { get; set; } = string.Empty;

    public string DllFile { get; set; } = null!;

    public IList<RealmNamedFileUsage> Files { get; } = null!;

    public bool DeletePending { get; set; }

    public string Hash { get; set; } = string.Empty;

    [JsonIgnore]
    public bool Hidden { get; set; }

    /// <summary>
    /// Whether deleting this rework should be prohibited.
    /// Usually for reworks that are required to be present. (core)
    /// </summary>
    public bool Protected { get; set; }

    public RealmRework()
    {
        ID = Guid.NewGuid();
    }
    
    public bool Equals(RealmRework? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other == null) return false;

        return ID == other.ID;
    }

    IEnumerable<INamedFileUsage> IHasNamedFiles.Files => Files;
}
