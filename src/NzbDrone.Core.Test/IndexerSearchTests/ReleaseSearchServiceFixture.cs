using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
        public class ReleaseSearchServiceFixture : CoreTest<ReleaseSearchService>
    {
        private Mock<IIndexer> _mockIndexer;
        private Author _author;
        private Book _firstBook;

        [SetUp]
        public void SetUp()
        {
            _mockIndexer = Mocker.GetMock<IIndexer>();
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition { Id = 1 });
            _mockIndexer.SetupGet(s => s.SupportsSearch).Returns(true);

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.AutomaticSearchEnabled(true))
                  .Returns(new List<IIndexer> { _mockIndexer.Object });

            Mocker.GetMock<IMakeDownloadDecision>()
                .Setup(s => s.GetSearchDecision(It.IsAny<List<Parser.Model.ReleaseInfo>>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(new List<DownloadDecision>());

            _author = Builder<Author>.CreateNew()
                .With(v => v.Monitored = true)
                .Build();

            _firstBook = Builder<Book>.CreateNew()
                .With(e => e.Author = _author)
                .Build();

            var edition = Builder<Edition>.CreateNew()
                .With(e => e.Book = _firstBook)
                .With(e => e.Monitored = true)
                .Build();

            _firstBook.Editions = new List<Edition> { edition };

            Mocker.GetMock<IAuthorService>()
                .Setup(v => v.GetAuthor(_author.Id))
                .Returns(_author);
        }

        private List<SearchCriteriaBase> WatchForSearchCriteria()
        {
            var result = new List<SearchCriteriaBase>();

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<BookSearchCriteria>()))
                .Callback<BookSearchCriteria>(s => result.Add(s))
                .Returns(Task.FromResult<IList<Parser.Model.ReleaseInfo>>(new List<Parser.Model.ReleaseInfo>()));

            return result;
        }

        [Test]
        public async Task Tags_IndexerTags_AuthorNoTags_IndexerNotIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 3 }
            });

            var allCriteria = WatchForSearchCriteria();

            await Subject.BookSearch(_firstBook, false, true, false);

            var criteria = allCriteria.OfType<BookSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task Tags_IndexerNoTags_AuthorTags_IndexerIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1
            });

            _author = Builder<Author>.CreateNew()
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 3 })
                .Build();

            Mocker.GetMock<IAuthorService>()
                .Setup(v => v.GetAuthor(_author.Id))
                .Returns(_author);

            var allCriteria = WatchForSearchCriteria();

            await Subject.BookSearch(_firstBook, false, true, false);

            var criteria = allCriteria.OfType<BookSearchCriteria>().ToList();

            criteria.Count.Should().Be(1);
        }

        [Test]
        public async Task Tags_IndexerAndAuthorTagsMatch_IndexerIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 1, 2, 3 }
            });

            _author = Builder<Author>.CreateNew()
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 3, 4, 5 })
                .Build();

            Mocker.GetMock<IAuthorService>()
                .Setup(v => v.GetAuthor(_author.Id))
                .Returns(_author);

            var allCriteria = WatchForSearchCriteria();

            await Subject.BookSearch(_firstBook, false, true, false);

            var criteria = allCriteria.OfType<BookSearchCriteria>().ToList();

            criteria.Count.Should().Be(1);
        }

        [Test]
        public async Task Tags_IndexerAndAuthorTagsMismatch_IndexerNotIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 1, 2, 3 }
            });

            _author = Builder<Author>.CreateNew()
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 4, 5, 6 })
                .Build();

            Mocker.GetMock<IAuthorService>()
                .Setup(v => v.GetAuthor(_author.Id))
                .Returns(_author);

            var allCriteria = WatchForSearchCriteria();

            await Subject.BookSearch(_firstBook, false, true, false);

            var criteria = allCriteria.OfType<BookSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        // BookSearch used to build its search term with
        //   book.Editions.Value.SingleOrDefault(x => x.Monitored).Title
        // which threw two different ways, both surfacing as a bare HTTP 500
        // from ReleaseController and reading to users as "search is broken":
        // NullReferenceException when nothing matched, and
        // InvalidOperationException when more than one edition was monitored.
        //
        // Measured on a real fresh library (Brandon Sanderson, 192 books),
        // 81 books — 42% — had zero edition rows and failed every search.
        [Test]
        public async Task should_search_on_book_title_when_book_has_no_editions()
        {
            _firstBook.Editions = new List<Edition>();

            var allCriteria = WatchForSearchCriteria();

            await Subject.BookSearch(_firstBook, false, true, false);

            var criteria = allCriteria.OfType<BookSearchCriteria>().Single();
            criteria.BookTitle.Should().Be(_firstBook.Title);
        }

        [Test]
        public async Task should_fall_back_to_an_unmonitored_edition_when_none_are_monitored()
        {
            var edition = Builder<Edition>.CreateNew()
                .With(e => e.Book = _firstBook)
                .With(e => e.Monitored = false)
                .With(e => e.Title = "Unmonitored Edition")
                .Build();

            _firstBook.Editions = new List<Edition> { edition };

            var allCriteria = WatchForSearchCriteria();

            await Subject.BookSearch(_firstBook, false, true, false);

            var criteria = allCriteria.OfType<BookSearchCriteria>().Single();
            criteria.BookTitle.Should().Be("Unmonitored Edition");
        }

        [Test]
        public async Task should_not_throw_when_multiple_editions_are_monitored()
        {
            var editions = Builder<Edition>.CreateListOfSize(3)
                .All()
                .With(e => e.Book = _firstBook)
                .With(e => e.Monitored = true)
                .Build()
                .ToList();

            _firstBook.Editions = editions;

            var allCriteria = WatchForSearchCriteria();

            await Subject.BookSearch(_firstBook, false, true, false);

            var criteria = allCriteria.OfType<BookSearchCriteria>().Single();
            criteria.BookTitle.Should().Be(editions.First().Title);
        }

        [Test]
        public async Task should_prefer_the_monitored_edition_title()
        {
            var editions = new List<Edition>
            {
                Builder<Edition>.CreateNew().With(e => e.Book = _firstBook)
                    .With(e => e.Monitored = false).With(e => e.Title = "Other Edition").Build(),
                Builder<Edition>.CreateNew().With(e => e.Book = _firstBook)
                    .With(e => e.Monitored = true).With(e => e.Title = "Monitored Edition").Build()
            };

            _firstBook.Editions = editions;

            var allCriteria = WatchForSearchCriteria();

            await Subject.BookSearch(_firstBook, false, true, false);

            var criteria = allCriteria.OfType<BookSearchCriteria>().Single();
            criteria.BookTitle.Should().Be("Monitored Edition");
        }
    }
}
