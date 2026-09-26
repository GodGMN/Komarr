using System.Collections.Generic;
using System.Linq;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Narrator
{
    public class NarratorResource : RestResource
    {
        public string Name { get; set; }
        public string ForeignNarratorId { get; set; }
    }

    public static class NarratorResourceMapper
    {
        public static NarratorResource ToResource(this NzbDrone.Core.Books.Narrator model)
        {
            if (model == null)
            {
                return null;
            }

            return new NarratorResource
            {
                Id = model.Id,
                Name = model.Name,
                ForeignNarratorId = model.ForeignNarratorId
            };
        }

        public static List<NarratorResource> ToResource(this IEnumerable<NzbDrone.Core.Books.Narrator> models)
        {
            return models?.Select(ToResource).ToList();
        }
    }
}
