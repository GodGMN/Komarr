using System.Collections.Generic;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Test.Common;
using Readarr.Api.V1.Metadata;

namespace NzbDrone.Api.Test.MetadataTests
{
    // Phase 9c API surface coverage. The interesting axes:
    //   * GET uses the configurable threshold (default 0.70)
    //   * Decoration joins book + author when they exist locally
    //   * PUT forces Source=Manual + Confidence=1.0 regardless of body
    //   * PUT against a missing id returns NotFound
    [TestFixture]
    public class LowConfidenceMappingControllerFixture : TestBase<LowConfidenceMappingController>
    {
        [Test]
        public void GetLowConfidenceMappings_should_pass_default_threshold_when_unspecified()
        {
            Mocker.GetMock<IBookIdMappingRepository>()
                .Setup(r => r.GetLowConfidence(It.IsAny<double>()))
                .Returns(new List<BookIdMapping>());

            Subject.GetLowConfidenceMappings();

            Mocker.GetMock<IBookIdMappingRepository>()
                .Verify(r => r.GetLowConfidence(0.70), Times.Once);
        }

        [Test]
        public void GetLowConfidenceMappings_should_respect_explicit_threshold()
        {
            Mocker.GetMock<IBookIdMappingRepository>()
                .Setup(r => r.GetLowConfidence(It.IsAny<double>()))
                .Returns(new List<BookIdMapping>());

            Subject.GetLowConfidenceMappings(0.5);

            Mocker.GetMock<IBookIdMappingRepository>()
                .Verify(r => r.GetLowConfidence(0.5), Times.Once);
        }

        [Test]
        public void GetLowConfidenceMappings_should_decorate_with_book_title_and_author_when_present()
        {
            var mapping = new BookIdMapping
            {
                Id = 7,
                GoodreadsId = "gr-100",
                OpenLibraryWorkId = "OL-W-200",
                Confidence = 0.6,
                Source = BookIdMappingSource.TitleAuthor
            };
            var book = new Book { Id = 42, Title = "Foundation", AuthorMetadataId = 11 };
            var author = new Author { Name = "Isaac Asimov" };

            Mocker.GetMock<IBookIdMappingRepository>()
                .Setup(r => r.GetLowConfidence(It.IsAny<double>()))
                .Returns(new List<BookIdMapping> { mapping });
            Mocker.GetMock<IBookService>()
                .Setup(s => s.FindById("gr-100")).Returns(book);
            Mocker.GetMock<IAuthorService>()
                .Setup(s => s.GetAuthorByMetadataId(11)).Returns(author);

            var result = Subject.GetLowConfidenceMappings();

            result.Should().HaveCount(1);
            result[0].BookId.Should().Be(42);
            result[0].BookTitle.Should().Be("Foundation");
            result[0].AuthorName.Should().Be("Isaac Asimov");
            result[0].Source.Should().Be(BookIdMappingSource.TitleAuthor);
        }

        [Test]
        public void GetLowConfidenceMappings_should_still_render_row_when_book_was_deleted()
        {
            var mapping = new BookIdMapping
            {
                Id = 7,
                GoodreadsId = "gr-orphan",
                OpenLibraryWorkId = "OL-W-200",
                Confidence = 0.4,
                Source = BookIdMappingSource.TitleAuthor
            };

            Mocker.GetMock<IBookIdMappingRepository>()
                .Setup(r => r.GetLowConfidence(It.IsAny<double>()))
                .Returns(new List<BookIdMapping> { mapping });
            Mocker.GetMock<IBookService>()
                .Setup(s => s.FindById("gr-orphan")).Returns((Book)null);

            var result = Subject.GetLowConfidenceMappings();

            result.Should().HaveCount(1);
            result[0].BookTitle.Should().BeNull();
            result[0].AuthorName.Should().BeNull();
            result[0].GoodreadsId.Should().Be("gr-orphan");
        }

        [Test]
        public void OverrideMapping_should_force_manual_source_and_confidence_1()
        {
            var existing = new BookIdMapping
            {
                Id = 7,
                GoodreadsId = "gr-100",
                OpenLibraryWorkId = "OL-old",
                Confidence = 0.55,
                Source = BookIdMappingSource.TitleAuthor
            };
            Mocker.GetMock<IBookIdMappingRepository>()
                .Setup(r => r.Get(7)).Returns(existing);

            var resource = new LowConfidenceMappingResource
            {
                Id = 7,
                OpenLibraryWorkId = "OL-CORRECT",
                OpenLibraryEditionId = "OL-EDITION",

                // Caller's confidence + source should be ignored — the
                // controller pins them. Set bogus values to prove it.
                Confidence = 0.12,
                Source = "BogusFromClient"
            };

            Subject.OverrideMapping(resource);

            Mocker.GetMock<IBookIdMappingRepository>()
                .Verify(r => r.Update(It.Is<BookIdMapping>(m =>
                    m.Id == 7 &&
                    m.OpenLibraryWorkId == "OL-CORRECT" &&
                    m.OpenLibraryEditionId == "OL-EDITION" &&
                    m.Confidence == 1.0 &&
                    m.Source == BookIdMappingSource.Manual)), Times.Once);
        }

        [Test]
        public void OverrideMapping_should_return_NotFound_for_missing_id()
        {
            Mocker.GetMock<IBookIdMappingRepository>()
                .Setup(r => r.Get(99)).Returns((BookIdMapping)null);

            var result = Subject.OverrideMapping(new LowConfidenceMappingResource { Id = 99 });

            result.Result.Should().BeOfType<NotFoundResult>();
            Mocker.GetMock<IBookIdMappingRepository>()
                .Verify(r => r.Update(It.IsAny<BookIdMapping>()), Times.Never);
        }
    }
}
