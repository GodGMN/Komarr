using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;

namespace NzbDrone.Core.Manga
{
    public enum MangaDownloadStatus
    {
        Pending = 0,
        Sent = 1,
        Completed = 2,
        Failed = 3
    }

    public class MangaDownload : ModelBase
    {
        public int MangaId { get; set; }
        public int? RequestedItemId { get; set; }
        public List<int> CoveredItemIds { get; set; } = new ();
        public int IndexerId { get; set; }
        public string Indexer { get; set; }
        public string ReleaseGuid { get; set; }
        public string ReleaseTitle { get; set; }
        public DownloadProtocol Protocol { get; set; }
        public int DownloadClientId { get; set; }
        public string DownloadClient { get; set; }
        public string DownloadId { get; set; }
        public MangaDownloadStatus Status { get; set; }
        public string Error { get; set; }
        public DateTime Added { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
}
