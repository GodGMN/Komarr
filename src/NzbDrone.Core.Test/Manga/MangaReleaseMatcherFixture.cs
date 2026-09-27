using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Manga;
using MangaModel = NzbDrone.Core.Manga.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaReleaseMatcherFixture
    {
        private readonly MangaReleaseParser _parser = new ();
        private readonly MangaReleaseMatcher _matcher = new ();

        [Test]
        public void Exact_blame_academy_does_not_match_parent_blame()
        {
            var blame = NewManga(1, 30149, "BLAME!");
            var academy = NewManga(2, 99999, "BLAME! Academy and So On");

            var outcome = _matcher.Match(_parser.Parse("BLAME! Academy and So On v01"), new[] { blame, academy }, new MangaItem[0]);

            outcome.Candidates.Should().ContainSingle();
            outcome.Candidates[0].Manga.AniListId.Should().Be(99999);
            outcome.Candidates[0].MatchKind.Should().Be(MangaTitleMatchKind.ExactPreferred);
            outcome.Candidates[0].CanAutoMatch.Should().BeTrue();
        }

        [Test]
        public void Shared_alias_requires_manual_selection()
        {
            var first = NewManga(1, 1, "Attack on Titan");
            var second = NewManga(2, 2, "Another AOT");
            first.UserAliases = new List<string> { "AOT" };
            second.Synonyms = new List<string> { "AOT" };

            var outcome = _matcher.Match(_parser.Parse("AOT v01"), new[] { first, second }, new MangaItem[0]);

            outcome.IsAmbiguous.Should().BeTrue();
            outcome.Candidates.Should().HaveCount(2);
            outcome.Candidates.Should().OnlyContain(x => !x.CanAutoMatch && x.Evidence.Any(reason => reason.Contains("Several manga")));
        }

        [Test]
        public void Fuzzy_only_match_never_allows_automatic_matching()
        {
            var blame = NewManga(1, 30149, "BLAME!");

            var outcome = _matcher.Match(_parser.Parse("BLAMR! v01"), new[] { blame }, new MangaItem[0]);

            outcome.Candidates.Should().ContainSingle();
            outcome.Candidates[0].MatchKind.Should().Be(MangaTitleMatchKind.Fuzzy);
            outcome.Candidates[0].CanAutoMatch.Should().BeFalse();
            outcome.Candidates[0].Evidence.Should().Contain(x => x.Contains("manual confirmation"));
        }

        [Test]
        public void Volume_pack_covers_only_matching_volumes_and_requested_item()
        {
            var manga = NewManga(1, 1, "Chainsaw Man");
            var items = new[]
            {
                NewItem(1, 1, MangaItemType.Volume, "1"),
                NewItem(2, 1, MangaItemType.Volume, "2"),
                NewItem(3, 1, MangaItemType.Volume, "3"),
                NewItem(4, 1, MangaItemType.Volume, "4"),
                NewItem(5, 1, MangaItemType.Chapter, "2")
            };

            var outcome = _matcher.Match(_parser.Parse("Chainsaw Man v01-v03"), new[] { manga }, items, 2);

            outcome.Candidates.Should().ContainSingle();
            outcome.Candidates[0].CoveredItems.Select(x => x.Id).Should().Equal(1, 2, 3);
            outcome.Candidates[0].ContainsRequestedItem.Should().BeTrue();
            outcome.Candidates[0].CanAutoMatch.Should().BeTrue();
            outcome.Candidates[0].Evidence.Should().Contain(x => x.Contains("Contains the requested item"));

            var missing = _matcher.Match(_parser.Parse("Chainsaw Man v01-v03"), new[] { manga }, items, 4);
            missing.Candidates[0].CanAutoMatch.Should().BeFalse();
            missing.Candidates[0].Evidence.Should().Contain(x => x.Contains("Does not contain the requested item"));
        }

        [Test]
        public void Text_chapter_number_can_cover_an_exact_known_item()
        {
            var manga = NewManga(1, 1, "Manga");
            var item = NewItem(1, 1, MangaItemType.Chapter, "EXTRA-1");

            var outcome = _matcher.Match(_parser.Parse("Manga ch EXTRA-1"), new[] { manga }, new[] { item }, 1);

            outcome.Candidates[0].CoveredItems.Should().ContainSingle();
            outcome.Candidates[0].CanAutoMatch.Should().BeFalse("a textual chapter token has medium parser confidence");
        }

        [Test]
        public void Unresolved_release_never_returns_an_automatic_candidate()
        {
            var outcome = _matcher.Match(_parser.Parse("BLAME! 01-06"), new[] { NewManga(1, 30149, "BLAME!") }, new MangaItem[0]);

            outcome.Candidates.Should().BeEmpty();
            outcome.Explanation.Should().Contain("unresolved");
        }

        [Test]
        public void Normalization_preserves_non_latin_titles_while_smoothing_punctuation()
        {
            MangaReleaseMatcher.Normalize("ＢＬＡＭＥ！").Should().Be("blame");
            MangaReleaseMatcher.Normalize("進撃の巨人").Should().Be("進撃の巨人");
            MangaReleaseMatcher.Normalize("One-Punch   Man").Should().Be("one punch man");
        }

        private static MangaModel NewManga(int id, int aniListId, string title)
        {
            return new MangaModel { Id = id, AniListId = aniListId, PreferredTitle = title };
        }

        private static MangaItem NewItem(int id, int mangaId, MangaItemType type, string number)
        {
            var item = new MangaItem { Id = id, MangaId = mangaId, Type = type };
            item.SetNumber(number);
            return item;
        }
    }
}
