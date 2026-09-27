using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Manga
{
    public class MangaSearchMissingCommand : Command
    {
        public override bool SendUpdatesToClient => true;
        public override bool IsLongRunning => true;
    }
}
