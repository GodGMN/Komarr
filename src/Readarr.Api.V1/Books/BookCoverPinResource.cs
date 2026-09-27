namespace Readarr.Api.V1.Books
{
    // Request body for PUT /api/v1/book/{id}/cover (cover-picker modal).
    // A null PreferredCoverUrl resets the pin so the mapper default
    // (work.covers[0] when available, else the monitored edition's
    // cover_i) wins again.
    public class BookCoverPinResource
    {
        public string PreferredCoverUrl { get; set; }
    }
}
