using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Http.REST.Attributes;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Metadata
{
    // Phase 9c. Surfaces BookIdMapping rows whose Confidence falls
    // below the threshold MetadataSwitchWizard considers "good enough".
    // PUT lets the user overwrite a row with their own OL work ID and
    // marks it as Manual — which the file-tag pass and any future
    // reidentify runs will then leave alone.
    [V1ApiController("metadata/lowconfidencemapping")]
    public class LowConfidenceMappingController : RestController<LowConfidenceMappingResource>
    {
        // Same threshold as ReidentifyService.MediumConfidence. Kept in
        // sync by convention — diverging would silently mean the wizard
        // and this page disagree about which rows need review.
        private const double DefaultThreshold = 0.70;

        private readonly IBookIdMappingRepository _mappingRepo;
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;

        public LowConfidenceMappingController(IBookIdMappingRepository mappingRepo,
                                              IBookService bookService,
                                              IAuthorService authorService)
        {
            _mappingRepo = mappingRepo;
            _bookService = bookService;
            _authorService = authorService;
        }

        protected override LowConfidenceMappingResource GetResourceById(int id)
        {
            return Decorate(_mappingRepo.Get(id));
        }

        [HttpGet]
        public List<LowConfidenceMappingResource> GetLowConfidenceMappings([FromQuery] double? threshold = null)
        {
            var cutoff = threshold ?? DefaultThreshold;
            var rows = _mappingRepo.GetLowConfidence(cutoff);
            return rows.Select(Decorate).ToList();
        }

        [RestPutById]
        public ActionResult<LowConfidenceMappingResource> OverrideMapping([FromBody] LowConfidenceMappingResource resource)
        {
            // Manual override: trust the user's chosen OL ID, lock
            // confidence at 1.0, and stamp Source=Manual so subsequent
            // reidentify passes (including the file-tag one) will leave
            // this row untouched.
            var existing = _mappingRepo.Get(resource.Id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.OpenLibraryWorkId = resource.OpenLibraryWorkId;
            existing.OpenLibraryEditionId = resource.OpenLibraryEditionId;
            existing.Confidence = 1.0;
            existing.Source = BookIdMappingSource.Manual;

            _mappingRepo.Update(existing);
            return Accepted(Decorate(existing));
        }

        private LowConfidenceMappingResource Decorate(BookIdMapping mapping)
        {
            var resource = mapping.ToResource();
            if (resource == null)
            {
                return null;
            }

            // Best-effort join: if we can find the Book for this
            // Goodreads ID, surface title + author so the UI doesn't
            // show bare opaque IDs to the user. If the local Book has
            // been deleted, the row still renders — just blanker.
            if (mapping.GoodreadsId.IsNotNullOrWhiteSpace())
            {
                var book = _bookService.FindById(mapping.GoodreadsId);
                if (book != null)
                {
                    resource.BookId = book.Id;
                    resource.BookTitle = book.Title;

                    if (book.AuthorMetadataId > 0)
                    {
                        var author = _authorService.GetAuthorByMetadataId(book.AuthorMetadataId);
                        resource.AuthorName = author?.Name;
                    }
                }
            }

            return resource;
        }
    }
}
