using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Core.Books;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using Readarr.Http;

namespace Readarr.Api.V1.Indexers
{
    // Early manga acquisition diagnostic: search synced indexers without
    // requiring an inherited Author/Book row or making a grab decision.
    [V1ApiController("indexer/rawsearch")]
    public class RawIndexerSearchController : ControllerBase
    {
        private readonly IIndexerFactory _indexerFactory;
        private readonly Logger _logger;

        public RawIndexerSearchController(IIndexerFactory indexerFactory, Logger logger)
        {
            _indexerFactory = indexerFactory;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<RawIndexerSearchResource>> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length > 120)
            {
                return BadRequest("Query must contain 1 to 120 characters.");
            }

            var criteria = new AuthorSearchCriteria
            {
                Author = new NzbDrone.Core.Books.Author { Name = query.Trim() },
                Books = new List<Book>(),
                InteractiveSearch = true,
                UserInvokedSearch = true
            };

            var indexers = _indexerFactory.InteractiveSearchEnabled()
                                          .Where(indexer => indexer.SupportsSearch)
                                          .ToList();

            var searches = indexers.Select(indexer => SearchIndexer(indexer, criteria));
            var results = await Task.WhenAll(searches);
            var releases = results.SelectMany(result => result.Releases)
                                  .OrderByDescending(release => release.PublishDate)
                                  .ToList();

            _logger.Info("Raw indexer search for {0}: {1} releases from {2} indexers", query, releases.Count, indexers.Count);

            return new RawIndexerSearchResource
            {
                Query = query.Trim(),
                Total = releases.Count,
                Indexers = results.Select(result => new RawIndexerResult
                {
                    Name = result.Name,
                    Count = result.Releases.Count,
                    Error = result.Error
                }).ToList(),
                Releases = releases.Take(50).Select(release => new RawIndexerRelease
                {
                    Title = release.Title,
                    Indexer = release.Indexer,
                    Categories = release.Categories ?? new List<int>(),
                    Size = release.Size,
                    PublishDate = release.PublishDate
                }).ToList()
            };
        }

        private async Task<IndexerSearchResult> SearchIndexer(IIndexer indexer, AuthorSearchCriteria criteria)
        {
            try
            {
                var releases = await indexer.Fetch(criteria);
                return new IndexerSearchResult(indexer.Definition.Name, releases.ToList(), null);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Raw search failed on indexer {0}", indexer.Definition.Name);
                return new IndexerSearchResult(indexer.Definition.Name, new List<ReleaseInfo>(), ex.Message);
            }
        }

        private class IndexerSearchResult
        {
            public IndexerSearchResult(string name, List<ReleaseInfo> releases, string error)
            {
                Name = name;
                Releases = releases;
                Error = error;
            }

            public string Name { get; }
            public List<ReleaseInfo> Releases { get; }
            public string Error { get; }
        }
    }

    public class RawIndexerSearchResource
    {
        public string Query { get; set; }
        public int Total { get; set; }
        public List<RawIndexerResult> Indexers { get; set; }
        public List<RawIndexerRelease> Releases { get; set; }
    }

    public class RawIndexerResult
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public string Error { get; set; }
    }

    public class RawIndexerRelease
    {
        public string Title { get; set; }
        public string Indexer { get; set; }
        public List<int> Categories { get; set; }
        public long Size { get; set; }
        public DateTime PublishDate { get; set; }
    }
}
