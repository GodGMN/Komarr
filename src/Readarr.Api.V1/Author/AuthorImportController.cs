using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Books;
using Readarr.Http;

namespace Readarr.Api.V1.Author
{
    // Bulk endpoint behind the Library Import wizard. The wizard resolves a
    // whole root folder's worth of unmapped folders to OpenLibrary authors in
    // one pass, and adding them one POST /author at a time would mean a refresh
    // per author and a partial library if the browser navigated away mid-run.
    //
    // AddAuthors swallows per-author failures by design (see AddAuthorService)
    // so one unresolvable folder can't abort the rest of the import; the
    // response contains only the authors that were actually added, which is
    // what the UI reports back.
    [V1ApiController("author/import")]
    public class AuthorImportController : Controller
    {
        private readonly IAddAuthorService _addAuthorService;

        public AuthorImportController(IAddAuthorService addAuthorService)
        {
            _addAuthorService = addAuthorService;
        }

        [HttpPost]
        public object Import([FromBody] List<AuthorResource> resource)
        {
            var newAuthors = resource.ToModel();

            return _addAuthorService.AddAuthors(newAuthors).ToResource();
        }
    }
}
