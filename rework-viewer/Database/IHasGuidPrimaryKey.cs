using Realms;

namespace rework_viewer.Database;

public interface IHasGuidPrimaryKey
{
    [PrimaryKey]
    Guid ID { get; }
}
