using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Books.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Books
{
    public class BookAddedHandler : IHandle<BookAddedEvent>
    {
        private readonly IManageCommandQueue _commandQueueManager;

        public BookAddedHandler(IManageCommandQueue commandQueueManager)
        {
            _commandQueueManager = commandQueueManager;
        }

        public void Handle(BookAddedEvent message)
        {
            if (message.DoRefresh)
            {
                // Pull in the author's full works list so the user can
                // browse the discography on the author page. The cascade-
                // add path in AddBookService sets the author's
                // MonitorNewItems = None, so additional books arrive
                // unmonitored ('Missing' status) — only the book the
                // user explicitly clicked Add on stays Monitored=true.
                _commandQueueManager.Push(new RefreshAuthorCommand(message.Book.Author.Value.Id));
            }
        }
    }
}
