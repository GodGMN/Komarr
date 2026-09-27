using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.Manga
{
    public interface IMangaDownloadFileIdentifier
    {
        List<MangaDownloadFile> Identify(MangaDownload download, IEnumerable<string> paths);
    }

    public class MangaDownloadFileIdentifier : IMangaDownloadFileIdentifier
    {
        private static readonly HashSet<string> SupportedExtensions = new (StringComparer.OrdinalIgnoreCase)
        {
            ".cbz", ".cbr", ".epub", ".pdf", ".zip"
        };

        private readonly IMangaService _manga;
        private readonly IMangaReleaseParser _parser;
        private readonly IMangaReleaseMatcher _matcher;
        private readonly IDiskProvider _disk;

        public MangaDownloadFileIdentifier(IMangaService manga, IMangaReleaseParser parser, IMangaReleaseMatcher matcher, IDiskProvider disk)
        {
            _manga = manga;
            _parser = parser;
            _matcher = matcher;
            _disk = disk;
        }

        public List<MangaDownloadFile> Identify(MangaDownload download, IEnumerable<string> paths)
        {
            var manga = _manga.Find(download.MangaId) ?? throw new KeyNotFoundException("Manga was not found.");
            var knownItems = _manga.GetItems(manga.Id).ToList();
            var titles = _manga.All().ToList();
            var results = new List<MangaDownloadFile>();

            foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var result = new MangaDownloadFile
                {
                    MangaDownloadId = download.Id,
                    Path = path,
                    Size = _disk.GetFileSize(path),
                    Status = MangaDownloadFileStatus.ManualReview,
                    ScannedAt = DateTime.UtcNow
                };
                results.Add(result);

                if (!SupportedExtensions.Contains(Path.GetExtension(path)))
                {
                    result.Status = MangaDownloadFileStatus.Unsupported;
                    result.Reason = "Unsupported file type; only CBZ, CBR, EPUB, PDF, and ZIP are considered.";
                    continue;
                }

                var parsed = _parser.Parse(Path.GetFileName(path));
                result.ParsedTitle = parsed.ParsedTitle;
                if (parsed.UnitType == MangaReleaseUnitType.Unknown)
                {
                    result.Reason = parsed.Warnings.FirstOrDefault() ?? "Volume or chapter could not be identified from this filename.";
                    continue;
                }

                var expectedUnit = manga.TrackingMode == MangaTrackingMode.Volume ? MangaReleaseUnitType.Volume : MangaReleaseUnitType.Chapter;
                if (parsed.UnitType != expectedUnit)
                {
                    result.Reason = "The filename uses a different unit than this manga tracks.";
                    continue;
                }

                var match = _matcher.Match(parsed, titles, knownItems);
                var candidate = match.Candidates.FirstOrDefault(value => value.Manga.Id == manga.Id);
                if (candidate == null)
                {
                    result.Reason = match.Explanation;
                    continue;
                }

                result.MatchedAlias = candidate.MatchedAlias;
                result.CoveredItemIds = candidate.CoveredItems.Select(item => item.Id).ToList();
                if (match.IsAmbiguous || !candidate.CanAutoMatch)
                {
                    result.Reason = match.IsAmbiguous ? match.Explanation : "Filename match needs manual confirmation.";
                }
                else if (result.CoveredItemIds.Count == 0)
                {
                    result.Reason = "No known manga item falls within this file's parsed coverage.";
                }
                else
                {
                    result.Status = MangaDownloadFileStatus.Ready;
                }
            }

            var duplicates = results.Where(result => result.Status == MangaDownloadFileStatus.Ready)
                .SelectMany(result => result.CoveredItemIds.Select(itemId => new { result, itemId }))
                .GroupBy(value => value.itemId)
                .Where(group => group.Count() > 1)
                .SelectMany(group => group.Select(value => value.result))
                .Distinct();
            foreach (var duplicate in duplicates)
            {
                duplicate.Status = MangaDownloadFileStatus.ManualReview;
                duplicate.Reason = "Another file in this download covers the same manga item.";
            }

            return results;
        }
    }
}
