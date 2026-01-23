using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Vault.Index.IServices;
using Vault.Interfaces;
using Vault.Models;
using Vault.Core.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Vault.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IElasticSearchService _elasticService;
        private readonly IVaultRepository<Document> _repository;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(IElasticSearchService elasticService, ILogger<DocumentsController> logger, IVaultRepository<Document> repository)
        {
            _elasticService = elasticService;
            _logger =  logger;
            _repository = repository;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Model State is Invalid or query was empty");
            }

            try
            {
                if(page < 1) page = 1;
                if(pageSize < 1) pageSize = 10;

                var result = await _elasticService.SearchDocumentAsync(query, page, pageSize);
                return Ok(result);
            }catch(Exception)
            {
                _logger.LogError("Error searching the document for query: " + query);
                return StatusCode(500, "Internal Server Error");
            }
            
        }
        [HttpGet("{id}/open")]
        public async Task<IActionResult> OpenInSystemViewer([FromRoute]string id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var doc = await _repository.GetByIdAsync(id);

            if(doc is null) return NotFound("File was not found");

            if(!System.IO.File.Exists(doc.Path)) return NotFound("File does not exst on disk");

            try
            {
                System.Diagnostics.Process.Start("open", doc.Path);
                return Ok(new {message = "file opened successfully"});
            }catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to open file locally");
                return StatusCode(500, "Failure to launch file");
            }
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download(string id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Model state was invalid");
            }

            Document? doc = await _repository.GetByIdAsync(id);
            if(doc is null) return NotFound("Unable to find the file with id: "+ id);

            if(!System.IO.File.Exists(doc.Path)) return NotFound("File you are searching for doesn't exist");

            var filestream =  new FileStream(doc.Path, FileMode.Open, FileAccess.Read);

            string mimeType = "application/pdf";

            return File(filestream, mimeType, Path.GetFileName(doc.Path));
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if(!ModelState.IsValid || file is null || file.Length == 0)
            {
                return BadRequest("No File Uploaded or Model state is invalid");
            }

            string extension = Path.GetExtension(file.FileName).ToLower();
            if(extension != ".pdf" && extension != ".zip" && extension != ".txt")
            {
                return BadRequest("Extension not supported, use(.pdf, .zip, .txt) files for now");
            }

            string ingestPath = "/tmp/vault_ingest";
            Directory.CreateDirectory(ingestPath);

            string targetFile = Path.Combine(ingestPath, file.FileName);

            if (System.IO.File.Exists(targetFile))
            {
                targetFile = Path.Combine(ingestPath, $"{Guid.NewGuid()}_{file.FileName}");
            }

            using(var stream = new FileStream(targetFile, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new {message = "File uploaded and queued for processing"});
        }
    }
}
