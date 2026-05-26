using ESEMS.Web.Models.Common;

namespace ESEMS.Web.Models.Services;

/// <summary>
/// 360 Service Assessment for tracking before/after status of 7 criteria per service (Draft8).
/// Criteria: Automation, Self-Service, Data Integration, Proactivity, Integrated Services, No Physical Attendance, Unified Channels
/// </summary>
public class ServiceAssessment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Reference to the assessed service</summary>
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>Assessment period label (e.g., "Before", "After", "2023", "2024")</summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>Automation level (0=Not Auto, 1=Semi Auto, 2=Fully Auto)</summary>
    public int Automation { get; set; }

    /// <summary>Self-Service level (0=Not Self, 1=Partial Self, 2=Fully Self)</summary>
    public int SelfService { get; set; }

    /// <summary>Data Integration level (0=Internal Integ., 1=Partial Integ., 2=Full Integ.)</summary>
    public int DataIntegration { get; set; }

    /// <summary>Proactivity level (0=Not Applicable, 1=Level 1, 2=Level 2, 3=Level 3)</summary>
    public int Proactivity { get; set; }

    /// <summary>Integrated Services level (0=Partial Integ., 1=Full Integ.)</summary>
    public int IntegratedServices { get; set; }

    /// <summary>No Physical Attendance (0=Visit Req., 1=No Visit)</summary>
    public int NoPhysicalAttendance { get; set; }

    /// <summary>Unified Channels (0=Entity Channel, 1=Unified Channel)</summary>
    public int UnifiedChannels { get; set; }

    /// <summary>Assessment date</summary>
    public DateTime AssessmentDate { get; set; } = DateTime.UtcNow;

    /// <summary>Additional notes</summary>
    public string? Notes { get; set; }

    // Navigation property
    public Service? Service { get; set; }
}

