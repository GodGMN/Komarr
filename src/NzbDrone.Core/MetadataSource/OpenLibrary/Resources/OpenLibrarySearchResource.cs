using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Resources
{
    public class OpenLibrarySearchResource
    {
        [JsonProperty("numFound")]
        public int NumFound { get; set; }

        public int Start { get; set; }

        public List<OpenLibrarySearchDoc> Docs { get; set; }
    }

    public class OpenLibrarySearchDoc
    {
        public string Key { get; set; }

        public string Title { get; set; }

        [JsonProperty("author_name")]
        public List<string> AuthorName { get; set; }

        [JsonProperty("author_key")]
        public List<string> AuthorKey { get; set; }

        [JsonProperty("first_publish_year")]
        public int? FirstPublishYear { get; set; }

        public List<string> Isbn { get; set; }

        [JsonProperty("cover_i")]
        public int? CoverI { get; set; }

        [JsonProperty("edition_count")]
        public int EditionCount { get; set; }
    }

    public class OpenLibraryAuthorSearchResource
    {
        [JsonProperty("numFound")]
        public int NumFound { get; set; }

        public List<OpenLibraryAuthorSearchDoc> Docs { get; set; }
    }

    public class OpenLibraryAuthorSearchDoc
    {
        public string Key { get; set; }
        public string Name { get; set; }

        [JsonProperty("alternate_names")]
        public List<string> AlternateNames { get; set; }

        [JsonProperty("birth_date")]
        public string BirthDate { get; set; }

        [JsonProperty("top_work")]
        public string TopWork { get; set; }

        [JsonProperty("work_count")]
        public int WorkCount { get; set; }
    }
}
