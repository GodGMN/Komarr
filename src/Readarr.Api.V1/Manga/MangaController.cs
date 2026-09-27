using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Manga;
using NzbDrone.Core.MetadataSource.AniList;
using Readarr.Http;

namespace Readarr.Api.V1.Manga
{
    [V1ApiController("manga")]
    public class MangaController : Controller
    {
        private readonly IMangaService _manga;
        private readonly IMangaIndexerSearchService _search;
        private readonly IMangaInteractiveSearchService _interactiveSearch;
        private readonly IMangaGrabService _grab;
        private readonly IMangaDownloadFileRepository _downloadFiles;

        public MangaController(
            IMangaService manga,
            IMangaIndexerSearchService search,
            IMangaInteractiveSearchService interactiveSearch,
            IMangaGrabService grab,
            IMangaDownloadFileRepository downloadFiles)
        {
            _manga = manga;
            _search = search;
            _interactiveSearch = interactiveSearch;
            _grab = grab;
            _downloadFiles = downloadFiles;
        }

        [HttpGet]
        public IEnumerable<NzbDrone.Core.Manga.Manga> GetAll()
        {
            return _manga.All();
        }

        [HttpGet("{id:int}")]
        public ActionResult<NzbDrone.Core.Manga.Manga> Get(int id)
        {
            var manga = _manga.Find(id);
            return manga == null ? NotFound() : manga;
        }

        [HttpGet("{id:int}/items")]
        public ActionResult<IEnumerable<MangaItem>> GetItems(int id)
        {
            if (_manga.Find(id) == null)
            {
                return NotFound();
            }

            return _manga.GetItems(id).ToList();
        }

        [HttpGet("{id:int}/files")]
        public ActionResult<IEnumerable<MangaFile>> GetFiles(int id)
        {
            if (_manga.Find(id) == null)
            {
                return NotFound();
            }

            return _manga.GetFiles(id).ToList();
        }

        [HttpGet("{id:int}/search")]
        public async Task<ActionResult<MangaIndexerSearchResult>> Search(int id)
        {
            var manga = _manga.Find(id);
            if (manga == null)
            {
                return NotFound();
            }

            return await _search.Search(manga);
        }

        [HttpGet("{id:int}/search/decisions")]
        public async Task<ActionResult<MangaInteractiveSearchResult>> SearchDecisions(int id, [FromQuery] int? itemId)
        {
            try
            {
                return await _interactiveSearch.Search(id, itemId);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id:int}/downloads")]
        public ActionResult<IEnumerable<MangaDownload>> GetDownloads(int id)
        {
            try
            {
                return _grab.GetDownloads(id).ToList();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("{id:int}/downloads/{downloadId:int}/files")]
        public ActionResult<IEnumerable<MangaDownloadFile>> GetDownloadFiles(int id, int downloadId)
        {
            if (_manga.Find(id) == null)
            {
                return NotFound();
            }

            var download = _grab.GetDownloads(id).FirstOrDefault(value => value.Id == downloadId);
            return download == null ? NotFound() : _downloadFiles.GetByDownloadId(downloadId).ToList();
        }

        [HttpPost("{id:int}/grab")]
        public async Task<ActionResult<MangaDownload>> Grab(int id, [FromBody] MangaGrabRequest request)
        {
            try
            {
                return await _grab.Grab(id, request);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (MangaGrabValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(502, "The download client could not accept this release. Check client health and try again.");
            }
        }

        [HttpPost]
        public ActionResult<NzbDrone.Core.Manga.Manga> Add([FromBody] MangaAddOptions options)
        {
            try
            {
                var manga = _manga.Add(options);
                return CreatedAtAction(nameof(Get), new { id = manga.Id }, manga);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (MangaMetadataException ex)
            {
                return ex.Availability == AniListAvailability.Available
                    ? NotFound(ex.Message)
                    : StatusCode(503, ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        public ActionResult<NzbDrone.Core.Manga.Manga> Update(int id, [FromBody] MangaAddOptions options)
        {
            try
            {
                return _manga.Update(id, options);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:int}/refresh")]
        public ActionResult<NzbDrone.Core.Manga.Manga> Refresh(int id)
        {
            try
            {
                return _manga.Refresh(id);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            try
            {
                _manga.Delete(id);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }
    }

    [V1ApiController("manga/lookup")]
    public class MangaLookupController : Controller
    {
        private readonly IAniListMetadataClient _metadata;

        public MangaLookupController(IAniListMetadataClient metadata)
        {
            _metadata = metadata;
        }

        [HttpGet]
        public AniListResult Search([FromQuery] string term)
        {
            return _metadata.Search(term);
        }
    }
}
