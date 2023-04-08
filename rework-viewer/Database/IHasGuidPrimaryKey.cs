using Newtonsoft.Json;
using Realms;

namespace rework_viewer.Database;

public interface IHasGuidPrimaryKey
{
    [JsonIgnore]
    [PrimaryKey]
    Guid ID { get; }
}
