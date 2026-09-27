using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Manga
{
    public enum MangaTrackingMode
    {
        Volume = 0,
        Chapter = 1
    }

    public class Manga : ModelBase
    {
        public int AniListId { get; set; }
        public int? MalId { get; set; }
        public string TitleRomaji { get; set; }
        public string TitleEnglish { get; set; }
        public string TitleNative { get; set; }
        public string PreferredTitle { get; set; }
        public string CleanTitle { get; set; }
        public List<string> Synonyms { get; set; } = new ();
        public string Description { get; set; }
        public string Status { get; set; }
        public string Format { get; set; }
        public string CountryOfOrigin { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? AniListChapterCount { get; set; }
        public int? AniListVolumeCount { get; set; }
        public string CoverUrl { get; set; }
        public string BannerUrl { get; set; }
        public MangaTrackingMode TrackingMode { get; set; } = MangaTrackingMode.Volume;
        public bool Monitored { get; set; }
        public bool MonitorFutureItems { get; set; }
        public string RootFolderPath { get; set; }
        public string Path { get; set; }
        public int QualityProfileId { get; set; }
        public int? MetadataProfileId { get; set; }
        public List<int> Tags { get; set; } = new ();
        public DateTime? LastInfoSync { get; set; }
        public DateTime? LastSearchTime { get; set; }
        public DateTime Added { get; set; }
        public DateTime? AniListUpdatedAt { get; set; }
    }
}
