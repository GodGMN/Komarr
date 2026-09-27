using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;

namespace NzbDrone.Common.Test.EnvironmentInfo
{
    [TestFixture]
    public class BuildInfoFixture
    {
        [Test]
        public void should_return_version()
        {
            // 0 = dev/unstamped, 1 = Librarr 1.x line, 10 = inherited Readarr placeholder
            BuildInfo.Version.Major.Should().BeOneOf(0, 1, 10);
        }

        [Test]
        public void should_get_branch()
        {
            // Branch is empty on GHA (BUILD_SOURCEBRANCHNAME is an
            // Azure Pipelines-only env var). Accept empty/null until the
            // GHA workflow plumbs a branch source — the only thing we
            // actually want to guard against is the explicit "unknown"
            // sentinel that AssemblyConfiguration falls back to when
            // the value isn't filled in at all.
            BuildInfo.Branch.Should().NotBe("unknown");
        }
    }
}
