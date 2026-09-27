using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Manga
{
    public class MangaFile : ModelBase
    {
        public int MangaId { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public DateTime DateAdded { get; set; }
        public string OriginalFilePath { get; set; }
        public string SceneName { get; set; }
        public string ReleaseGroup { get; set; }
        public string Language { get; set; }
        public string Source { get; set; }
        public string Quality { get; set; }
        public int IndexerFlags { get; set; }
        public string EditionLabel { get; set; }
    }
}
