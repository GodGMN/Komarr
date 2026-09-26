using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.OpenLibrary.Mappers;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;
using NzbDrone.Core.Test.MetadataSource.OpenLibrary.Fixtures;

namespace NzbDrone.Core.Test.MetadataSource.OpenLibrary
{
    // Phase 3 exit-criterion fixture: the corpus of real OL JSON committed
    // under Files/OpenLibrary/ deserializes into our resource shapes AND
    // round-trips through the mappers without exceptions. Each cassette
    // becomes its own [TestCase] so a future schema drift on OL surfaces
    // as a named failure pointing at the offending file.
    //
    // Captured 2026-05-17 with scripts/capture-ol-cassettes.sh — see the
    // README in Files/OpenLibrary/ for the capture procedure and corpus
    // category coverage.
    [TestFixture]
    public class OpenLibraryCassetteFixture
    {
        // CassettesDir is resolved relative to the test assembly output dir
        // (the same convention OpenLibraryFixtureLoader uses).
        private static string CassettesDir =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", "OpenLibrary");

        // ── /works/{id}.json shape ──────────────────────────────────
        public static IEnumerable WorkCassettes() =>
            EnumerateMatching("work_*.json", exclude: name => name.StartsWith("work_redirect_"));

        [TestCaseSource(nameof(WorkCassettes))]
        public void Work_cassette_should_deserialize_and_map_to_book(string fileName)
        {
            var work = OpenLibraryFixtureLoader.Load<OpenLibraryWorkResource>(fileName);

            work.Should().NotBeNull();

            // Captured `key` from OL is "/works/OLxxxxxW" — boundary stripping
            // happens in the mapper, so the raw resource still carries the prefix.
            work.Key.Should().StartWith("/works/");

            var (book, authors) = OpenLibraryWorkMapper.ToBook(work, null);

            book.Should().NotBeNull();
            book.ForeignBookId.Should().NotBeNullOrEmpty();
            book.ForeignBookId.Should().NotStartWith("/", "mapper must strip the /works/ prefix");
            authors.Should().NotBeNull();
        }

        // The redirect record shape — Key + type=/type/redirect + location.
        // Title is absent. Verifies the mapper doesn't NRE on it.
        [Test]
        public void Work_redirect_record_should_round_trip_without_throwing()
        {
            const string fileName = "work_redirect_record.json";
            if (!OpenLibraryFixtureLoader.Exists(fileName))
            {
                Assert.Ignore($"Redirect cassette {fileName} not present.");
            }

            var work = OpenLibraryFixtureLoader.Load<OpenLibraryWorkResource>(fileName);

            // Don't assert on shape — just that the mapper survives a record
            // with no Title / no Subjects / no Authors.
            Action map = () => OpenLibraryWorkMapper.ToBook(work, null);
            map.Should().NotThrow();
        }

        // ── /works/{id}/editions.json shape ─────────────────────────
        public static IEnumerable EditionListCassettes() => EnumerateMatching("editions_*.json");

        [TestCaseSource(nameof(EditionListCassettes))]
        public void Edition_list_cassette_should_deserialize_and_map_entries(string fileName)
        {
            var list = OpenLibraryFixtureLoader.Load<OpenLibraryEditionListResource>(fileName);

            list.Should().NotBeNull();
            list.Entries.Should().NotBeNull("OL returns at least an empty entries[]");

            // Every entry must be mappable. Editions with weird/missing fields
            // exercise the mapper's null-tolerance.
            foreach (var entry in list.Entries)
            {
                Action map = () =>
                {
                    var edition = OpenLibraryEditionMapper.ToEdition(entry);
                    edition.Should().NotBeNull();
                };
                map.Should().NotThrow($"entry {entry?.Key} of {fileName} should be mappable");
            }
        }

        // ── /authors/{id}.json shape ────────────────────────────────
        public static IEnumerable AuthorCassettes() =>
            EnumerateMatching("author_*.json",
                              exclude: name => name.EndsWith("_works.json") || name.StartsWith("author_search"));

        [TestCaseSource(nameof(AuthorCassettes))]
        public void Author_cassette_should_deserialize_and_map(string fileName)
        {
            var author = OpenLibraryFixtureLoader.Load<OpenLibraryAuthorResource>(fileName);

            author.Should().NotBeNull();
            author.Key.Should().StartWith("/authors/");
            author.Name.Should().NotBeNullOrEmpty();

            var mapped = OpenLibraryAuthorMapper.ToAuthor(author, null);
            mapped.Should().NotBeNull();
            mapped.Metadata.Value.ForeignAuthorId.Should().NotBeNullOrEmpty();
            mapped.Metadata.Value.ForeignAuthorId.Should().NotStartWith("/");

            // The mapper prefers `personal_name` over `name` and CleanSpaces()
            // them — don't assert byte-for-byte equality with the raw resource;
            // just that the projection produced something.
            mapped.Metadata.Value.Name.Should().NotBeNullOrEmpty();
        }

        // ── /authors/{id}/works.json shape ──────────────────────────
        public static IEnumerable AuthorWorksCassettes() => EnumerateMatching("author_*_works.json");

        [TestCaseSource(nameof(AuthorWorksCassettes))]
        public void Author_works_cassette_should_deserialize(string fileName)
        {
            var works = OpenLibraryFixtureLoader.Load<OpenLibraryAuthorWorksResource>(fileName);

            works.Should().NotBeNull();
            works.Entries.Should().NotBeNull("OL always returns an entries array");
            works.Entries.Should().NotBeEmpty(fileName);
        }

        // ── /search.json shape (books) ──────────────────────────────
        public static IEnumerable SearchBookCassettes() =>
            EnumerateMatching("search_*.json",
                              exclude: name => name.StartsWith("search_author_"));

        [TestCaseSource(nameof(SearchBookCassettes))]
        public void Book_search_cassette_should_deserialize_and_rerank(string fileName)
        {
            var search = OpenLibraryFixtureLoader.Load<OpenLibrarySearchResource>(fileName);

            search.Should().NotBeNull();
            search.Docs.Should().NotBeNull();

            // ReRankAndMap accepts an empty query (the ASIN-search path does this).
            Action map = () => OpenLibrarySearchMapper.ReRankAndMap(search, queryTitle: string.Empty, queryAuthor: null);
            map.Should().NotThrow();
        }

        // ── /search/authors.json shape ──────────────────────────────
        public static IEnumerable SearchAuthorCassettes() => EnumerateMatching("search_author_*.json");

        [TestCaseSource(nameof(SearchAuthorCassettes))]
        public void Author_search_cassette_should_deserialize_and_map_each_doc(string fileName)
        {
            var search = OpenLibraryFixtureLoader.Load<OpenLibraryAuthorSearchResource>(fileName);

            search.Should().NotBeNull();
            search.Docs.Should().NotBeNull();
            search.Docs.Should().NotBeEmpty($"author search cassette {fileName} should return at least one doc");

            foreach (var doc in search.Docs)
            {
                var author = OpenLibrarySearchMapper.ToAuthorSummary(doc);
                author.Should().NotBeNull();
                author.Metadata.Value.ForeignAuthorId.Should().NotBeNullOrEmpty();
            }
        }

        // Ranking assertions against the captured responses. The unit-level
        // fixture (OpenLibraryAuthorSearchMapperFixture) works from trimmed
        // docs; these run the same scorer over the untouched JSON OL
        // actually served, so a change in OL's own ordering or in the
        // fields it returns shows up here rather than in a user's library.
        [TestCase("search_author_tolkien_lastfirst.json", "Tolkien, J.R.R.", "OL26320A")]
        [TestCase("search_author_sanderson.json", "Brandon Sanderson", "OL1394865A")]
        [TestCase("search_author_sanderson.json", "Sanderson, Brandon", "OL1394865A")]
        [TestCase("search_author_king.json", "Stephen King", "OL19981A")]
        public void Author_search_cassette_should_rank_the_real_author_first(string fileName, string query, string expectedOlid)
        {
            var search = OpenLibraryFixtureLoader.Load<OpenLibraryAuthorSearchResource>(fileName);

            var ranked = OpenLibrarySearchMapper.ReRankAndMapAuthors(search, query);

            ranked.Should().NotBeEmpty();
            ranked[0].Metadata.Value.ForeignAuthorId.Should().Be(expectedOlid);

            // Nothing is filtered out — a stub record is demoted, never
            // hidden, because OL's index lags and a genuinely new author
            // can legitimately report zero works.
            ranked.Should().HaveSameCount(search.Docs);
        }

        // Regression guard for the ordering this whole re-rank exists to fix.
        // If this ever starts passing without the re-rank, OL fixed its own
        // ranking and the scorer's justification is worth revisiting.
        [Test]
        public void Tolkien_lastfirst_cassette_should_document_ols_own_ordering_as_wrong()
        {
            var search = OpenLibraryFixtureLoader.Load<OpenLibraryAuthorSearchResource>("search_author_tolkien_lastfirst.json");

            OpenLibrarySearchMapper.ToAuthorSummary(search.Docs[0])
                                   .Metadata.Value.ForeignAuthorId
                                   .Should().Be("OL12498774A", "OL ranks a 1-work title match above the real Tolkien");
        }

        // ── /isbn/{isbn}.json shape ─────────────────────────────────
        public static IEnumerable IsbnCassettes() => EnumerateMatching("isbn_*.json");

        [TestCaseSource(nameof(IsbnCassettes))]
        public void Isbn_cassette_should_deserialize_and_map(string fileName)
        {
            var edition = OpenLibraryFixtureLoader.Load<OpenLibraryEditionResource>(fileName);

            edition.Should().NotBeNull();
            edition.Key.Should().StartWith("/books/");

            var book = OpenLibraryEditionMapper.ToBook(edition);
            book.Should().NotBeNull();
        }

        // ── /subjects/{subject}.json shape ──────────────────────────
        public static IEnumerable SubjectCassettes() => EnumerateMatching("subject_*.json");

        [TestCaseSource(nameof(SubjectCassettes))]
        public void Subject_cassette_should_deserialize(string fileName)
        {
            var subject = OpenLibraryFixtureLoader.Load<OpenLibrarySubjectResource>(fileName);

            subject.Should().NotBeNull();
            subject.Works.Should().NotBeNull();
        }

        // ── helpers ─────────────────────────────────────────────────

        // NUnit calls TestCaseSource methods statically before any [SetUp] runs,
        // so cassette discovery is a plain file enumeration. If the directory
        // hasn't been copied yet (early CI bootstrap) we return an empty set
        // and the individual `TestCaseSource` cases simply don't materialize.
        private static IEnumerable<TestCaseData> EnumerateMatching(string pattern, Predicate<string> exclude = null)
        {
            if (!Directory.Exists(CassettesDir))
            {
                yield break;
            }

            foreach (var path in Directory.EnumerateFiles(CassettesDir, pattern))
            {
                var name = Path.GetFileName(path);
                if (exclude != null && exclude(name))
                {
                    continue;
                }

                yield return new TestCaseData(name).SetName($"{{m}}({name})");
            }
        }
    }
}
