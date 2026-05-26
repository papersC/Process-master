using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.Services;

/// <summary>
/// 1:1 sidecar to <see cref="Service"/> carrying the bilingual catalog content
/// published to citizens — long-form narrative that doesn't belong on the
/// operational Service row. Sourced from MBRHE_Services_Catalog_Populated.xlsx.
///
/// Lifecycle is independent from the parent Service: a Service may exist
/// operationally for months before its catalog content is drafted, reviewed,
/// and published via <see cref="IsPublished"/>. No <c>IsDeleted</c> — if the
/// Service is soft-deleted, the catalog row goes with it (cascade).
/// </summary>
public class ServiceCatalogInfo
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string ServiceId { get; set; } = string.Empty;

    // ─── Structured catalog facts ─────────────────────────────────────────
    // Duration: value + unit. Replaces the prior DurationEn/Ar narrative
    // fields — "15 working days" is two pieces of information (15,
    // BusinessDays), not one English sentence. Leave both NULL when the
    // service doesn't have a fixed SLA expressed in the catalog.
    public decimal? DurationValue { get; set; }
    public TimeUnit? DurationUnit { get; set; }

    // Fee: amount + IsFree flag + optional small qualifier. Replaces the
    // prior FeesEn/Ar narratives. IsFree=true is canonical for free
    // services (don't store FeeAmount=0 to mean free — explicit flag).
    // FeeNote is bounded free text (200 chars) for qualifiers that the
    // structured fields can't express, e.g. "Belongs to Dubai Municipality"
    // or "Includes a 25 AED service charge". Currency stays AED for now —
    // promote to its own field if the org ever quotes in another currency.
    public decimal? FeeAmount { get; set; }
    public bool IsFree { get; set; }
    [MaxLength(200)]
    public string? FeeNote { get; set; }

    // Delivery channels: CSV of <see cref="ServiceDeliveryChannel"/> names.
    // Multi-select — a service can be available on several channels at once.
    // Stored as text rather than a junction table because the value set is
    // small (10 options) and the access pattern is "render the chip list on
    // the service page"; full M2M would over-engineer.
    [MaxLength(500)]
    public string? DeliveryChannels { get; set; }

    // ─── New domain content not represented on Service ─────────────────────
    // EligibleCategories and TargetAudience were 100% identical across all
    // 35 filled catalog rows — merged into a single field here.
    // MaxLength 4000 bounds storage and prevents oversized POST bodies; the
    // real client catalog rows max out around 2 KB.
    [MaxLength(4000)] public string? TargetAudienceEn { get; set; }
    [MaxLength(4000)] public string? TargetAudienceAr { get; set; }

    // Multi-line application requirements / documents required (1- 2- 3-).
    [MaxLength(4000)] public string? PreConditionsEn { get; set; }
    [MaxLength(4000)] public string? PreConditionsAr { get; set; }

    // Usage restrictions / ضوابط الانتفاع — what the beneficiary must comply
    // with after receiving the service.
    [MaxLength(4000)] public string? PoliciesEn { get; set; }
    [MaxLength(4000)] public string? PoliciesAr { get; set; }

    // Step-by-step procedure as it appears to the customer.
    [MaxLength(4000)] public string? ProcedureEn { get; set; }
    [MaxLength(4000)] public string? ProcedureAr { get; set; }

    // (Category lives on Service directly — it's classification, not narrative.)

    // ─── Publishing workflow ───────────────────────────────────────────────
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? PublishedById { get; set; }

    // ─── Provenance (catalog rows usually arrive via bulk import) ─────────
    [MaxLength(256)]
    public string? SourceReference { get; set; }

    // ─── Audit ────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }
    public int Version { get; set; } = 1;

    [ForeignKey(nameof(ServiceId))]
    public Service? Service { get; set; }

    /// <summary>Parses <see cref="DeliveryChannels"/> CSV into the enum list.
    /// Unknown / malformed entries are silently dropped.</summary>
    public List<ServiceDeliveryChannel> GetDeliveryChannelList()
    {
        if (string.IsNullOrWhiteSpace(DeliveryChannels)) return new();
        return DeliveryChannels
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .Select(c => Enum.TryParse<ServiceDeliveryChannel>(c, out var v) ? v : (ServiceDeliveryChannel?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();
    }

    /// <summary>Sets <see cref="DeliveryChannels"/> from a list, deduplicating
    /// and stable-ordering by enum value.</summary>
    public void SetDeliveryChannelList(IEnumerable<ServiceDeliveryChannel> channels)
    {
        DeliveryChannels = string.Join(",", channels.Distinct().OrderBy(c => (int)c).Select(c => c.ToString()));
        if (string.IsNullOrWhiteSpace(DeliveryChannels)) DeliveryChannels = null;
    }
}
