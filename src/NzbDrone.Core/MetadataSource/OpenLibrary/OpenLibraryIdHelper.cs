using System.Text.RegularExpressions;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    // Shape-detectors for Open Library identifiers. Hoisted out of
    // MetadataSourceFactory so LegacyMigrationService (and any future
    // consumer) can ask the same question without taking a dependency on
    // the factory.
    //
    // OL author ids are `OL\d+A` (e.g. OL26320A); work ids are `OL\d+W`
    // (e.g. OL1234W). GoodReads-shaped ids that came in from a pre-cutover
    // BookInfo DB are bare numeric strings ("3345" for Joseph Conrad), so
    // "is it shaped like an OL id?" is a strong signal that a foreign id
    // is already on the new system.
    public static class OpenLibraryIdHelper
    {
        // Anchored on the digits, not just the OL prefix and the type
        // suffix. The old StartsWith("OL") && EndsWith("A") pair called
        // "Olivia" an author id -- harmless while every caller passed a
        // stored foreign id, but SearchForNewAuthor now asks this question
        // about whatever a user typed into a search box, where it is not
        // harmless at all.
        private static readonly Regex AuthorIdRegex =
            new Regex(@"^OL\d+A$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex WorkIdRegex =
            new Regex(@"^OL\d+W$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsAuthorId(string id)
            => id != null && AuthorIdRegex.IsMatch(id);

        public static bool IsWorkId(string id)
            => id != null && WorkIdRegex.IsMatch(id);
    }
}
