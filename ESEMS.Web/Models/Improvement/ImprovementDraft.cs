using System.ComponentModel.DataAnnotations;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Per-user, in-progress snapshot of the Improvements wizard form. Lets users
/// abandon a wizard mid-flow ("had to leave my desk") and resume later
/// without losing the click-through they already invested. The payload is
/// the wizard's form-data serialised as JSON; the controller hydrates the
/// wizard view by sending it back via a ?draftId=X query param.
///
/// Only the row's owner can read / update / delete their drafts (filter on
/// OwnerId in every controller action). Drafts are NOT linked to a saved
/// ImprovementInitiative — a draft becomes nothing once submitted (the user
/// or controller deletes it on successful submit).
/// </summary>
public class ImprovementDraft
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Username of the wizard operator. Must match User.Identity.Name on read.</summary>
    [Required, MaxLength(256)]
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// Best-effort label so the drafts list shows something more useful than
    /// "Untitled draft". Pulled from the wizard's TitleEn at save time;
    /// falls back to a timestamp string if the user hadn't typed a title yet.
    /// </summary>
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Wizard form-data serialised as JSON. Schema is whatever
    /// getWizardSnapshot() in Wizard.cshtml emits — opaque to the server.</summary>
    [Required]
    public string PayloadJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
