using ESEMS.Web.Models.APQC;

namespace ESEMS.Web.Models.ViewModels;

/// <summary>
/// Bag for the _ResponsibilityPicker partial. Controllers populate this
/// when rendering any Create/Edit form that should let the user link the
/// parent entity (Process, Service, …) to OrganizationUnitResponsibility
/// rows via M2M.
/// </summary>
public class ResponsibilityPickerVM
{
    /// <summary>Name of the posted form field carrying the selected IDs (List&lt;string&gt;).</summary>
    public string FieldName { get; set; } = "SelectedResponsibilityIds";

    /// <summary>Responsibilities already linked to the parent entity — render as chips on first paint.</summary>
    public List<OrganizationUnitResponsibility> Selected { get; set; } = new();

    /// <summary>
    /// When supplied, the typeahead floats this unit's responsibilities to
    /// the top of the dropdown. Set this to the parent entity's OwningUnitId
    /// so the common case is one keystroke away.
    /// </summary>
    public string? PreferredUnitId { get; set; }
}
