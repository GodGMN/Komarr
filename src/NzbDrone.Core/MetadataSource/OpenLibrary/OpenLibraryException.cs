using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    public class OpenLibraryException : NzbDroneException
    {
        public OpenLibraryException(string message, params object[] args)
            : base(message, args)
        {
        }

        public OpenLibraryException(string message)
            : base(message)
        {
        }
    }
}
