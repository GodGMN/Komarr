using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.OpenLibrary;

namespace NzbDrone.Core.Test.MetadataSource.OpenLibrary
{
    // /api/v1/author/lookup lands in SearchForNewAuthor, and OL's own author
    // search cannot find an author by OL id (q=OL79043A returns numFound 0).
    // These pin the term parsing that lets the Library Import wizard be
    // corrected by pasting an id, without letting a plain name be mistaken
    // for one.
    [TestFixture]
    public class OpenLibraryAuthorSearchTermFixture
    {
        [TestCase("OL79043A")]
        [TestCase("ol79043a")]
        [TestCase("  OL79043A  ")]
        [TestCase("author:OL79043A")]
        [TestCase("Author: OL79043A")]
        [TestCase("author:  OL79043A  ")]
        public void should_resolve_an_author_id_directly(string input)
        {
            var (_, authorId) = OpenLibraryProxy.ParseAuthorSearchTerm(input);

            // Upper-cased on the way out: OL's author endpoint is
            // case-sensitive, so /authors/ol79043a.json is a 404.
            authorId.Should().Be("OL79043A");
        }

        // The whole reason IsAuthorId got anchored on the digits: "Olivia"
        // starts with OL and ends with A. Under the old shape-check, typing
        // an author's name into the wizard would have been treated as an id
        // lookup and returned nothing.
        [TestCase("Olivia")]
        [TestCase("Olivia Manning")]
        [TestCase("OLA")]
        [TestCase("OL79043W")]
        [TestCase("OL79043")]
        [TestCase("79043A")]
        public void should_not_mistake_a_name_for_an_author_id(string input)
        {
            var (term, authorId) = OpenLibraryProxy.ParseAuthorSearchTerm(input);

            authorId.Should().BeNull();
            term.Should().Be(input.Trim());
        }

        [Test]
        public void should_strip_the_author_prefix_from_a_name_search()
        {
            // A user who types `author:tolkien` means "search authors for
            // tolkien". Passing the literal string to OL matches nothing.
            var (term, authorId) = OpenLibraryProxy.ParseAuthorSearchTerm("author:tolkien");

            authorId.Should().BeNull();
            term.Should().Be("tolkien");
        }

        [TestCase("isbn:067003469X")]
        [TestCase("work:OL26421189W")]
        [TestCase("edition:OL49282196M")]
        public void should_leave_other_prefixes_alone(string input)
        {
            // These are SearchForNewEntity's business. Stripping them here
            // would turn `work:OL26421189W` into a name search for an id.
            var (term, authorId) = OpenLibraryProxy.ParseAuthorSearchTerm(input);

            authorId.Should().BeNull();
            term.Should().Be(input);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void should_survive_an_empty_term(string input)
        {
            var (term, authorId) = OpenLibraryProxy.ParseAuthorSearchTerm(input);

            authorId.Should().BeNull();
            term.Should().BeEmpty();
        }
    }
}
