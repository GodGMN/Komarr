using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class UpgradeDiskSpecification : IDecisionEngineSpecification
    {
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly ICustomFormatCalculationService _formatService;
        private readonly Logger _logger;

        public UpgradeDiskSpecification(UpgradableSpecification qualityUpgradableSpecification,
                                        ICacheManager cacheManager,
                                        ICustomFormatCalculationService formatService,
                                        Logger logger)
        {
            _upgradableSpecification = qualityUpgradableSpecification;
            _formatService = formatService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual Decision IsSatisfiedBy(RemoteBook subject, SearchCriteriaBase searchCriteria)
        {
            var releaseQuality = subject.ParsedBookInfo?.Quality;

            foreach (var file in subject.Books.SelectMany(c => c.BookFiles.Value))
            {
                if (file == null)
                {
                    return Decision.Accept();
                }

                // A file of the other format doesn't occupy this release's
                // slot, so it can't be "equal or higher preference" than it.
                // Skipped rather than filtered out of the collection so the
                // null early-accept above keeps its existing meaning.
                if (file.Quality.Format() != releaseQuality.Format())
                {
                    continue;
                }

                var customFormats = _formatService.ParseCustomFormat(file);

                if (!_upgradableSpecification.IsUpgradable(subject.Author.QualityProfileFor(releaseQuality),
                                                           file.Quality,
                                                           customFormats,
                                                           subject.ParsedBookInfo.Quality,
                                                           subject.CustomFormats))
                {
                    return Decision.Reject("Existing files on disk is of equal or higher preference: {0}", file.Quality.Quality.Name);
                }
            }

            return Decision.Accept();
        }
    }
}
