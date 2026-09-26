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
    // Phase 6 import list: every work by an OL author.
    //   GET /authors/{key}/works.json?limit={n}
    // Returns OpenLibraryAuthorWorksResource. Each Entry is an
    // OpenLibraryWorkResource; we lift the work key + title and
    // attach the configured author key so the import target gets
    // grouped under one author entity.
    public class OpenLibraryAuthorImportList : ImportListBase<OpenLibraryAuthorImportListSettings>
    {
        private readonly IHttpClient _httpClient;
        private readonly IOpenLibraryRequestBuilder _requestBuilder;

        public override string Name => "Open Library Author";
        public override ImportListType ListType => ImportListType.Other;
        public override TimeSpan MinRefreshInterval => TimeSpan.FromHours(24);

        public OpenLibraryAuthorImportList(IHttpClient httpClient,
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
                var key = Settings.AuthorKey?.Trim();
                if (key.IsNullOrWhiteSpace())
                {
                    _importListStatusService.RecordSuccess(Definition.Id);
                    return result;
                }

                var request = _requestBuilder.For($"authors/{key}/works.json?limit={Settings.Limit}").Build();
                var response = _httpClient.Get<OpenLibraryAuthorWorksResource>(request);

                if (response?.Resource?.Entries == null)
                {
                    _importListStatusService.RecordSuccess(Definition.Id);
                    return result;
                }

                foreach (var work in response.Resource.Entries)
                {
                    if (work?.Key.IsNullOrWhiteSpace() != false)
                    {
                        continue;
                    }

                    result.Add(new ImportListItemInfo
                    {
                        BookGoodreadsId = ExtractKey(work.Key),
                        Book = work.Title,
                        EditionGoodreadsId = null,
                        Author = null,
                        AuthorGoodreadsId = key
                    });
                }

                _importListStatusService.RecordSuccess(Definition.Id);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "OL author import list failed");
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
                var key = Settings.AuthorKey?.Trim();
                var request = _requestBuilder.For($"authors/{key}/works.json?limit=1").Build();
                _httpClient.Get<OpenLibraryAuthorWorksResource>(request);
                return null;
            }
            catch (HttpException e)
            {
                _logger.Warn(e, "OL author probe failed");
                if (e.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new ValidationFailure(nameof(Settings.AuthorKey), $"OL author '{Settings.AuthorKey}' not found");
                }

                return new ValidationFailure(nameof(Settings.AuthorKey), "Could not reach Open Library");
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
