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
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Model State is Invalid or query was empty");
            }

            try
            {
                var results = await _elasticService.SearchDocumentAsync(query);
                return Ok(results);
            }catch(Exception)
            {
                _logger.LogError("Error searching the document for query: " + query);
                return StatusCode(500, "Internal Server Error");
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
    }
}
