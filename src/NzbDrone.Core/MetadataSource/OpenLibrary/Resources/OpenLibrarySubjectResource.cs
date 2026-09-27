using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Resources
{
    // GET /subjects/{subject}.json shape.
    public class OpenLibrarySubjectResource
    {
        public string Key { get; set; }
        public string Name { get; set; }

        [JsonProperty("work_count")]
        public int WorkCount { get; set; }

        public List<OpenLibrarySubjectWork> Works { get; set; }
    }

    public class OpenLibrarySubjectWork
    {
        // "/works/OL12345W"
        public string Key { get; set; }
        public string Title { get; set; }

        [JsonProperty("authors")]
        public List<OpenLibrarySubjectAuthor> Authors { get; set; }

        [JsonProperty("cover_id")]
        public int? CoverId { get; set; }

        [JsonProperty("first_publish_year")]
        public int? FirstPublishYear { get; set; }
    }

    public class OpenLibrarySubjectAuthor
    {
        // "/authors/OL12345A"
        public string Key { get; set; }
        public string Name { get; set; }
    }
}
