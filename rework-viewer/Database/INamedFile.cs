using rework_viewer.Models;

namespace rework_viewer.Database;

/// <summary>
/// Represents a join model which gives a filename and scope to a <see cref="File"/>
/// </summary>
public interface INamedFile
{
    string Filename { get; set; }
    
    RealmFile File { get; set; }
}
