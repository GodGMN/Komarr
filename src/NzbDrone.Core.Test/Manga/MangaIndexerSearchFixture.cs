using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Parser.Model;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaIndexerSearchFixture
    {
        [Test]
        public async Task Searches_at_most_three_distinct_title_variants_and_deduplicates_releases()
        {
            var queries = new List<string>();
            var indexer = new Mock<IIndexer>();
            indexer.As<IMangaSearchIndexer>();
            indexer.SetupGet(x => x.SupportsSearch).Returns(true);
            indexer.SetupGet(x => x.Definition).Returns(new IndexerDefinition { Name = "Nyaa via Prowlarr", Implementation = "Torznab" });
            indexer.Setup(x => x.Fetch(It.IsAny<AuthorSearchCriteria>())).ReturnsAsync((AuthorSearchCriteria criteria) =>
            {
                queries.Add(((MangaSearchCriteria)criteria).MangaTitle);
                return new List<ReleaseInfo>
                {
                    new ReleaseInfo { Guid = "same-torrent", Title = "BLAME! v01", Indexer = "Nyaa", PublishDate = DateTime.UtcNow }
                };
            });
            var factory = new Mock<IIndexerFactory>();
            factory.Setup(x => x.InteractiveSearchEnabled(true)).Returns(new List<IIndexer> { indexer.Object });
            var service = new MangaIndexerSearchService(factory.Object, LogManager.GetCurrentClassLogger());

            var result = await service.Search(new MangaModel
            {
                PreferredTitle = "BLAME!",
                TitleEnglish = "Blame",
                TitleRomaji = "Buramu!"
            });

            queries.Should().Equal("BLAME!", "Buramu!");
            result.Queries.Should().Equal(queries);
            result.Releases.Should().ContainSingle();
            result.IndexerErrors.Should().BeEmpty();
        }

        [Test]
        public async Task Queries_native_manga_indexers_such_as_nyaa()
        {
            var indexer = new Mock<IIndexer>();
            indexer.As<IMangaSearchIndexer>();
            indexer.SetupGet(x => x.SupportsSearch).Returns(true);
            indexer.SetupGet(x => x.Definition).Returns(new IndexerDefinition { Name = "Nyaa", Implementation = "Nyaa" });
            indexer.Setup(x => x.Fetch(It.IsAny<AuthorSearchCriteria>())).ReturnsAsync(new List<ReleaseInfo>());
            var factory = new Mock<IIndexerFactory>();
            factory.Setup(x => x.InteractiveSearchEnabled(true)).Returns(new List<IIndexer> { indexer.Object });
            var service = new MangaIndexerSearchService(factory.Object, LogManager.GetCurrentClassLogger());

            await service.Search(new MangaModel { PreferredTitle = "Tokyo Ghoul" });

            indexer.Verify(x => x.Fetch(It.IsAny<AuthorSearchCriteria>()), Times.Once());
        }

        [Test]
        public async Task Does_not_query_indexers_without_manga_search_support_or_titles_without_a_name()
        {
            var indexer = new Mock<IIndexer>();
            indexer.SetupGet(x => x.SupportsSearch).Returns(true);
            indexer.SetupGet(x => x.Definition).Returns(new IndexerDefinition { Implementation = "Gazelle" });
            var factory = new Mock<IIndexerFactory>();
            factory.Setup(x => x.InteractiveSearchEnabled(true)).Returns(new List<IIndexer> { indexer.Object });
            var service = new MangaIndexerSearchService(factory.Object, LogManager.GetCurrentClassLogger());

            var empty = await service.Search(new MangaModel());
            var named = await service.Search(new MangaModel { PreferredTitle = "BLAME!" });

            empty.Queries.Should().BeEmpty();
            named.Releases.Should().BeEmpty();
            indexer.Verify(x => x.Fetch(It.IsAny<AuthorSearchCriteria>()), Times.Never());
        }
    }
}
