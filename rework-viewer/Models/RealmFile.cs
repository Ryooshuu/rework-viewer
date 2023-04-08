using osu.Framework.Testing;
using Realms;
using rework_viewer.IO;

namespace rework_viewer.Models;

[ExcludeFromDynamicCompile]
[MapTo("File")]
public class RealmFile : RealmObject, IFileInfo
{
    [PrimaryKey]
    public string Hash { get; set; } = string.Empty;
}
