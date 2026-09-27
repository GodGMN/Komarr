using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Update;

namespace NzbDrone.Core.Test.UpdateTests
{
    public class UpdatePackageProviderFixture : CoreTest<UpdatePackageProvider>
    {
        [Test]
        public void inherited_update_feed_is_disabled()
        {
            Subject.GetLatestUpdate("develop", new Version(0, 1)).Should().BeNull();
            Subject.GetRecentUpdates("develop", new Version(0, 1), null).Should().BeEmpty();
        }
    }
}
