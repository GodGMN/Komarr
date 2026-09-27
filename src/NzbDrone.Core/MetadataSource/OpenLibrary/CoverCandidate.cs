namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    // One cover thumbnail choice for the cover-picker modal. Built by
    // BookController's /api/v1/book/{id}/covers route from the OL work
    // + editions payload, deduped by CoverId.
    public class CoverCandidate
    {
        public int CoverId { get; set; }
        public string Url { get; set; }

        // "work" — OL's editorial pick at work.covers[i]. Always rendered
        // first; the modal labels it "Canonical".
        // "edition" — cover_i from a specific edition record.
        public string Source { get; set; }

        // Optional edition-only metadata so the modal can label the
        // thumbnail with publisher + year. Null when Source == "work".
        public string EditionTitle { get; set; }
        public string PublishDate { get; set; }
        public string Publisher { get; set; }
    }
}
