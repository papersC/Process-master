using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.DocumentManagement;

namespace ESEMS.Web.Controllers.Api;

/// <summary>
/// API for the per-user "My Space" document library.
/// Every user has their own folder under wwwroot/uploads/myspace/{userId}/
/// and can only see/manage their own files.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
// RBAC-006 — [ApiController] does NOT apply anti-forgery by default. This
// controller exposes state-changing DELETE/PUT/POST endpoints scoped to the
// user's MySpace folder; without anti-forgery a cross-origin fetch with the
// auth cookie can delete or mutate any logged-in user's documents. Auto-
// validate so the SPA fetch wrapper's header is enforced on every write.
[AutoValidateAntiforgeryToken]
public class MySpaceController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MySpaceController> _logger;

    private static readonly string[] AllowedExtensions =
    {
        ".pdf", ".xlsx", ".xls", ".docx", ".doc",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp",
        ".csv", ".pptx", ".ppt", ".txt"
    };

    private const long MaxFileSize = 20 * 1024 * 1024;       // 20 MB per file
    private const long MaxMultiSize = 100 * 1024 * 1024;     // 100 MB per batch

    /// <summary>
    /// File signature (magic bytes) map for secondary content validation.
    /// If the first N bytes of an uploaded file don't match any known
    /// signature for the declared extension, the file is rejected. This
    /// stops attacks where a .exe is renamed to .pdf.
    /// </summary>
    private static readonly Dictionary<string, byte[][]> MagicBytes = new()
    {
        [".pdf"]  = [new byte[] { 0x25, 0x50, 0x44, 0x46 }],           // %PDF
        [".xlsx"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 }],           // PK (ZIP archive)
        [".xls"]  = [new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }],           // OLE compound
        [".docx"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 }],           // PK (ZIP archive)
        [".doc"]  = [new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }],           // OLE compound
        [".pptx"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 }],           // PK (ZIP archive)
        [".ppt"]  = [new byte[] { 0xD0, 0xCF, 0x11, 0xE0 }],           // OLE compound
        [".png"]  = [new byte[] { 0x89, 0x50, 0x4E, 0x47 }],           // ‰PNG
        [".jpg"]  = [new byte[] { 0xFF, 0xD8, 0xFF }],                 // JFIF/EXIF
        [".jpeg"] = [new byte[] { 0xFF, 0xD8, 0xFF }],
        [".gif"]  = [new byte[] { 0x47, 0x49, 0x46, 0x38 }],           // GIF8
        [".bmp"]  = [new byte[] { 0x42, 0x4D }],                       // BM
        // .csv and .txt are plain text — no magic bytes; validated by extension only.
    };

    /// <summary>
    /// Returns true if the file's leading bytes match at least one known
    /// signature for the given extension, or if no signature is on file
    /// (e.g. .txt/.csv where any byte content is valid).
    /// </summary>
    private static bool ValidateFileSignature(IFormFile file, string extension)
    {
        if (!MagicBytes.TryGetValue(extension, out var signatures))
            return true; // no known magic bytes for this extension — allow

        using var reader = new BinaryReader(file.OpenReadStream());
        var headerBytes = reader.ReadBytes(signatures.Max(s => s.Length));
        file.OpenReadStream().Position = 0; // rewind for the next consumer

        return signatures.Any(sig =>
            headerBytes.Length >= sig.Length &&
            headerBytes.Take(sig.Length).SequenceEqual(sig));
    }

    public MySpaceController(ApplicationDbContext db, IWebHostEnvironment env, ILogger<MySpaceController> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    /// <summary>
    /// List the current user's documents (optionally filtered).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search = null, [FromQuery] string? category = null)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var q = _db.UserDocuments.Where(d => d.UserId == userId && !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(d => d.OriginalName.Contains(s)
                             || (d.Description != null && d.Description.Contains(s))
                             || (d.Tags != null && d.Tags.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(d => d.Category == category);

        var docs = await q.OrderByDescending(d => d.UploadedAt)
            .Select(d => new
            {
                d.Id,
                d.OriginalName,
                d.ContentType,
                d.FileSize,
                d.Description,
                d.Tags,
                d.Category,
                d.UploadedAt,
                Url = $"/uploads/myspace/{d.UserId}/{d.FileName}"
            })
            .ToListAsync();

        return Ok(docs);
    }

    /// <summary>
    /// Upload a single file into My Space.
    /// </summary>
    [HttpPost("upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> Upload(IFormFile file,
        [FromForm] string? description = null,
        [FromForm] string? category = null,
        [FromForm] string? tags = null)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });
        if (file.Length > MaxFileSize)
            return BadRequest(new { error = "File size exceeds the 20 MB limit" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { error = $"File type {ext} is not allowed" });

        // Magic-byte signature check: prevents renaming a .exe to .pdf
        if (!ValidateFileSignature(file, ext))
        {
            _logger.LogWarning("MySpace upload rejected: file {FileName} has invalid signature for extension {Ext}", file.FileName, ext);
            return BadRequest(new { error = $"File content does not match the {ext} format" });
        }

        var savedName = $"{Guid.NewGuid()}{ext}";
        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "myspace", userId.ToString());

        // Same defence as UploadMultiple: surface filesystem permission failures with
        // an actionable message instead of a blank 500.
        try
        {
            Directory.CreateDirectory(uploadsDir);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "MySpace upload failed: cannot create uploads dir {Dir}", uploadsDir);
            return StatusCode(500, new { error = "Upload directory is not writable. Contact the administrator to grant write permission to the application pool identity on " + uploadsDir });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MySpace upload failed: cannot create uploads dir {Dir}", uploadsDir);
            return StatusCode(500, new { error = "Upload directory is not available: " + ex.Message });
        }

        var filePath = Path.Combine(uploadsDir, savedName);
        UserDocument doc;
        try
        {
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            doc = new UserDocument
            {
                UserId = userId,
                FileName = savedName,
                OriginalName = file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                FileSize = file.Length,
                Description = description,
                Tags = tags,
                Category = string.IsNullOrWhiteSpace(category) ? "General" : category!,
                UploadedAt = DateTime.UtcNow
            };

            _db.UserDocuments.Add(doc);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MySpace upload failed during file write or DB save (uploadsDir={Dir}, userId={UserId})", uploadsDir, userId);
            return StatusCode(500, new { error = "Could not save uploaded file: " + ex.Message });
        }

        _logger.LogInformation("MySpace upload: user {UserId} uploaded {FileName} ({Size} bytes)", userId, file.FileName, file.Length);

        return Ok(new
        {
            doc.Id,
            doc.OriginalName,
            doc.ContentType,
            doc.FileSize,
            doc.Description,
            doc.Tags,
            doc.Category,
            doc.UploadedAt,
            Url = $"/uploads/myspace/{userId}/{savedName}"
        });
    }

    /// <summary>
    /// Upload multiple files at once. Returns one entry per successfully
    /// stored file. Files exceeding the single-file limit or with
    /// disallowed extensions are silently skipped.
    /// </summary>
    [HttpPost("upload-multiple")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxMultiSize)]
    public async Task<IActionResult> UploadMultiple(List<IFormFile> files, [FromForm] string? category = null)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        if (files == null || files.Count == 0)
            return BadRequest(new { error = "No files provided" });

        // Wrap the whole storage + DB pipeline in try/catch so a filesystem permission
        // problem on the IIS host doesn't surface as a blank "Upload failed (500)" with
        // no detail. Production-side root cause has historically been the app-pool
        // identity lacking write permission to wwwroot\uploads\myspace\ — that throws
        // UnauthorizedAccessException on Directory.CreateDirectory or FileStream.Create.
        var uploadsDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "myspace", userId.ToString());
        try
        {
            Directory.CreateDirectory(uploadsDir);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "MySpace upload failed: cannot create uploads dir {Dir} (app-pool identity lacks write permission)", uploadsDir);
            return StatusCode(500, new { error = "Upload directory is not writable. Contact the administrator to grant write permission to the application pool identity on " + uploadsDir });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MySpace upload failed: cannot create uploads dir {Dir}", uploadsDir);
            return StatusCode(500, new { error = "Upload directory is not available: " + ex.Message });
        }

        var results = new List<object>();

        try
        {
            foreach (var file in files)
            {
                if (file.Length == 0 || file.Length > MaxFileSize) continue;
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext)) continue;

                var savedName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsDir, savedName);
                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var doc = new UserDocument
                {
                    UserId = userId,
                    FileName = savedName,
                    OriginalName = file.FileName,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    FileSize = file.Length,
                    Category = string.IsNullOrWhiteSpace(category) ? "General" : category!,
                    UploadedAt = DateTime.UtcNow
                };
                _db.UserDocuments.Add(doc);

                results.Add(new
                {
                    doc.Id,
                    doc.OriginalName,
                    doc.ContentType,
                    doc.FileSize,
                    doc.Category,
                    doc.UploadedAt,
                    Url = $"/uploads/myspace/{userId}/{savedName}"
                });
            }

            await _db.SaveChangesAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MySpace upload failed during file write or DB save (uploadsDir={Dir}, userId={UserId})", uploadsDir, userId);
            return StatusCode(500, new { error = "Could not save uploaded files: " + ex.Message });
        }
    }

    /// <summary>
    /// Soft-delete a document (only the owner can delete).
    /// Keeps the physical file so any existing ProcessDocument links remain resolvable.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var doc = await _db.UserDocuments.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
        if (doc == null) return NotFound();

        doc.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Update document metadata (description / category / tags).
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateDocDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var doc = await _db.UserDocuments.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
        if (doc == null) return NotFound();

        if (dto.Description != null) doc.Description = dto.Description;
        if (dto.Category != null) doc.Category = dto.Category;
        if (dto.Tags != null) doc.Tags = dto.Tags;
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Stream a document's content with its original filename.
    /// The owner always has access; other users must have an active link
    /// to the document via a ProcessDocument (or similar) to download it.
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(string id)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var doc = await _db.UserDocuments.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
        if (doc == null) return NotFound();

        // Owner always has access
        if (doc.UserId != userId)
        {
            // Non-owners: allow if any ProcessDocument links this file
            var hasLink = await _db.ProcessDocuments.AnyAsync(pd => pd.UserDocumentId == id);
            if (!hasLink) return Forbid();
        }

        var filePath = Path.Combine(_env.WebRootPath, "uploads", "myspace", doc.UserId.ToString(), doc.FileName);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        return PhysicalFile(filePath, doc.ContentType, doc.OriginalName);
    }

    public class UpdateDocDto
    {
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Tags { get; set; }
    }
}
