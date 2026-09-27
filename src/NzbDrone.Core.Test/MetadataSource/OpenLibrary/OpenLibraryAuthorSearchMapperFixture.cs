using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.OpenLibrary.Mappers;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;

namespace NzbDrone.Core.Test.MetadataSource.OpenLibrary
{
    // Ranking fixture for /search/authors.json. Every doc set below is a
    // trimmed copy of a real response captured from live OL, because the
    // whole point of the re-rank is that OL's own ordering is wrong for
    // the query shapes the Library Import wizard produces — a synthetic
    // fixture would just encode the ordering we already believe in.
    [TestFixture]
    public class OpenLibraryAuthorSearchMapperFixture
    {
        private static OpenLibraryAuthorSearchDoc Doc(string key, string name, int workCount, string topWork = null, params string[] alternateNames)
        {
            return new OpenLibraryAuthorSearchDoc
            {
                Key = $"/authors/{key}",
                Name = name,
                WorkCount = workCount,
                TopWork = topWork,
                AlternateNames = alternateNames.Any() ? alternateNames.ToList() : null
            };
        }

        private static OpenLibraryAuthorSearchResource Response(params OpenLibraryAuthorSearchDoc[] docs)
        {
            return new OpenLibraryAuthorSearchResource
            {
                NumFound = docs.Length,
                Docs = docs.ToList()
            };
        }

        [Test]
        public void should_return_empty_for_null_resource()
        {
            OpenLibrarySearchMapper.ReRankAndMapAuthors(null, "anything").Should().BeEmpty();
        }

        [Test]
        public void should_return_empty_when_docs_are_null()
        {
            var resource = new OpenLibraryAuthorSearchResource { NumFound = 0, Docs = null };

            OpenLibrarySearchMapper.ReRankAndMapAuthors(resource, "anything").Should().BeEmpty();
        }

        // The bug this whole change exists for. "Tolkien, J.R.R." is an
        // ordinary Calibre folder name, and live OL ranks an unrelated
        // 1-work archaeology report above the real Tolkien because the
        // report's title happens to contain "(Tolkien, J". The wizard
        // auto-selects the first result, so OL's order is what gets
        // imported.
        [Test]
        public void should_rank_the_real_author_above_a_title_match_with_almost_no_works()
        {
            var response = Response(
                Doc("OL12498774A", "Wheeler, R.E.M. And Wheeler, T.V. (Tolkien, J", 1, "REPORT ON THE EXCAVATION OF THE PREHISTORIC"),
                Doc("OL26320A", "J.R.R. Tolkien", 355, "The Hobbit"));

            var result = OpenLibrarySearchMapper.ReRankAndMapAuthors(response, "Tolkien, J.R.R.");

            result.First().Metadata.Value.ForeignAuthorId.Should().Be("OL26320A");
        }

        [Test]
        public void should_rank_the_populated_record_above_an_identically_named_stub()
        {
            var response = Response(
                Doc("OL16029248A", "Brandon Sanderson", 0),
                Doc("OL1394865A", "Brandon Sanderson", 190, "The Final Empire", "Sanderson, Brandon", "B. Sanderson"));

            var result = OpenLibrarySearchMapper.ReRankAndMapAuthors(response, "Brandon Sanderson");

            result.First().Metadata.Value.ForeignAuthorId.Should().Be("OL1394865A");
        }

        // "Sanderson, Brandon" matches no token order and isn't the primary
        // name, but OL lists it as an alternate. Folder names are far more
        // often last-first than the search box is.
        [Test]
        public void should_score_an_exact_hit_on_an_alternate_name()
        {
            var response = Response(
                Doc("OL16029248A", "Brandon Sanderson", 0),
                Doc("OL1394865A", "Brandon Sanderson", 190, "The Final Empire", "Sanderson, Brandon"));

            var result = OpenLibrarySearchMapper.ReRankAndMapAuthors(response, "Sanderson, Brandon");

            result.First().Metadata.Value.ForeignAuthorId.Should().Be("OL1394865A");
        }

        // The tiebreak is bounded precisely so it can't do this. A prolific
        // unrelated author must not displace an exact name match, however
        // small that author's bibliography is.
        [Test]
        public void should_not_let_work_count_outweigh_a_name_match()
        {
            var response = Response(
                Doc("OL27695A", "Agatha Christie", 1222, "The Mysterious Affair at Styles"),
                Doc("OL9109701A", "AGATHA CHRISTIE DA SILVA CUNHA", 1, "Ações e Experiências"));

            var obscure = OpenLibrarySearchMapper.ReRankAndMapAuthors(
                Response(
                    Doc("OL27695A", "Agatha Christie", 1222, "The Mysterious Affair at Styles"),
                    Doc("OL9109701A", "Agatha Christie da Silva Cunha", 1, "Ações e Experiências")),
                "Agatha Christie da Silva Cunha");

            // Exact match wins despite being outnumbered 1222 to 1.
            obscure.First().Metadata.Value.ForeignAuthorId.Should().Be("OL9109701A");

            // ...and the famous one still wins its own query.
            OpenLibrarySearchMapper.ReRankAndMapAuthors(response, "Agatha Christie")
                                   .First().Metadata.Value.ForeignAuthorId.Should().Be("OL27695A");
        }

        // Two-letter surnames and the "Le" of "Le Guin" survive tokenization;
        // bare initials don't. The book scorer drops anything under three
        // characters, which is why authors get their own tokenizer.
        [Test]
        public void should_match_on_short_name_tokens()
        {
            var response = Response(
                Doc("OL3006803A", "Charles Le Guin", 1, "North Coast"),
                Doc("OL31353A", "Ursula K. Le Guin", 265, "The Left Hand of Darkness"));

            var result = OpenLibrarySearchMapper.ReRankAndMapAuthors(response, "le guin");

            result.First().Metadata.Value.ForeignAuthorId.Should().Be("OL31353A");
        }

        [Test]
        public void should_preserve_ol_ordering_when_scores_tie()
        {
            var response = Response(
                Doc("OL1A", "Unrelated One", 5),
                Doc("OL2A", "Unrelated Two", 5));

            var result = OpenLibrarySearchMapper.ReRankAndMapAuthors(response, "something else entirely");

            result.Select(a => a.Metadata.Value.ForeignAuthorId)
                  .Should().ContainInOrder("OL1A", "OL2A");
        }

        [Test]
        public void should_not_drop_any_results()
        {
            var response = Response(
                Doc("OL19981A", "Stephen King", 606, "Carrie"),
                Doc("OL7829294A", "Stephen King", 48, "Misery"),
                Doc("OL529440A", "Anthony Stephen King", 10, "The British Constitution"),
                Doc("OL117505A", "Sir Stephen King-Hall", 93, "The Diary of a U-Boat Commander"),
                Doc("OL7829287A", "Stephen King", 7, "Principles of Macroeconomics"));

            var result = OpenLibrarySearchMapper.ReRankAndMapAuthors(response, "Stephen King");

            result.Should().HaveCount(5);

            // The three "Stephen King" records are indistinguishable in every
            // other mapped field, so their relative order is the only thing
            // separating the novelist from the economist.
            result.Where(a => a.Metadata.Value.Name == "Stephen King")
                  .Select(a => a.Metadata.Value.ForeignAuthorId)
                  .Should().ContainInOrder("OL19981A", "OL7829294A", "OL7829287A");
        }

        [Test]
        public void should_carry_work_count_and_top_work_onto_the_summary()
        {
            var response = Response(Doc("OL25712A", "Terry Pratchett", 236, "The Colour of Magic"));

            var author = OpenLibrarySearchMapper.ReRankAndMapAuthors(response, "pratchett").Single();

            author.Metadata.Value.WorkCount.Should().Be(236);
            author.Metadata.Value.TopWork.Should().Be("The Colour of Magic");
        }

        [Test]
        public void should_tolerate_a_doc_with_no_name()
        {
            var response = Response(
                Doc("OL1A", null, 3),
                Doc("OL2A", "Real Author", 12, "A Book"));

            var result = OpenLibrarySearchMapper.ReRankAndMapAuthors(response, "Real Author");

            result.Should().HaveCount(2);
            result.First().Metadata.Value.ForeignAuthorId.Should().Be("OL2A");
        }
    }
}
