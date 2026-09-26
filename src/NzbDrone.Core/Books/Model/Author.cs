using System;
using System.Collections.Generic;
using Equ;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Profiles.Metadata;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Books
{
    public class Author : Entity<Author>
    {
        public Author()
        {
            Tags = new HashSet<int>();
            Metadata = new AuthorMetadata();
        }

        // These correspond to columns in the Authors table
        public int AuthorMetadataId { get; set; }
        public string CleanName { get; set; }
        public bool Monitored { get; set; }
        public NewItemMonitorTypes MonitorNewItems { get; set; }
        public DateTime? LastInfoSync { get; set; }
        public string Path { get; set; }
        public string RootFolderPath { get; set; }
        public DateTime Added { get; set; }
        public int QualityProfileId { get; set; }

        // 0 means "single-format author": every release, whatever its format,
        // is judged by QualityProfileId. Set it and audiobook-format releases
        // get their own profile, their own ranking and their own cutoff —
        // which is what lets one author hold both an EPUB and an M4B instead
        // of the two competing for a single slot. See QualityProfileFor.
        public int AudiobookQualityProfileId { get; set; }
        public int MetadataProfileId { get; set; }
        public HashSet<int> Tags { get; set; }
        [MemberwiseEqualityIgnore]
        public AddAuthorOptions AddOptions { get; set; }

        // Dynamically loaded from DB
        [MemberwiseEqualityIgnore]
        public LazyLoaded<AuthorMetadata> Metadata { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<QualityProfile> QualityProfile { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<QualityProfile> AudiobookQualityProfile { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<MetadataProfile> MetadataProfile { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<Book>> Books { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<Series>> Series { get; set; }

        // The single place any download or import decision resolves an
        // author's quality profile. Everything that used to read
        // Author.QualityProfile.Value directly now comes through here with
        // the quality it is actually judging, because the answer depends on
        // the release's format.
        //
        // Falls back to the ebook profile whenever the author is
        // single-format (AudiobookQualityProfileId == 0) or the audiobook
        // profile could not be loaded. Falling back rather than failing is
        // deliberate: a missing profile must not silently reject every
        // release for an author, and this reproduces exactly the behaviour
        // that existed before formats were separable.
        // Null-tolerant all the way down, deliberately. These are called from
        // decision specifications, where an unexpected null must degrade to
        // the old single-profile behaviour rather than throw — a spec that
        // throws doesn't reject one release, it takes down the decision for
        // every release in the batch.
        public QualityProfile QualityProfileFor(QualityModel quality)
        {
            return QualityProfileFor(quality?.Quality);
        }

        public QualityProfile QualityProfileFor(Quality quality)
        {
            return QualityProfileFor(quality?.Format ?? QualityFormat.Text);
        }

        public QualityProfile QualityProfileFor(QualityFormat format)
        {
            if (format == QualityFormat.Audio && AudiobookQualityProfileId > 0)
            {
                var audiobookProfile = AudiobookQualityProfile?.Value;

                if (audiobookProfile != null)
                {
                    return audiobookProfile;
                }
            }

            return QualityProfile?.Value;
        }

        //compatibility properties
        [MemberwiseEqualityIgnore]
        public string Name
        {
            get { return Metadata.Value.Name; } set { Metadata.Value.Name = value; }
        }

        [MemberwiseEqualityIgnore]
        public string ForeignAuthorId
        {
            get { return Metadata.Value.ForeignAuthorId; } set { Metadata.Value.ForeignAuthorId = value; }
        }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", Metadata.Value.ForeignAuthorId.NullSafe(), Metadata.Value.Name.NullSafe());
        }

        public override void UseMetadataFrom(Author other)
        {
            CleanName = other.CleanName;
        }

        public override void UseDbFieldsFrom(Author other)
        {
            Id = other.Id;
            AuthorMetadataId = other.AuthorMetadataId;
            Monitored = other.Monitored;
            MonitorNewItems = other.MonitorNewItems;
            LastInfoSync = other.LastInfoSync;
            Path = other.Path;
            RootFolderPath = other.RootFolderPath;
            Added = other.Added;
            QualityProfileId = other.QualityProfileId;
            QualityProfile = other.QualityProfile;
            AudiobookQualityProfileId = other.AudiobookQualityProfileId;
            AudiobookQualityProfile = other.AudiobookQualityProfile;
            MetadataProfileId = other.MetadataProfileId;
            MetadataProfile = other.MetadataProfile;
            Tags = other.Tags;
            AddOptions = other.AddOptions;
        }

        public override void ApplyChanges(Author other)
        {
            Path = other.Path;
            QualityProfileId = other.QualityProfileId;
            QualityProfile = other.QualityProfile;
            AudiobookQualityProfileId = other.AudiobookQualityProfileId;
            AudiobookQualityProfile = other.AudiobookQualityProfile;
            MetadataProfileId = other.MetadataProfileId;
            MetadataProfile = other.MetadataProfile;

            Books = other.Books;
            Tags = other.Tags;
            AddOptions = other.AddOptions;
            RootFolderPath = other.RootFolderPath;
            Monitored = other.Monitored;
            MonitorNewItems = other.MonitorNewItems;
        }
    }
}
