using System;
using System.Globalization;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Mappers
{
    internal static class OpenLibraryDateParser
    {
        // OL dates come in many shapes:
        //   "1965-06-04"  ISO
        //   "June 4, 1965"  US long
        //   "4 June 1965"  EU long
        //   "1965"  year only
        //   "ca. 1900"  approximate
        //   ""  missing
        //
        // Strategy: try common explicit formats, then year-only, then bail with null.
        private static readonly string[] Formats =
        {
            "yyyy-MM-dd",
            "yyyy-MM",
            "MMMM d, yyyy",
            "MMMM dd, yyyy",
            "d MMMM yyyy",
            "dd MMMM yyyy",
            "MMM d, yyyy",
            "MMM dd, yyyy",
            "d MMM yyyy"
        };

        public static DateTime? Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (DateTime.TryParseExact(raw, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exact))
            {
                return exact;
            }

            if (int.TryParse(raw, out var year) && year >= 1 && year <= 9999)
            {
                return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            return null;
        }
    }
}
