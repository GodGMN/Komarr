using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.OpenLibrary.Resources
{
    public class OpenLibraryAuthorResource
    {
        // "/authors/OL5749351A"
        public string Key { get; set; }

        public string Name { get; set; }

        [JsonProperty("personal_name")]
        public string PersonalName { get; set; }

        [JsonConverter(typeof(OpenLibraryDescriptionConverter))]
        public string Bio { get; set; }

        [JsonProperty("birth_date")]
        public string BirthDate { get; set; }

        [JsonProperty("death_date")]
        public string DeathDate { get; set; }

        [JsonProperty("alternate_names")]
        public List<string> AlternateNames { get; set; }

        public List<int> Photos { get; set; }
    }

    public class OpenLibraryAuthorWorksResource
    {
        public int Size { get; set; }
        public List<OpenLibraryWorkResource> Entries { get; set; }
    }
}
