using System;

namespace NzbDrone.Common.Exceptions
{
    public class ReadarrStartupException : NzbDroneException
    {
        public ReadarrStartupException(string message, params object[] args)
            : base("Komarr failed to start: " + string.Format(message, args))
        {
        }

        public ReadarrStartupException(string message)
            : base("Komarr failed to start: " + message)
        {
        }

        public ReadarrStartupException()
            : base("Komarr failed to start")
        {
        }

        public ReadarrStartupException(Exception innerException, string message, params object[] args)
            : base("Komarr failed to start: " + string.Format(message, args), innerException)
        {
        }

        public ReadarrStartupException(Exception innerException, string message)
            : base("Komarr failed to start: " + message, innerException)
        {
        }

        public ReadarrStartupException(Exception innerException)
            : base("Komarr failed to start: " + innerException.Message)
        {
        }
    }
}
