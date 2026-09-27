using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Books;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Manga
{
    public class MangaIndexerSearchResult
    {
        public List<string> Queries { get; set; } = new ();
        public List<ReleaseInfo> Releases { get; set; } = new ();
        public Dictionary<string, string> IndexerErrors { get; set; } = new ();
    }

    public interface IMangaIndexerSearchService
    {
        Task<MangaIndexerSearchResult> Search(Manga manga, bool interactive = true);
    }

    public class MangaIndexerSearchService : IMangaIndexerSearchService
    {
        private readonly IIndexerFactory _indexerFactory;
        private readonly Logger _logger;

        public MangaIndexerSearchService(IIndexerFactory indexerFactory, Logger logger)
        {
            _indexerFactory = indexerFactory;
            _logger = logger;
        }

        public async Task<MangaIndexerSearchResult> Search(Manga manga, bool interactive = true)
        {
            if (manga == null)
            {
                throw new ArgumentNullException(nameof(manga));
            }

            var result = new MangaIndexerSearchResult
            {
                Queries = new[] { manga.PreferredTitle, manga.TitleEnglish, manga.TitleRomaji }
                    .Where(title => !string.IsNullOrWhiteSpace(title) && title.Trim().Length <= 120)
                    .Select(title => title.Trim())
                    .DistinctBy(MangaReleaseMatcher.Normalize)
                    .Take(3)
                    .ToList()
            };
            if (result.Queries.Count == 0)
            {
                return result;
            }

            var indexers = interactive ? _indexerFactory.InteractiveSearchEnabled() : _indexerFactory.AutomaticSearchEnabled();
            indexers = indexers.Where(indexer => indexer.SupportsSearch &&
                (indexer.Definition.Implementation == "Torznab" || indexer.Definition.Implementation == "Newznab")).ToList();

            var searches = indexers.Select(indexer => SearchIndexer(indexer, result.Queries, interactive));
            var batches = await Task.WhenAll(searches);
            foreach (var batch in batches)
            {
                if (batch.Error != null)
                {
                    result.IndexerErrors[batch.Indexer] = batch.Error;
                }

                result.Releases.AddRange(batch.Releases);
            }

            result.Releases = result.Releases
                .GroupBy(release => !string.IsNullOrWhiteSpace(release.Guid) ? release.Guid :
                    !string.IsNullOrWhiteSpace(release.DownloadUrl) ? release.DownloadUrl :
                    $"{release.IndexerId}:{release.Title}:{release.Size}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(release => release.IndexerPriority).First())
                .OrderByDescending(release => release.PublishDate)
                .ToList();
            return result;
        }

        private async Task<(string Indexer, List<ReleaseInfo> Releases, string Error)> SearchIndexer(IIndexer indexer, IEnumerable<string> queries, bool interactive)
        {
            var releases = new List<ReleaseInfo>();
            foreach (var query in queries)
            {
                try
                {
                    var criteria = new MangaSearchCriteria
                    {
                        MangaTitle = query,
                        Author = new Author { Name = query },
                        Books = new List<Book>(),
                        InteractiveSearch = interactive,
                        UserInvokedSearch = interactive
                    };
                    releases.AddRange(await indexer.Fetch(criteria));
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Manga search failed on indexer {0} for {1}", indexer.Definition.Name, query);
                    return (indexer.Definition.Name, releases, ex.Message);
                }
            }

            return (indexer.Definition.Name, releases, null);
        }
    }
}
