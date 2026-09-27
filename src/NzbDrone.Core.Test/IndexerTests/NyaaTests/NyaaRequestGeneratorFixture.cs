using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers.Nyaa;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests.NyaaTests
{
    public class NyaaRequestGeneratorFixture : CoreTest<NyaaRequestGenerator>
    {
        [SetUp]
        public void SetUp()
        {
            Subject.Settings = new NyaaSettings
            {
                BaseUrl = "https://nyaa.si",
                AdditionalParameters = "&cats=3_1&filter=1"
            };
        }

        [Test]
        public void should_use_configured_categories_for_the_recent_feed()
        {
            var requests = Subject.GetRecentRequests();

            var page = requests.GetAllTiers().First().First();

            page.Url.FullUri.Should().Be("https://nyaa.si/?page=rss&cats=3_1&filter=1");
        }

        [Test]
        public void should_search_books_by_title()
        {
            var criteria = new BookSearchCriteria
            {
                Author = new NzbDrone.Core.Books.Author { Name = "Tokyo Ghoul" },
                BookTitle = "Tokyo Ghoul"
            };

            var requests = Subject.GetSearchRequests(criteria);

            var page = requests.GetAllTiers().First().First();

            page.Url.FullUri.Should().Be("https://nyaa.si/?page=rss&cats=3_1&filter=1&term=Tokyo%20Ghoul");
        }

        [Test]
        public void manga_search_should_use_the_manga_title()
        {
            var criteria = new MangaSearchCriteria
            {
                Author = new NzbDrone.Core.Books.Author { Name = "Tokyo Ghoul" },
                MangaTitle = "Tokyo Ghoul"
            };

            var requests = Subject.GetSearchRequests(criteria);

            var page = requests.GetAllTiers().First().First();

            page.Url.FullUri.Should().Be("https://nyaa.si/?page=rss&cats=3_1&filter=1&term=Tokyo%20Ghoul");
        }

        [Test]
        public void manga_search_should_url_encode_reserved_characters()
        {
            var criteria = new MangaSearchCriteria
            {
                Author = new NzbDrone.Core.Books.Author { Name = "Daisy Jones & The Six" },
                MangaTitle = "Daisy Jones & The Six"
            };

            var requests = Subject.GetSearchRequests(criteria);

            var page = requests.GetAllTiers().First().First();

            page.Url.FullUri.Should().EndWith("term=Daisy%20Jones%20%26%20The%20Six");
            page.Url.FullUri.Should().NotContain(" & ");
        }

        [Test]
        public void author_search_should_use_the_cleaned_author_query()
        {
            var criteria = new AuthorSearchCriteria
            {
                Author = new NzbDrone.Core.Books.Author { Name = "Daisy Jones & The Six" }
            };

            var requests = Subject.GetSearchRequests(criteria);

            var page = requests.GetAllTiers().First().First();

            page.Url.FullUri.Should().EndWith("term=Daisy%20Jones%20The%20Six");
        }

        [Test]
        public void manga_search_should_fall_back_to_the_author_query_without_a_manga_title()
        {
            var criteria = new MangaSearchCriteria
            {
                Author = new NzbDrone.Core.Books.Author { Name = "Tokyo Ghoul" },
                MangaTitle = ""
            };

            var requests = Subject.GetSearchRequests(criteria);

            var page = requests.GetAllTiers().First().First();

            page.Url.FullUri.Should().EndWith("term=Tokyo%20Ghoul");
        }
    }
}
