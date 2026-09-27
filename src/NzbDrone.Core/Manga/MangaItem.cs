using System;
using System.Globalization;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Manga
{
    public enum MangaItemType
    {
        Volume = 0,
        Chapter = 1
    }

    public enum MangaItemDiscoverySource
    {
        Metadata = 0,
        Release = 1,
        Manual = 2
    }

    public class MangaItem : ModelBase
    {
        public int MangaId { get; set; }
        public MangaItemType Type { get; set; }
        public decimal? NumberDecimal { get; set; }
        public string NumberText { get; set; }
        public string Title { get; set; }
        public bool Monitored { get; set; }
        public MangaItemDiscoverySource DiscoveredFrom { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public DateTime Added { get; set; }

        public void SetNumber(string number)
        {
            if (string.IsNullOrWhiteSpace(number))
            {
                throw new ArgumentException("An item number is required.", nameof(number));
            }

            NumberText = number.Trim();
            NumberDecimal = decimal.TryParse(NumberText,
                                             NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                                             CultureInfo.InvariantCulture,
                                             out var numeric) ? numeric : null;
        }
    }
}
