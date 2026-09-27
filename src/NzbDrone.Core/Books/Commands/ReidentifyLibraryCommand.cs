using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Books.Commands
{
    // Phase 5 reidentify command. Run once per library when the user flips
    // MetadataSourceType from "BookInfo" → "OpenLibrary". Walks every
    // Author + Book + (TODO) BookFile, computes the best OL mapping, and
    // writes rows into BookIdMapping. The Phase 5 wizard reads
    // BookIdMappingRepository.GetLowConfidence(0.7) to surface rows that
    // need manual review.
    public class ReidentifyLibraryCommand : Command
    {
        public ReidentifyLibraryCommand()
        {
        }

        public override bool SendUpdatesToClient => true;

        public override bool UpdateScheduledTask => false;

        public override string CompletionMessage => "Library reidentification complete";
    }
}
