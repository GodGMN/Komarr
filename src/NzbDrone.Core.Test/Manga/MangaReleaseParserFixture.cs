using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NzbDrone.Core.Manga;

namespace NzbDrone.Core.Test.Manga
{
    [TestFixture]
    public class MangaReleaseParserFixture
    {
        private readonly MangaReleaseParser _parser = new ();

        [TestCase("Chainsaw Man v12 (2023) (Digital) (LuCaZ)", "Chainsaw Man", MangaReleaseUnitType.Volume, 12, 12)]
        [TestCase("Chainsaw Man Vol. 01-11", "Chainsaw Man", MangaReleaseUnitType.Volume, 1, 11)]
        [TestCase("Chainsaw Man ch. 182", "Chainsaw Man", MangaReleaseUnitType.Chapter, 182, 182)]
        [TestCase("[Kaizen] Jujutsu Kaisen Modulo Ch01 (2025.09.04)", "Jujutsu Kaisen Modulo", MangaReleaseUnitType.Chapter, 1, 1)]
        [TestCase("MAD - ch 12.5.cbz", "MAD", MangaReleaseUnitType.Chapter, 12.5, 12.5)]
        public void Parses_explicit_units(string input, string title, MangaReleaseUnitType unit, double start, double end)
        {
            var parsed = _parser.Parse(input);

            parsed.ParsedTitle.Should().Be(title);
            parsed.UnitType.Should().Be(unit);
            parsed.StartNumber.Should().Be((decimal)start);
            parsed.EndNumber.Should().Be((decimal)end);
            parsed.Confidence.Should().Be(MangaParseConfidence.High);
        }

        [TestCase("Some Manga 01-20")]
        [TestCase("BLAME! Master Edition 01-06")]
        [TestCase("Planetes Omnibus (2015-2016) (c2c) (Trite)")]
        [TestCase("One Piece v01-52 ch001-450")]
        [TestCase("Manga ch001-260, 300-328")]
        [TestCase("Manga v09-01")]
        [TestCase("Manga v01 [Audiobook]")]
        [TestCase("Black Channel -v01-v15c068-")]
        public void Leaves_ambiguous_or_non_manga_releases_unresolved(string input)
        {
            var parsed = _parser.Parse(input);

            parsed.UnitType.Should().Be(MangaReleaseUnitType.Unknown);
            parsed.Confidence.Should().Be(MangaParseConfidence.Low);
            parsed.Warnings.Should().NotBeEmpty();
        }

        [Test]
        public void Parses_pack_and_hints()
        {
            var parsed = _parser.Parse("[Rillant] Ashita no Joe v01-06 (2024-2026) (Omnibus Edition) (Digital) [English]");

            parsed.IsPack.Should().BeTrue();
            parsed.ReleaseGroup.Should().Be("Rillant");
            parsed.EditionHint.Should().Be("Omnibus");
            parsed.Source.Should().Be("Digital");
            parsed.Language.Should().Be("English");
            parsed.Year.Should().Be(2024);
        }

        [Test]
        public void Parses_explicit_text_chapter_number_without_inventing_a_decimal()
        {
            var parsed = _parser.Parse("Manga ch EXTRA-1");

            parsed.UnitType.Should().Be(MangaReleaseUnitType.Chapter);
            parsed.StartNumberText.Should().Be("EXTRA-1");
            parsed.StartNumber.Should().BeNull();
            parsed.Confidence.Should().Be(MangaParseConfidence.Medium);
        }

        [Test]
        public void Reviewed_corpus_reports_unsafe_matches_and_misses_separately()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "ParserFixtures", "nyaa-public-2026-09-27.jsonl");
            var falsePositives = new List<string>();
            var falseNegatives = 0;
            var truePositives = 0;
            var fixtures = File.ReadAllLines(path);

            foreach (var line in fixtures)
            {
                var fixture = JObject.Parse(line);
                var input = fixture.Value<string>("input");
                var expected = fixture["expected"];
                var expectedUnit = expected.Value<string>("unitType");
                var actual = _parser.Parse(input);
                var expectedPositive = expectedUnit != "Unknown";
                var actualPositive = actual.UnitType != MangaReleaseUnitType.Unknown;
                var exact = expectedPositive && actualPositive &&
                            actual.UnitType.ToString() == expectedUnit &&
                            SameNumber(actual.StartNumberText, expected.Value<string>("start")) &&
                            SameNumber(actual.EndNumberText, expected.Value<string>("end")) &&
                            string.Equals(actual.ParsedTitle, expected.Value<string>("title"), StringComparison.OrdinalIgnoreCase);

                if (exact)
                {
                    truePositives++;
                }
                else if (expectedPositive && actualPositive)
                {
                    falsePositives.Add(input);
                    falseNegatives++;
                }
                else if (expectedPositive)
                {
                    falseNegatives++;
                }
                else if (actualPositive)
                {
                    falsePositives.Add(input);
                }
            }

            TestContext.Progress.WriteLine($"Manga parser corpus: {fixtures.Length} fixtures, {truePositives} true positives, {falsePositives.Count} false positives, {falseNegatives} false negatives.");
            falsePositives.Should().BeEmpty("unsafe interpretations must be reviewed: {0}", string.Join(" | ", falsePositives.Take(25)));
            truePositives.Should().BeGreaterThan(100);
        }

        private static bool SameNumber(string actual, string expected)
        {
            return decimal.TryParse(actual, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var actualNumber) &&
                   decimal.TryParse(expected, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var expectedNumber)
                ? actualNumber == expectedNumber
                : string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
