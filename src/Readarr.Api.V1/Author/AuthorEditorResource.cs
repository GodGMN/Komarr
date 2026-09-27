using System.Collections.Generic;
using NzbDrone.Core.Books;

namespace Readarr.Api.V1.Author
{
    public class AuthorEditorResource
    {
        public List<int> AuthorIds { get; set; }
        public bool? Monitored { get; set; }
        public NewItemMonitorTypes? MonitorNewItems { get; set; }
        public int? QualityProfileId { get; set; }

        // Nullable so "leave alone" and "clear it" stay distinguishable: the
        // bulk editor omits the field entirely for No Change, and sends an
        // explicit 0 to put an author back to a single quality profile.
        public int? AudiobookQualityProfileId { get; set; }
        public int? MetadataProfileId { get; set; }
        public string RootFolderPath { get; set; }
        public List<int> Tags { get; set; }
        public ApplyTags ApplyTags { get; set; }
        public bool MoveFiles { get; set; }
        public bool DeleteFiles { get; set; }
    }
}
