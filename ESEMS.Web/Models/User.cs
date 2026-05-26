using System.ComponentModel.DataAnnotations.Schema;
using ESEMS.Web.Models.APQC;

namespace ESEMS.Web.Models;

/// <summary>
/// Application user - plain POCO class (not mapped to database).
/// Navigation properties in other models reference this type for backward compatibility.
/// The actual database user table is managed by CustomUser.
/// </summary>
[NotMapped]
public class User
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? NormalizedUserName { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    /// <summary>
    /// First name in English
    /// </summary>
    public string FirstNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Last name in English
    /// </summary>
    public string LastNameEn { get; set; } = string.Empty;

    /// <summary>
    /// First name in Arabic
    /// </summary>
    public string? FirstNameAr { get; set; }

    /// <summary>
    /// Last name in Arabic
    /// </summary>
    public string? LastNameAr { get; set; }

    /// <summary>
    /// Job title in English
    /// </summary>
    public string? JobTitleEn { get; set; }

    /// <summary>
    /// Job title in Arabic
    /// </summary>
    public string? JobTitleAr { get; set; }

    /// <summary>
    /// Organization unit ID
    /// </summary>
    public int? OrganizationUnitId { get; set; }

    /// <summary>
    /// Employee ID
    /// </summary>
    public string? EmployeeId { get; set; }

    /// <summary>
    /// Windows domain username (for Windows Authentication)
    /// </summary>
    public string? WindowsUsername { get; set; }

    /// <summary>
    /// Whether the user is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Preferred language (en or ar)
    /// </summary>
    public string PreferredLanguage { get; set; } = "en";

    /// <summary>
    /// Profile picture URL
    /// </summary>
    public string? ProfilePictureUrl { get; set; }

    /// <summary>
    /// Last login date
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Created date
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Updated date
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public OrganizationUnit? OrganizationUnit { get; set; }

    /// <summary>
    /// Gets the full name in English
    /// </summary>
    public string FullNameEn => $"{FirstNameEn} {LastNameEn}".Trim();

    /// <summary>
    /// Gets the full name in Arabic
    /// </summary>
    public string? FullNameAr => !string.IsNullOrEmpty(FirstNameAr) && !string.IsNullOrEmpty(LastNameAr)
        ? $"{FirstNameAr} {LastNameAr}".Trim()
        : null;

    /// <summary>
    /// Gets the localized full name based on current culture
    /// </summary>
    public string GetLocalizedFullName()
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        if (culture.StartsWith("ar") && !string.IsNullOrEmpty(FullNameAr))
        {
            return FullNameAr;
        }
        return FullNameEn;
    }

    /// <summary>
    /// Gets the localized job title based on current culture
    /// </summary>
    public string? GetLocalizedJobTitle()
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        if (culture.StartsWith("ar") && !string.IsNullOrEmpty(JobTitleAr))
        {
            return JobTitleAr;
        }
        return JobTitleEn;
    }
}

