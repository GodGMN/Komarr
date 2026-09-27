using System;
using System.IO;

namespace NzbDrone.Common.Disk
{
    public class DestinationAlreadyExistsException : IOException
    {
        public DestinationAlreadyExistsException()
        {
        }

        public DestinationAlreadyExistsException(string message)
            : base(message)
        {
        }

        public DestinationAlreadyExistsException(string message, int hresult)
            : base(message, hresult)
        {
        }

        public DestinationAlreadyExistsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        // The protected (SerializationInfo, StreamingContext) constructor was
        // removed for .NET 10. It existed only so BinaryFormatter could
        // round-trip this exception across an AppDomain boundary — a scenario
        // that no longer exists, since BinaryFormatter itself has been removed
        // from the runtime. Nothing in the codebase called it; only the
        // formatter did, by reflection.
    }
}
