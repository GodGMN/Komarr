using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource;

namespace NzbDrone.Core.Test.MetadataSource
{
    // Guards how Librarr identifies itself to OpenLibrary, Wikidata and Audnex.
    //
    // This is a regression guard, not a unit test of interesting logic. Three
    // call sites shipped a contact URL pointing at github.com/Librarr/Librarr —
    // a repo that does not exist — and a fourth impersonated an Android device.
    // Both forfeit the rate allowances these services grant to identified
    // clients (OL gives 3x; Wikidata rate-limits anonymous callers hard), and
    // neither leaves the service a way to contact us before blocking us.
    //
    // Nothing about a wrong-but-well-formed User-Agent fails visibly, so
    // without a test pinning it the old strings can drift straight back in.
    [TestFixture]
    public class MetadataUserAgentFixture
    {
        [Test]
        public void should_point_at_the_real_repository()
        {
            MetadataUserAgent.ContactUrl.Should().Be("https://github.com/Rorqualx/Librarr");
        }

        [Test]
        public void should_not_advertise_the_non_existent_librarr_org()
        {
            MetadataUserAgent.Value.Should().NotContain("github.com/Librarr/Librarr");
            MetadataUserAgent.For("covers").Should().NotContain("github.com/Librarr/Librarr");
        }

        [Test]
        public void should_identify_app_and_contact()
        {
            var ua = MetadataUserAgent.Value;

            ua.Should().StartWith("Librarr/");
            ua.Should().Contain("+https://github.com/Rorqualx/Librarr");
        }

        [Test]
        public void should_not_impersonate_another_client()
        {
            var agents = new[] { MetadataUserAgent.Value, MetadataUserAgent.For("cover images") };

            foreach (var ua in agents)
            {
                ua.Should().NotContain("Dalvik");
                ua.Should().NotContain("Android");
                ua.Should().NotContain("iPhone");
                ua.Should().NotContain("Goodreads/");
            }
        }

        [Test]
        public void For_should_append_the_purpose_inside_the_comment()
        {
            MetadataUserAgent.For("series metadata")
                .Should().EndWith("; series metadata)");
        }
    }
}
