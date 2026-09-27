using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Books
{
    public class BookIdMapping : ModelBase
    {
        public string GoodreadsId { get; set; }
        public string OpenLibraryWorkId { get; set; }
        public string OpenLibraryEditionId { get; set; }
        public double Confidence { get; set; }
        public string Source { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    // Source values used by ReidentifyService when recording mappings.
    // Stored as plain strings (not an enum) to keep the bridge table
    // schema-stable as new strategies are added without migrations.
    public static class BookIdMappingSource
    {
        public const string Isbn = "ISBN";
        public const string Asin = "ASIN";
        public const string TitleAuthor = "TitleAuthor";
        public const string FileTag = "FileTag";
        public const string Manual = "Manual";
    }
}
