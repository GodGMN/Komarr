using System;
using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.MediaFiles.BookImport.Identification;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.Goodreads;
using NzbDrone.Core.MetadataSource.OpenLibrary;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.BookImport.Identification
{
    [TestFixture]
    public class CandidateServiceFixture : CoreTest<CandidateService>
    {
        private static LocalEdition TitleAndAuthorEdition()
        {
            return new LocalEdition
            {
                LocalBooks = new List<LocalBook>
                {
                    new LocalBook
                    {
                        FileTrackInfo = new ParsedTrackInfo
                        {
                            Authors = new List<string> { "Author" },
                            BookTitle = "Book"
                        }
                    }
                }
            };
        }

        private static LocalEdition IsbnEdition()
        {
            return new LocalEdition
            {
                LocalBooks = new List<LocalBook>
                {
                    new LocalBook
                    {
                        FileTrackInfo = new ParsedTrackInfo
                        {
                            Authors = new List<string> { "Author" },
                            BookTitle = "Book",
                            Isbn = "9780345391803"
                        }
                    }
                }
            };
        }

        private static HttpException NotFound()
        {
            return new HttpException(new HttpResponse(
                new HttpRequest("https://openlibrary.org/isbn/9780345391803.json"),
                new HttpHeader(),
                Array.Empty<byte>(),
                HttpStatusCode.NotFound));
        }

        [Test]
        public void should_not_throw_on_goodreads_exception()
        {
            Mocker.GetMock<ISearchForNewBook>()
                .Setup(s => s.SearchForNewBook(It.IsAny<string>(), It.IsAny<string>(), true))
                .Throws(new GoodreadsException("Bad search"));

            Subject.GetRemoteCandidates(TitleAndAuthorEdition(), null).Should().BeEmpty();
        }

        [Test]
        public void should_not_throw_on_openlibrary_exception()
        {
            Mocker.GetMock<ISearchForNewBook>()
                .Setup(s => s.SearchForNewBook(It.IsAny<string>(), It.IsAny<string>(), true))
                .Throws(new OpenLibraryException("OL is unhappy"));

            Subject.GetRemoteCandidates(TitleAndAuthorEdition(), null).Should().BeEmpty();
        }

        [Test]
        public void should_not_throw_when_isbn_lookup_returns_404()
        {
            Mocker.GetMock<ISearchForNewBook>()
                .Setup(s => s.SearchByIsbn(It.IsAny<string>()))
                .Throws(NotFound());

            Mocker.GetMock<ISearchForNewBook>()
                .Setup(s => s.SearchForNewBook(It.IsAny<string>(), It.IsAny<string>(), true))
                .Returns(new List<Books.Book>());

            Subject.GetRemoteCandidates(IsbnEdition(), null).Should().BeEmpty();

            // An HTTP failure survived the retry loop — loud enough to notice when
            // it is happening to every book in a large import.
            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_not_throw_when_title_search_returns_404()
        {
            Mocker.GetMock<ISearchForNewBook>()
                .Setup(s => s.SearchForNewBook(It.IsAny<string>(), It.IsAny<string>(), true))
                .Throws(NotFound());

            Subject.GetRemoteCandidates(TitleAndAuthorEdition(), null).Should().BeEmpty();

            // Three search paths fall back to SearchForNewBook, so three warnings.
            ExceptionVerification.ExpectedWarns(3);
        }
    }
}
