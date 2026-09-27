using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Parser.Model;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaInteractiveSearchFixture
    {
        [Test]
        public async Task Returns_accepted_and_rejected_releases_with_parsing_and_coverage_evidence()
        {
            var manga = new MangaModel { Id = 7, PreferredTitle = "BLAME!", Monitored = true };
            manga.QualityPolicy.AllowedLanguages.Add("English");
            var first = new MangaItem { Id = 10, MangaId = 7, Type = MangaItemType.Volume, Monitored = true };
            first.SetNumber("1");
            var second = new MangaItem { Id = 11, MangaId = 7, Type = MangaItemType.Volume, Monitored = true };
            second.SetNumber("2");
            var mangaService = new Mock<IMangaService>();
            mangaService.Setup(x => x.Find(7)).Returns(manga);
            mangaService.Setup(x => x.GetItems(7)).Returns(new[] { first, second });
            mangaService.Setup(x => x.GetFiles(7)).Returns(new MangaFile[0]);
            mangaService.Setup(x => x.All()).Returns(new[] { manga });
            var fileItems = new Mock<IMangaFileItemRepository>();
            fileItems.Setup(x => x.GetByFileIds(It.IsAny<IEnumerable<int>>())).Returns(new MangaFileItem[0]);
            var indexers = new Mock<IMangaIndexerSearchService>();
            indexers.Setup(x => x.Search(manga, true)).ReturnsAsync(new MangaIndexerSearchResult
            {
                Queries = new List<string> { "BLAME!" },
                Releases = new List<ReleaseInfo>
                {
                    NewRelease("BLAME! v01 [English] (Digital)"),
                    NewRelease("Naruto v01"),
                    NewRelease("BLAME! v02 [Japanese]")
                }
            });
            var service = new MangaInteractiveSearchService(
                mangaService.Object,
                fileItems.Object,
                indexers.Object,
                new MangaReleaseParser(),
                new MangaReleaseMatcher(),
                new MangaReleaseDecisionEngine());

            var result = await service.Search(7, 10);

            result.Queries.Should().Equal("BLAME!");
            result.Total.Should().Be(3);
            result.Releases[0].Decision.CanGrabAutomatically.Should().BeTrue();
            result.Releases[0].CoveredItemIds.Should().Equal(10);
            result.Releases[0].MatchedAlias.Should().Be("BLAME!");
            result.Releases[0].Quality.Should().Contain("Digital");
            result.Releases[1].Decision.Rejections.Should().Contain(x => x.Contains("does not match"));
            result.Releases[2].Decision.Rejections.Should().Contain(x => x.Contains("requested item"));
            result.Releases[2].Decision.Rejections.Should().Contain(x => x.Contains("language 'Japanese'"));
        }

        [Test]
        public async Task Invalid_item_is_rejected_before_querying_indexers()
        {
            var manga = new MangaModel { Id = 7, PreferredTitle = "BLAME!" };
            var mangaService = new Mock<IMangaService>();
            mangaService.Setup(x => x.Find(7)).Returns(manga);
            mangaService.Setup(x => x.GetItems(7)).Returns(new MangaItem[0]);
            var indexers = new Mock<IMangaIndexerSearchService>();
            var service = new MangaInteractiveSearchService(
                mangaService.Object,
                new Mock<IMangaFileItemRepository>().Object,
                indexers.Object,
                new MangaReleaseParser(),
                new MangaReleaseMatcher(),
                new MangaReleaseDecisionEngine());

            var action = () => service.Search(7, 999);

            await action.Should().ThrowAsync<System.ArgumentException>();
            indexers.Verify(x => x.Search(It.IsAny<MangaModel>(), It.IsAny<bool>()), Times.Never());
        }

        private static ReleaseInfo NewRelease(string title)
        {
            return new TorrentInfo { Title = title, Guid = title, Size = 1000000, Seeders = 10 };
        }
    }
}
