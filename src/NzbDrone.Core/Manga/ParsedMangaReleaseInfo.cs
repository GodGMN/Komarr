using System.Collections.Generic;

namespace NzbDrone.Core.Manga
{
    public enum MangaReleaseUnitType
    {
        Unknown = 0,
        Volume = 1,
        Chapter = 2
    }

    public enum MangaParseConfidence
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public class ParsedMangaReleaseInfo
    {
        public string RawTitle { get; set; }
        public string ParsedTitle { get; set; }
        public MangaReleaseUnitType UnitType { get; set; }
        public string StartNumberText { get; set; }
        public string EndNumberText { get; set; }
        public decimal? StartNumber { get; set; }
        public decimal? EndNumber { get; set; }
        public bool IsPack { get; set; }
        public bool IsComplete { get; set; }
        public string EditionHint { get; set; }
        public string Language { get; set; }
        public string Source { get; set; }
        public string ReleaseGroup { get; set; }
        public int? Year { get; set; }
        public MangaParseConfidence Confidence { get; set; } = MangaParseConfidence.Low;
        public List<string> Warnings { get; set; } = new ();
    }
}
