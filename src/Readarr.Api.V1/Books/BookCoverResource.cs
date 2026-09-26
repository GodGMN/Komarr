namespace Readarr.Api.V1.Books
{
    // Cover-picker modal payload. Returned by GET /api/v1/book/{id}/covers
    // as a list, sourced from OpenLibraryProxy.GetCoverCandidates which
    // dedupes by CoverId across work.covers + every edition's cover_i.
    public class BookCoverResource
    {
        public int CoverId { get; set; }
        public string Url { get; set; }
        public string Source { get; set; }
        public string EditionTitle { get; set; }
        public string PublishDate { get; set; }
        public string Publisher { get; set; }
    }
}
