using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests
{
    // Phase 5b: file-tag pass override semantics. The interesting axis
    // isn't the OL lookup itself (that's covered by the OpenLibrary
    // mapper fixtures) but how a freshly-derived file-tag mapping
    // interacts with whatever's already on disk.
    [TestFixture]
    public class ReidentifyServiceFixture : LoggingTest
    {
        [Test]
        public void should_insert_when_no_existing_mapping()
        {
            var incoming = NewFileTagMapping();

            var verdict = ReidentifyService.ResolveOverride(null, incoming);

            verdict.Should().Be(ReidentifyService.OverrideVerdict.Insert);
        }

        [Test]
        public void should_update_when_existing_is_server_side_titleauthor()
        {
            var existing = new BookIdMapping
            {
                GoodreadsId = "gr-123",
                OpenLibraryWorkId = "OL-prior",
                Confidence = 0.55,
                Source = BookIdMappingSource.TitleAuthor
            };
            var incoming = NewFileTagMapping();

            var verdict = ReidentifyService.ResolveOverride(existing, incoming);

            verdict.Should().Be(ReidentifyService.OverrideVerdict.Update);
        }

        [Test]
        public void should_update_when_existing_is_server_side_isbn_with_lower_confidence()
        {
            // Even an ISBN-source server lookup can be wrong — file tags
            // still take precedence. Override decision is by source, not
            // confidence math.
            var existing = new BookIdMapping
            {
                GoodreadsId = "gr-123",
                OpenLibraryWorkId = "OL-prior",
                Confidence = 0.95,
                Source = BookIdMappingSource.Isbn
            };
            var incoming = NewFileTagMapping();

            var verdict = ReidentifyService.ResolveOverride(existing, incoming);

            verdict.Should().Be(ReidentifyService.OverrideVerdict.Update);
        }

        [Test]
        public void should_skip_when_existing_is_manual_user_override()
        {
            var existing = new BookIdMapping
            {
                GoodreadsId = "gr-123",
                OpenLibraryWorkId = "OL-prior",
                Confidence = 1.0,
                Source = BookIdMappingSource.Manual
            };
            var incoming = NewFileTagMapping();

            var verdict = ReidentifyService.ResolveOverride(existing, incoming);

            verdict.Should().Be(ReidentifyService.OverrideVerdict.Skip);
        }

        [Test]
        public void should_skip_when_incoming_is_null()
        {
            // Tag read produced nothing — the existing row stays put.
            var existing = new BookIdMapping
            {
                GoodreadsId = "gr-123",
                OpenLibraryWorkId = "OL-prior",
                Source = BookIdMappingSource.Isbn
            };

            var verdict = ReidentifyService.ResolveOverride(existing, null);

            verdict.Should().Be(ReidentifyService.OverrideVerdict.Skip);
        }

        private static BookIdMapping NewFileTagMapping() => new BookIdMapping
        {
            GoodreadsId = "gr-123",
            OpenLibraryWorkId = "OL-from-tag",
            OpenLibraryEditionId = "OL-edition-from-tag",
            Confidence = 0.97,
            Source = BookIdMappingSource.FileTag
        };
    }
}
