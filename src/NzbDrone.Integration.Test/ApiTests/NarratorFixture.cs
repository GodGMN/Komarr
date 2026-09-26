using System.Net;
using FluentAssertions;
using NUnit.Framework;

namespace NzbDrone.Integration.Test.ApiTests
{
    // The Phase 12.1 controller is read-only with no public POST/PUT
    // surface — narrator rows are created by RefreshEditionService
    // syncing audnex output. So these fixtures validate the wire path
    // (routing + serialization + empty-result handling) rather than a
    // seeded round-trip; the mapper logic is covered by unit tests
    // over NarratorResourceMapper.
    [TestFixture]
    public class NarratorFixture : IntegrationTest
    {
        [Test]
        public void narrator_lookup_unknown_id_returns_404()
        {
            Narrators.InvalidGet(9999, HttpStatusCode.NotFound);
        }

        [Test]
        public void narrator_lookup_by_unknown_edition_returns_empty_list()
        {
            var result = Narrators.GetByEdition(9999);

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Test]
        public void narrator_books_for_unknown_id_returns_empty_list()
        {
            // Phase 12.4 — backing endpoint for the per-narrator detail
            // page. A bogus id should round-trip as 200 OK + [] rather
            // than 404, so the page can render an empty state instead
            // of an error.
            var result = Narrators.GetBooks(9999);

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }
    }
}
