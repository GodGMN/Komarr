using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Books;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Narrator
{
    // Phase 12.4: lightweight book row for the per-narrator detail page.
    // Just the fields needed to render a clickable list — full book +
    // author info stays on the existing /api/v1/book/{slug} endpoint.
    public class NarratorBookResource : RestResource
    {
        public string Title { get; set; }
        public string TitleSlug { get; set; }
        public string ForeignBookId { get; set; }
        public int AuthorId { get; set; }
        public string AuthorName { get; set; }
        public string AuthorTitleSlug { get; set; }
    }

    public static class NarratorBookResourceMapper
    {
        public static NarratorBookResource ToNarratorBookResource(this Book model)
        {
            if (model == null)
            {
                return null;
            }

            var author = model.Author?.Value;
            var metadata = author?.Metadata?.Value;

            return new NarratorBookResource
            {
                Id = model.Id,
                Title = model.Title,
                TitleSlug = model.TitleSlug,
                ForeignBookId = model.ForeignBookId,
                AuthorId = author?.Id ?? 0,
                AuthorName = metadata?.Name,
                AuthorTitleSlug = metadata?.TitleSlug
            };
        }

        public static List<NarratorBookResource> ToNarratorBookResource(this IEnumerable<Book> models)
        {
            return models?.Select(ToNarratorBookResource).ToList();
        }
    }
}
