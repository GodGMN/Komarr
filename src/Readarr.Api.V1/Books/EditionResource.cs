using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaCover;
using Readarr.Api.V1.Narrator;
using Readarr.Http.REST;
using Swashbuckle.AspNetCore.Annotations;

namespace Readarr.Api.V1.Books
{
    public class EditionResource : RestResource
    {
        public int BookId { get; set; }
        public string ForeignEditionId { get; set; }
        public string TitleSlug { get; set; }
        public string Isbn13 { get; set; }
        public string Asin { get; set; }
        public string Title { get; set; }
        public string Language { get; set; }
        public string Overview { get; set; }
        public string Format { get; set; }
        public bool IsEbook { get; set; }
        public string Disambiguation { get; set; }
        public string Publisher { get; set; }
        public int PageCount { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string Narrators { get; set; }
        public List<NarratorResource> NarratorList { get; set; }
        public List<MediaCover> Images { get; set; }
        public List<Links> Links { get; set; }
        public Ratings Ratings { get; set; }
        public bool Monitored { get; set; }
        public bool ManualAdd { get; set; }
        public string RemoteCover { get; set; }

        //Hiding this so people don't think its usable (only used to set the initial state)
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        [SwaggerIgnore]
        public bool Grabbed { get; set; }
    }

    public static class EditionResourceMapper
    {
        public static EditionResource ToResource(this Edition model)
        {
            if (model == null)
            {
                return null;
            }

            return new EditionResource
            {
                Id = model.Id,
                BookId = model.BookId,
                ForeignEditionId = model.ForeignEditionId,
                TitleSlug = model.TitleSlug,
                Isbn13 = model.Isbn13,
                Asin = model.Asin,
                Title = model.Title,
                Language = model.Language,
                Overview = model.Overview,
                Format = model.Format,
                IsEbook = model.IsEbook,
                Disambiguation = model.Disambiguation,
                Publisher = model.Publisher,
                PageCount = model.PageCount,
                ReleaseDate = model.ReleaseDate,
                Narrators = ProjectNarrators(model),
                NarratorList = ProjectNarratorList(model),
                Images = model.Images,
                Links = model.Links,
                Ratings = model.Ratings,
                Monitored = model.Monitored,
                ManualAdd = model.ManualAdd
            };
        }

        // Project narrators from the normalized Narrators/EditionNarrators
        // tables (migration 043 + 044) as a comma-separated string. Older
        // frontend code paths (BookDetailsHeader.js fallback) still read
        // this field. Returns null on empty so the property serializes
        // omitted rather than as an empty string.
        private static string ProjectNarrators(Edition model)
        {
            var fromJoin = model.NarratorList?.Value;
            if (fromJoin == null || fromJoin.Count == 0)
            {
                return null;
            }

            return string.Join(", ", fromJoin.Select(n => n.Name));
        }

        // Structured projection of the same join — surfaced under the new
        // `narratorList` field so the frontend can render per-narrator
        // chips (see frontend/src/Book/Details/NarratorChip.js).
        private static List<NarratorResource> ProjectNarratorList(Edition model)
        {
            var fromJoin = model.NarratorList?.Value;
            if (fromJoin == null || fromJoin.Count == 0)
            {
                return null;
            }

            return fromJoin.ToResource();
        }

        public static Edition ToModel(this EditionResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new Edition
            {
                Id = resource.Id,
                BookId = resource.BookId,
                ForeignEditionId = resource.ForeignEditionId,
                TitleSlug = resource.TitleSlug,
                Isbn13 = resource.Isbn13,
                Asin = resource.Asin,
                Title = resource.Title,
                Language = resource.Language,
                Overview = resource.Overview,
                Format = resource.Format,
                IsEbook = resource.IsEbook,
                Disambiguation = resource.Disambiguation,
                Publisher = resource.Publisher,
                PageCount = resource.PageCount,
                ReleaseDate = resource.ReleaseDate,
                Images = resource.Images,
                Links = resource.Links,
                Ratings = resource.Ratings,
                Monitored = resource.Monitored,
                ManualAdd = resource.ManualAdd
            };
        }

        public static List<EditionResource> ToResource(this IEnumerable<Edition> models)
        {
            return models?.Select(ToResource).ToList();
        }

        public static List<Edition> ToModel(this IEnumerable<EditionResource> resources)
        {
            return resources.Select(ToModel).ToList();
        }
    }
}
