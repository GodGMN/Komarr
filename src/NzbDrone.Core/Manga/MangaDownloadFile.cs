using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Manga
{
    public enum MangaDownloadFileStatus
    {
        Ready = 0,
        ManualReview = 1,
        Unsupported = 2
    }

    public class MangaDownloadFile : ModelBase
    {
        public int MangaDownloadId { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public MangaDownloadFileStatus Status { get; set; }
        public string ParsedTitle { get; set; }
        public string MatchedAlias { get; set; }
        public List<int> CoveredItemIds { get; set; } = new ();
        public string Reason { get; set; }
        public DateTime ScannedAt { get; set; }
    }
}
