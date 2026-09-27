using System;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MetadataSource.OpenLibrary;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    // Phase 4 closeout coverage. The connectivity check is the in-CI proxy
    // for the master plan's "health check goes red when OL blocked at
    // firewall" exit criterion — without these tests it's only verifiable
    // by actually firewalling openlibrary.org.
    [TestFixture]
    public class MetadataSourceConnectivityCheckFixture : CoreTest<MetadataSourceConnectivityCheck>
    {
        [SetUp]
        public void Setup()
        {
            // The concrete request builder is fine in tests — it just shapes
            // the HttpRequest; the IHttpClient mock is what actually decides
            // whether the call succeeds.
            Mocker.SetConstant<IOpenLibraryRequestBuilder>(new OpenLibraryRequestBuilder());

            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>()))
                  .Returns("Open Library is unreachable.");
        }

        private void GivenMetadataSource(string source)
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(c => c.MetadataSourceType)
                  .Returns(source);
        }

        private void GivenHttpResponse(HttpStatusCode statusCode, string content = "{}")
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Returns<HttpRequest>(r => new HttpResponse(r, new HttpHeader(), content, statusCode));
        }

        private void GivenHttpThrows(Exception ex)
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Throws(ex);
        }

        [Test]
        public void should_be_ok_and_skip_probe_when_source_is_BookInfo()
        {
            GivenMetadataSource("BookInfo");

            Subject.Check().ShouldBeOk();

            // Lock in zero network calls on the BookInfo path — config-save
            // events fire often and we don't want to ping OL on every save
            // when the user isn't even on the OL source.
            Mocker.GetMock<IHttpClient>()
                  .Verify(s => s.Execute(It.IsAny<HttpRequest>()), Times.Never);
        }

        [Test]
        public void should_be_ok_when_OL_responds_200()
        {
            GivenMetadataSource("OpenLibrary");
            GivenHttpResponse(HttpStatusCode.OK);

            Subject.Check().ShouldBeOk();
        }

        [Test]
        public void should_be_error_when_OL_returns_5xx()
        {
            // Asserting on result.Message is more stable than counting log
            // calls — NLog's Warn(string, params object[]) overload doesn't
            // reliably reach the ExceptionVerification target in this test
            // config (the Warn(Exception, string) overload does). Behavior
            // is what the exit criterion calls for; the warn is a bonus.
            GivenMetadataSource("OpenLibrary");
            GivenHttpResponse(HttpStatusCode.ServiceUnavailable, "down for maintenance");

            var result = Subject.Check();

            result.ShouldBeError();
            result.Message.Should().Contain("HTTP 503");
            Mocker.GetMock<IHttpClient>()
                  .Verify(s => s.Execute(It.IsAny<HttpRequest>()), Times.Once);

            // The SUT logs a Warn here too; absorb it so AssertNoUnexpectedLogs
            // in the teardown doesn't trip.
            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void should_be_error_when_OL_throws()
        {
            // Simulates the firewall-block scenario — the IHttpClient stack
            // raises an exception before any response materializes.
            GivenMetadataSource("OpenLibrary");
            GivenHttpThrows(new WebException("No route to host"));

            var result = Subject.Check();

            result.ShouldBeError();
            result.Message.Should().Contain("unreachable");

            ExceptionVerification.IgnoreWarns();
        }
    }
}
