using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Manga;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Indexers
{
    public class RssSyncService : IExecute<RssSyncCommand>
    {
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IFetchAndParseRss _rssFetcherAndParser;
        private readonly IMakeDownloadDecision _downloadDecisionMaker;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly IPendingReleaseService _pendingReleaseService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IMangaRssSyncService _mangaRss;
        private readonly Logger _logger;

        public RssSyncService(IIndexerStatusService indexerStatusService,
                              IIndexerFactory indexerFactory,
                              IFetchAndParseRss rssFetcherAndParser,
                              IMakeDownloadDecision downloadDecisionMaker,
                              IProcessDownloadDecisions processDownloadDecisions,
                              IPendingReleaseService pendingReleaseService,
                              IMangaRssSyncService mangaRss,
                              IEventAggregator eventAggregator,
                              Logger logger)
        {
            _indexerStatusService = indexerStatusService;
            _indexerFactory = indexerFactory;
            _rssFetcherAndParser = rssFetcherAndParser;
            _downloadDecisionMaker = downloadDecisionMaker;
            _processDownloadDecisions = processDownloadDecisions;
            _pendingReleaseService = pendingReleaseService;
            _mangaRss = mangaRss;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        private async Task<ProcessedDecisions> Sync()
        {
            _logger.ProgressInfo("Starting RSS Sync");

            var rssReleases = await _rssFetcherAndParser.Fetch();
            var mangaResult = _mangaRss.Process(rssReleases);
            _eventAggregator.PublishEvent(new MangaRssSyncCompleteEvent(mangaResult));
            _logger.Info("Manga RSS: {0} matched, {1} safe candidates, {2} ambiguous, {3} broad aliases, {4} duplicates, {5} unresolved",
                mangaResult.Matched,
                mangaResult.SafeCandidates,
                mangaResult.Ambiguous,
                mangaResult.TooBroad,
                mangaResult.Duplicates,
                mangaResult.Unresolved);
            var pendingReleases = _pendingReleaseService.GetPending();

            var reports = rssReleases.Concat(pendingReleases).ToList();
            var decisions = _downloadDecisionMaker.GetRssDecision(reports);
            var processed = await _processDownloadDecisions.ProcessDecisions(decisions);

            var message = string.Format("RSS Sync Completed. Reports found: {0}, Reports grabbed: {1}", reports.Count, processed.Grabbed.Count);

            if (processed.Pending.Any())
            {
                message += ", Reports pending: " + processed.Pending.Count;
            }

            _logger.ProgressInfo(message);

            return processed;
        }

        public void Execute(RssSyncCommand message)
        {
            var processed = Sync().GetAwaiter().GetResult();
            var grabbedOrPending = processed.Grabbed.Concat(processed.Pending).ToList();

            _eventAggregator.PublishEvent(new RssSyncCompleteEvent(processed));
        }
    }
}
