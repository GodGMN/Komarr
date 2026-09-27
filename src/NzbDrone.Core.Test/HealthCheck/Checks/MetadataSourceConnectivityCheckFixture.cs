using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    [TestFixture]
    public class MetadataSourceConnectivityCheckFixture : CoreTest<MetadataSourceConnectivityCheck>
    {
        [Test]
        public void inherited_metadata_probe_is_disabled()
        {
            Subject.Check().ShouldBeOk();
            Mocker.GetMock<IHttpClient>()
                  .Verify(s => s.Execute(It.IsAny<HttpRequest>()), Times.Never);
        }
    }
}
