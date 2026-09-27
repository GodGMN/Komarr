using System.Collections.Generic;
using Readarr.Api.V1.Narrator;
using RestSharp;

namespace NzbDrone.Integration.Test.Client
{
    public class NarratorClient : ClientBase<NarratorResource>
    {
        public NarratorClient(IRestClient restClient, string apiKey)
            : base(restClient, apiKey, "narrator")
        {
        }

        public List<NarratorResource> GetByEdition(int editionId)
        {
            var request = BuildRequest("?editionId=" + editionId.ToString());
            return Get<List<NarratorResource>>(request);
        }

        public List<NarratorBookResource> GetBooks(int narratorId)
        {
            var request = BuildRequest(narratorId.ToString() + "/book");
            return Get<List<NarratorBookResource>>(request);
        }
    }
}
