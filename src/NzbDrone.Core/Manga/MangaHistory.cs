using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Manga
{
    public enum MangaHistoryEventType
    {
        Grabbed = 0,
        GrabFailed = 1,
        DownloadCompleted = 2,
        DownloadFailed = 3,
        Imported = 4,
        ImportFailed = 5,
        NeedsReview = 6
    }

    public class MangaHistory : ModelBase
    {
        public int MangaId { get; set; }
        public int? MangaDownloadId { get; set; }
        public int? MangaDownloadFileId { get; set; }
        public MangaHistoryEventType EventType { get; set; }
        public DateTime Date { get; set; }
        public string Message { get; set; }
        public string ReleaseTitle { get; set; }
        public string ReleaseGuid { get; set; }
        public int IndexerId { get; set; }
        public List<int> CoveredItemIds { get; set; } = new ();
    }

    public class MangaBlocklist : ModelBase
    {
        public int MangaId { get; set; }
        public int IndexerId { get; set; }
        public string ReleaseGuid { get; set; }
        public string ReleaseTitle { get; set; }
        public string Reason { get; set; }
        public DateTime Added { get; set; }
    }
}
