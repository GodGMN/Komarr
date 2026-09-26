using FluentAssertions;
using NUnit.Framework;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class AuthorLookupFixture : IntegrationTest
    {
        [TestCase("Robert Harris", "Robert Harris")]
        [TestCase("Philip W. Errington", "Philip W. Errington")]
        public void lookup_new_author_by_name(string term, string name)
        {
            var author = Author.Lookup(term);

            author.Should().NotBeEmpty();
            author.Should().Contain(c => c.AuthorName == name);
        }

        // Replaces lookup_new_author_by_goodreads_book_id, which asked for
        // `edition:2` and expected J.K. Rowling. That test was dead twice
        // over: the id was a Goodreads edition, and /author/lookup never
        // handled typed prefixes at all -- the prefix was ignored and the
        // whole string searched as a name, which matched six unrelated
        // records with "2" in them.
        //
        // Looking an author up by their own OpenLibrary id is the thing that
        // actually needs guarding: it is how the Library Import wizard is
        // corrected when its automatic match is wrong, and OpenLibrary's own
        // author search cannot do it (q=OL1422008A returns nothing).
        [TestCase(OpenLibraryFixtureData.PhilipErringtonId)]
        [TestCase("author:" + OpenLibraryFixtureData.PhilipErringtonId)]
        public void lookup_new_author_by_openlibrary_id(string term)
        {
            var author = Author.Lookup(term);

            author.Should().HaveCount(1);
            author.Should().OnlyContain(c => c.ForeignAuthorId == OpenLibraryFixtureData.PhilipErringtonId);
        }

        [Test]
        public void lookup_by_unknown_openlibrary_id_should_be_empty()
        {
            // Well-formed but nonexistent. OpenLibrary answers with a 404,
            // which used to escape as an HTTP 500 with a stack trace; a
            // mistyped id deserves an empty result.
            var author = Author.Lookup("OL99999999999A");

            author.Should().BeEmpty();
        }
    }
}
