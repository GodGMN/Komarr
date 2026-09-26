using System;
using NzbDrone.Core.Books;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Metadata
{
    // Phase 9c. One row of the "needs manual review" page. Each maps a
    // Goodreads book ID to its current best-guess OpenLibrary work,
    // plus the source/confidence that led us there so the user can
    // gauge how seriously to take the existing mapping.
    public class LowConfidenceMappingResource : RestResource
    {
        public string GoodreadsId { get; set; }
        public string OpenLibraryWorkId { get; set; }
        public string OpenLibraryEditionId { get; set; }
        public double Confidence { get; set; }
        public string Source { get; set; }
        public DateTime CreatedUtc { get; set; }

        // Display-only join data — pulled at GET time so the UI doesn't
        // need a second roundtrip per row to render. PUT bodies can
        // leave these null.
        public int? BookId { get; set; }
        public string BookTitle { get; set; }
        public string AuthorName { get; set; }
    }

    public static class LowConfidenceMappingResourceMapper
    {
        public static LowConfidenceMappingResource ToResource(this BookIdMapping model)
        {
            if (model == null)
            {
                return null;
            }

            return new LowConfidenceMappingResource
            {
                Id = model.Id,
                GoodreadsId = model.GoodreadsId,
                OpenLibraryWorkId = model.OpenLibraryWorkId,
                OpenLibraryEditionId = model.OpenLibraryEditionId,
                Confidence = model.Confidence,
                Source = model.Source,
                CreatedUtc = model.CreatedUtc
            };
        }
    }
}
