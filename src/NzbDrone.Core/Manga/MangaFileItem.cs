using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Manga
{
    public class MangaFileItem : ModelBase
    {
        public int MangaFileId { get; set; }
        public int MangaItemId { get; set; }
    }
}
