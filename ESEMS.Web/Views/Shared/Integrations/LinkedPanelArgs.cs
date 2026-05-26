namespace ESEMS.Web.Views.Shared.Integrations;

/// <summary>
/// Argument shape for the _LinkedRisks / _LinkedKpis partials. Lives next to the views
/// (Views\Shared\Integrations\) so the model and the partial travel together.
/// </summary>
public sealed class LinkedPanelArgs
{
    /// <summary>One of <see cref="ESEMS.Web.Services.Integrations.Contracts.LinkedEntityType"/>.</summary>
    public required string EntityType { get; init; }

    /// <summary>Local primary key of the entity (Process.Id, Service.Id, etc.).</summary>
    public required string EntityId { get; init; }

    /// <summary>Cap on items pulled from the external system, defends against runaway responses.</summary>
    public int MaxItems { get; init; } = 25;

    /// <summary>Optional id attribute for the wrapper card — used when the page wants to
    /// hash-link to this section, e.g. anchor-nav `#linked-risks`.</summary>
    public string? AnchorId { get; init; }
}
