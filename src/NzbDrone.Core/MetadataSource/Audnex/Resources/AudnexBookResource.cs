using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.Audnex.Resources
{
    // Shape of https://api.audnex.us/books/{asin}.
    public class AudnexBookResource
    {
        public string Asin { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Description { get; set; }
        public string Summary { get; set; }
        public string Language { get; set; }
        public string Publisher { get; set; }

        [JsonProperty("publishedAt")]
        public DateTime? PublishedAt { get; set; }

        [JsonProperty("releaseDate")]
        public DateTime? ReleaseDate { get; set; }

        public string Image { get; set; }
        public int? Rating { get; set; }

        [JsonProperty("runtimeLengthMin")]
        public int? RuntimeLengthMin { get; set; }

        [JsonProperty("formatType")]
        public string FormatType { get; set; }

        public List<AudnexPerson> Authors { get; set; }
        public List<AudnexPerson> Narrators { get; set; }
        public List<AudnexGenre> Genres { get; set; }
    }

    public class AudnexPerson
    {
        public string Asin { get; set; }
        public string Name { get; set; }
    }

    public class AudnexGenre
    {
        public string Asin { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }
}
