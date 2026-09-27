using System;
using System.Collections.Generic;
using System.Net;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.OpenLibrary;
using NzbDrone.Core.MetadataSource.OpenLibrary.Resources;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.ImportLists.OpenLibrary
{
    // Phase 6 OL-native import list. Pulls works by Open Library subject
    // tag from /subjects/{subject}.json. See sibling lists
    // OpenLibraryAuthorImportList and OpenLibraryTrendingImportList for
    // the other two from the master plan; all three share the same
    // skeleton + ExtractKey helper.
    public class OpenLibrarySubjectImportList : ImportListBase<OpenLibrarySubjectImportListSettings>
    {
        private readonly IHttpClient _httpClient;
        private readonly IOpenLibraryRequestBuilder _requestBuilder;

        public override string Name => "Open Library Subject";
        public override ImportListType ListType => ImportListType.Other;
        public override TimeSpan MinRefreshInterval => TimeSpan.FromHours(12);

        public OpenLibrarySubjectImportList(IHttpClient httpClient,
                                            IOpenLibraryRequestBuilder requestBuilder,
                                            IImportListStatusService importListStatusService,
                                            IConfigService configService,
                                            IParsingService parsingService,
                                            Logger logger)
            : base(importListStatusService, configService, parsingService, logger)
        {
            _httpClient = httpClient;
            _requestBuilder = requestBuilder;
        }

        public override IList<ImportListItemInfo> Fetch()
        {
            var result = new List<ImportListItemInfo>();

            try
            {
                var request = _requestBuilder.For($"subjects/{Settings.Subject}.json?limit={Settings.Limit}").Build();
                var response = _httpClient.Get<OpenLibrarySubjectResource>(request);

                if (response?.Resource?.Works == null)
                {
                    _importListStatusService.RecordSuccess(Definition.Id);
                    return result;
                }

                foreach (var work in response.Resource.Works)
                {
                    var authorName = work.Authors?.Count > 0 ? work.Authors[0].Name : null;
                    var authorKey = work.Authors?.Count > 0 ? work.Authors[0].Key : null;

                    result.Add(new ImportListItemInfo
                    {
                        BookGoodreadsId = ExtractKey(work.Key),
                        Book = work.Title,
                        EditionGoodreadsId = null,
                        Author = authorName,
                        AuthorGoodreadsId = ExtractKey(authorKey)
                    });
                }

                _importListStatusService.RecordSuccess(Definition.Id);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "OL subject import list failed");
                _importListStatusService.RecordFailure(Definition.Id);
            }

            return CleanupListItems(result);
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            failures.AddIfNotNull(TestConnection());
        }

        private ValidationFailure TestConnection()
        {
            try
            {
                var request = _requestBuilder.For($"subjects/{Settings.Subject}.json?limit=1").Build();
                _httpClient.Get<OpenLibrarySubjectResource>(request);
                return null;
            }
            catch (HttpException e)
            {
                _logger.Warn(e, "OL subject probe failed");
                if (e.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new ValidationFailure(nameof(Settings.Subject), $"OL subject '{Settings.Subject}' not found");
                }

                return new ValidationFailure(nameof(Settings.Subject), "Could not reach Open Library");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to connect to Open Library");
                return new ValidationFailure(string.Empty, "Unable to connect to import list, check the log for more details");
            }
        }

        private static string ExtractKey(string olKey)
        {
            if (olKey.IsNullOrWhiteSpace())
            {
                return olKey;
            }

            var slash = olKey.LastIndexOf('/');
            return slash >= 0 ? olKey.Substring(slash + 1) : olKey;
        }
    }
}
