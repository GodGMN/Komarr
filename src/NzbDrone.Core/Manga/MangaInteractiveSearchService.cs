using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Manga
{
    public class MangaInteractiveRelease
    {
        public string Guid { get; set; }
        public int IndexerId { get; set; }
        public string Indexer { get; set; }
        public string Title { get; set; }
        public long Size { get; set; }
        public DateTime PublishDate { get; set; }
        public DownloadProtocol Protocol { get; set; }
        public int? Seeders { get; set; }
        public string Container { get; set; }
        public string Quality { get; set; }
        public List<int> Categories { get; set; } = new ();
        public ParsedMangaReleaseInfo Parsed { get; set; }
        public string MatchedAlias { get; set; }
        public MangaTitleMatchKind? MatchKind { get; set; }
        public List<int> CoveredItemIds { get; set; } = new ();
        public MangaReleaseDecision Decision { get; set; }
    }

    public class MangaInteractiveSearchResult
    {
        public int MangaId { get; set; }
        public int? ItemId { get; set; }
        public List<string> Queries { get; set; } = new ();
        public int Total { get; set; }
        public Dictionary<string, string> IndexerErrors { get; set; } = new ();
        public List<MangaInteractiveRelease> Releases { get; set; } = new ();
    }

    public interface IMangaInteractiveSearchService
    {
        Task<MangaInteractiveSearchResult> Search(int mangaId, int? itemId = null);
    }

    public class MangaInteractiveSearchService : IMangaInteractiveSearchService
    {
        private readonly IMangaService _manga;
        private readonly IMangaFileItemRepository _fileItems;
        private readonly IMangaIndexerSearchService _indexers;
        private readonly IMangaReleaseParser _parser;
        private readonly IMangaReleaseMatcher _matcher;
        private readonly IMangaReleaseDecisionEngine _decisions;

        public MangaInteractiveSearchService(
            IMangaService manga,
            IMangaFileItemRepository fileItems,
            IMangaIndexerSearchService indexers,
            IMangaReleaseParser parser,
            IMangaReleaseMatcher matcher,
            IMangaReleaseDecisionEngine decisions)
        {
            _manga = manga;
            _fileItems = fileItems;
            _indexers = indexers;
            _parser = parser;
            _matcher = matcher;
            _decisions = decisions;
        }

        public async Task<MangaInteractiveSearchResult> Search(int mangaId, int? itemId = null)
        {
            var manga = _manga.Find(mangaId) ?? throw new KeyNotFoundException("Manga was not found.");
            var items = _manga.GetItems(mangaId).ToList();
            if (itemId.HasValue && items.All(item => item.Id != itemId.Value))
            {
                throw new ArgumentException("Requested item does not belong to this manga.");
            }

            var files = _manga.GetFiles(mangaId).ToList();
            var fileItems = _fileItems.GetByFileIds(files.Select(file => file.Id)).ToList();
            var library = _manga.All().ToList();
            var search = await _indexers.Search(manga);
            var result = new MangaInteractiveSearchResult
            {
                MangaId = mangaId,
                ItemId = itemId,
                Queries = search.Queries,
                Total = search.Releases.Count,
                IndexerErrors = search.IndexerErrors
            };

            foreach (var release in search.Releases.Take(100))
            {
                var parsed = _parser.Parse(release.Title);
                var match = _matcher.Match(parsed, library, items, itemId);
                var candidate = match.Candidates.FirstOrDefault(value => value.Manga.Id == mangaId);
                var decision = candidate == null
                    ? new MangaReleaseDecision
                    {
                        Rejections = new List<string>
                        {
                            parsed.UnitType == MangaReleaseUnitType.Unknown ? match.Explanation :
                                match.Candidates.Count > 0 ? "Release title matches a different manga." : "Release title does not match this manga."
                        },
                        Evidence = parsed.Warnings.ToList()
                    }
                    : _decisions.Evaluate(manga, match, candidate, release, files, fileItems, requestedItemId: itemId);
                result.Releases.Add(new MangaInteractiveRelease
                {
                    Guid = release.Guid,
                    IndexerId = release.IndexerId,
                    Indexer = release.Indexer,
                    Title = release.Title,
                    Size = release.Size,
                    PublishDate = release.PublishDate,
                    Protocol = release.DownloadProtocol,
                    Seeders = TorrentInfo.GetSeeders(release),
                    Container = release.Container,
                    Quality = string.Join(" / ", new[] { parsed.Source, release.Container, parsed.Language }
                        .Where(value => !string.IsNullOrWhiteSpace(value)).DefaultIfEmpty("Unknown")),
                    Categories = release.Categories ?? new List<int>(),
                    Parsed = parsed,
                    MatchedAlias = candidate?.MatchedAlias,
                    MatchKind = candidate?.MatchKind,
                    CoveredItemIds = candidate?.CoveredItems.Select(item => item.Id).ToList() ?? new List<int>(),
                    Decision = decision
                });
            }

            return result;
        }
    }
}
