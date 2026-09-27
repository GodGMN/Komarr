using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Backup;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Test.Backup
{
    [TestFixture]
    public class BackupRestoreFixture
    {
        private Mock<IDiskProvider> _disk;
        private Mock<IArchiveService> _archive;
        private BackupService _service;

        [SetUp]
        public void SetUp()
        {
            _disk = new Mock<IDiskProvider>();
            _archive = new Mock<IArchiveService>();
            var folders = new Mock<IAppFolderInfo>();
            folders.SetupGet(value => value.TempFolder).Returns("/tmp");
            folders.SetupGet(value => value.AppDataFolder).Returns("/config");
            _service = new BackupService(
                new Mock<IMainDatabase>().Object,
                new Mock<IMakeDatabaseBackup>().Object,
                new Mock<IDiskTransferService>().Object,
                _disk.Object,
                folders.Object,
                _archive.Object,
                new Mock<IConfigService>().Object,
                LogManager.GetCurrentClassLogger());
        }

        [Test]
        public void Zip_missing_database_does_not_replace_config()
        {
            _disk.Setup(value => value.GetFiles("/tmp/komarr_backup_restore", false))
                .Returns(new List<string> { "/tmp/komarr_backup_restore/config.xml" });

            System.Action action = () => _service.Restore("backup.zip");

            action.Should().Throw<RestoreBackupFailedException>().WithMessage("*both config.xml and komarr.db*");
            _disk.Verify(value => value.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void Complete_zip_restores_database_and_config()
        {
            _disk.Setup(value => value.GetFiles("/tmp/komarr_backup_restore", false))
                .Returns(new List<string>
                {
                    "/tmp/komarr_backup_restore/Config.xml",
                    "/tmp/komarr_backup_restore/komarr.db"
                });

            _service.Restore("backup.zip");

            _archive.Verify(value => value.Extract("backup.zip", "/tmp/komarr_backup_restore"), Times.Once());
            _disk.Verify(value => value.MoveFile("/tmp/komarr_backup_restore/Config.xml", It.IsAny<string>(), true), Times.Once());
            _disk.Verify(value => value.MoveFile("/tmp/komarr_backup_restore/komarr.db", It.IsAny<string>(), true), Times.Once());
        }
    }
}
