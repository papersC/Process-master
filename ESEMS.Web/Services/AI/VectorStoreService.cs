using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;

namespace ESEMS.Web.Services.AI;

/// <summary>
/// In-memory keyword-based search index for RAG pipeline.
/// Indexes all ESEMS entities for semantic-like search.
/// </summary>
public class VectorStoreService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VectorStoreService> _logger;
    private List<IndexedDocument> _documents = new();
    private DateTime _lastIndexed = DateTime.MinValue;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "do", "does", "did", "will", "would", "could",
        "should", "may", "might", "shall", "can", "need", "dare", "ought",
        "used", "to", "of", "in", "for", "on", "with", "at", "by", "from",
        "as", "into", "through", "during", "before", "after", "above", "below",
        "between", "out", "off", "over", "under", "again", "further", "then",
        "once", "and", "but", "or", "nor", "not", "so", "yet", "both",
        "في", "من", "إلى", "على", "عن", "مع", "هو", "هي", "هم", "هذا", "هذه",
        "التي", "الذي", "ذلك", "تلك", "كان", "كانت", "يكون", "أن", "لا", "ما"
    };

    public VectorStoreService(IServiceProvider serviceProvider, ILogger<VectorStoreService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task IndexAllDataAsync()
    {
        if (!await _indexLock.WaitAsync(TimeSpan.FromSeconds(5)))
            return; // Skip if already indexing

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var docs = new List<IndexedDocument>();

            // Index Processes
            var processes = await context.Processes.Where(p => !p.IsDeleted)
                .Include(p => p.ProcessGroup).ToListAsync();
            foreach (var p in processes)
            {
                docs.Add(new IndexedDocument
                {
                    EntityId = p.Id,
                    EntityType = "Process",
                    Content = $"{p.Code} {p.NameEn} {p.NameAr} {p.DescriptionEn} {p.DescriptionAr} {p.ProcessGroup?.NameEn} {p.Status}",
                    Title = p.NameEn
                });
            }

            // Index Services
            var services = await context.Services.Where(s => !s.IsDeleted).ToListAsync();
            foreach (var s in services)
            {
                docs.Add(new IndexedDocument
                {
                    EntityId = s.Id,
                    EntityType = "Service",
                    Content = $"{s.Code} {s.NameEn} {s.NameAr} {s.DescriptionEn} {s.DescriptionAr} {s.ServiceType} {s.Channel}",
                    Title = s.NameEn
                });
            }

            // Index Incidents
            var incidents = await context.Incidents.Where(i => !i.IsDeleted).ToListAsync();
            foreach (var inc in incidents)
            {
                docs.Add(new IndexedDocument
                {
                    EntityId = inc.Id,
                    EntityType = "Incident",
                    Content = $"{inc.IncidentNumber} {inc.NameEn} {inc.NameAr} {inc.DescriptionEn} {inc.Category} {inc.Priority} {inc.Status}",
                    Title = inc.NameEn
                });
            }

            // Index Risks
            var risks = await context.EnterpriseRisks.Where(r => !r.IsDeleted).ToListAsync();
            foreach (var r in risks)
            {
                docs.Add(new IndexedDocument
                {
                    EntityId = r.Id,
                    EntityType = "Risk",
                    Content = $"{r.RiskNumber} {r.NameEn} {r.NameAr} {r.DescriptionEn} {r.RiskLevel} L:{r.Likelihood} I:{r.Impact}",
                    Title = r.NameEn
                });
            }

            // Index Improvements
            var improvements = await context.ImprovementInitiatives.Where(i => !i.IsDeleted).ToListAsync();
            foreach (var imp in improvements)
            {
                docs.Add(new IndexedDocument
                {
                    EntityId = imp.Id,
                    EntityType = "Improvement",
                    Content = $"{imp.Code} {imp.TitleEn} {imp.TitleAr} {imp.DescriptionEn} {imp.Status} {imp.Quadrant} {imp.Priority}",
                    Title = imp.TitleEn
                });
            }

            // Index Problems
            var problems = await context.Problems.Where(p => !p.IsDeleted).ToListAsync();
            foreach (var prob in problems)
            {
                docs.Add(new IndexedDocument
                {
                    EntityId = prob.Id,
                    EntityType = "Problem",
                    Content = $"{prob.ProblemNumber} {prob.NameEn} {prob.NameAr} {prob.DescriptionEn} {prob.Status} {prob.Category}",
                    Title = prob.NameEn
                });
            }

            // Pre-compute keywords for each document
            foreach (var doc in docs)
                doc.Keywords = Tokenize(doc.Content);

            _documents = docs;
            _lastIndexed = DateTime.UtcNow;
            _logger.LogInformation("Vector store indexed {Count} documents", docs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index data for vector store");
        }
        finally
        {
            _indexLock.Release();
        }
    }

    public List<IndexedDocument> Search(string query, int topK = 15)
    {
        var queryKeywords = Tokenize(query);
        if (queryKeywords.Count == 0) return new();

        return _documents
            .Select(doc => new { Doc = doc, Score = CalculateScore(queryKeywords, doc.Keywords) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Doc)
            .ToList();
    }

    public bool IsStale => (DateTime.UtcNow - _lastIndexed).TotalMinutes > 30;
    public int DocumentCount => _documents.Count;

    internal static HashSet<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();

        return text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1 && !StopWords.Contains(w))
            .ToHashSet();
    }

    internal static int CalculateScore(HashSet<string> queryKeywords, HashSet<string> docKeywords)
    {
        return queryKeywords.Count(q => docKeywords.Any(d => d.Contains(q) || q.Contains(d)));
    }
}

public class IndexedDocument
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public HashSet<string> Keywords { get; set; } = new();
}
