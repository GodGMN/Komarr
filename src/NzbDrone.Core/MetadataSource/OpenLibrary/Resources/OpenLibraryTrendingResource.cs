using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Resources
{
    // GET /trending/{period}.json shape. Periods: now, daily, weekly,
    // monthly, yearly, forever. Each work entry is shaped like a search
    // doc (flat strings/arrays) — NOT the canonical work resource.
    public class OpenLibraryTrendingResource
    {
        public List<OpenLibraryTrendingWork> Works { get; set; }
    }

    public class OpenLibraryTrendingWork
    {
        // "/works/OL12345W"
        public string Key { get; set; }
        public string Title { get; set; }

        [JsonProperty("author_name")]
        public List<string> AuthorName { get; set; }

        [JsonProperty("author_key")]
        public List<string> AuthorKey { get; set; }

        [JsonProperty("cover_i")]
        public int? CoverI { get; set; }

        [JsonProperty("first_publish_year")]
        public int? FirstPublishYear { get; set; }
    }
}
