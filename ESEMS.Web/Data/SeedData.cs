using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.ServiceManagement;
using ESEMS.Web.Models.AssetManagement;
using ESEMS.Web.Models.RiskManagement;
using ESEMS.Web.Models.Feedback;
using ESEMS.Web.Models.SLA;

namespace ESEMS.Web.Data;

/// <summary>
/// Comprehensive database seeding for ESEMS application
/// Includes APQC hierarchy, BPMN diagrams, ISO-compliant data
/// </summary>
public static class SeedData
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        Console.WriteLine("=== SEED DATA: Starting seed process ===");

        // Check if we already have data - if OrganizationUnits exist, skip seeding
        var hasOrgUnits = await context.OrganizationUnits.AnyAsync();
        var hasCategories = await context.Categories.AnyAsync();
        Console.WriteLine($"=== SEED DATA: Has existing org units: {hasOrgUnits}, categories: {hasCategories} ===");

        // Skip seeding if we already have organization units (imported from Excel)
        if (hasOrgUnits)
        {
            Console.WriteLine("=== SEED DATA: Skipping seed - data already exists (from Excel import or previous seed) ===");
            return;
        }

        // Create Organization Units (expanded structure)
        var orgUnits = CreateOrganizationUnits();
        await context.OrganizationUnits.AddRangeAsync(orgUnits);
        await context.SaveChangesAsync();

        // Seed JobPosition catalog — generic positions used for RACI role-level
        // assignment. MBRHE will refine this list during production rollout.
        var jobRoles = CreateJobPositions();
        await context.JobPositions.AddRangeAsync(jobRoles);
        await context.SaveChangesAsync();

        // Create Strategic Objectives
        var objectives = CreateStrategicObjectives();
        await context.StrategicObjectives.AddRangeAsync(objectives);
        await context.SaveChangesAsync();

        // Create System Definitions
        var systems = CreateSystemDefinitions();
        await context.SystemDefinitions.AddRangeAsync(systems);
        await context.SaveChangesAsync();

        // Create APQC Categories
        var categories = CreateCategories(orgUnits);
        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();

        // Create Process Groups
        var processGroups = CreateProcessGroups(categories, orgUnits);
        await context.ProcessGroups.AddRangeAsync(processGroups);
        await context.SaveChangesAsync();

        // Create Processes with BPMN diagrams
        Console.WriteLine("=== SEED DATA: Creating processes with BPMN diagrams... ===");
        var processes = CreateProcesses(processGroups, orgUnits, objectives, systems);
        var processesWithBpmn = processes.Count(p => !string.IsNullOrWhiteSpace(p.BpmnDiagram));
        Console.WriteLine($"=== SEED DATA: Created {processes.Count} processes, {processesWithBpmn} with BPMN diagrams ===");
        await context.Processes.AddRangeAsync(processes);
        await context.SaveChangesAsync();
        Console.WriteLine("=== SEED DATA: Processes saved to database ===");

        // Create Activities (APQC Level 4)
        var activities = CreateActivities(processes, orgUnits);
        await context.Activities.AddRangeAsync(activities);
        await context.SaveChangesAsync();

        // Create Tasks (APQC Level 5)
        var tasks = CreateTasks(activities, orgUnits, systems);
        await context.ProcessTasks.AddRangeAsync(tasks);
        await context.SaveChangesAsync();

        // Create Services with SLA
        var services = CreateServices(orgUnits, objectives);
        await context.Services.AddRangeAsync(services);
        await context.SaveChangesAsync();

        // Create Improvement Initiatives
        var improvements = CreateImprovements(processes, services, orgUnits);
        await context.ImprovementInitiatives.AddRangeAsync(improvements);
        await context.SaveChangesAsync();

        // Create Change Requests
        var changeRequests = CreateChangeRequests(processes, services, orgUnits);
        await context.ChangeRequests.AddRangeAsync(changeRequests);
        await context.SaveChangesAsync();

        // Create Process Maturity Assessments (Draft7 - 7 APQC Pillars)
        var maturityAssessments = CreateProcessMaturityAssessments();
        await context.ProcessMaturityAssessments.AddRangeAsync(maturityAssessments);
        await context.SaveChangesAsync();

        // Create 360 Service Assessments (Draft8 - 7 Criteria × 12 Services)
        var serviceAssessments = CreateServiceAssessments(services);
        await context.ServiceAssessments.AddRangeAsync(serviceAssessments);
        await context.SaveChangesAsync();

        // Create KPI Trends (Draft7 - Historical Performance)
        var kpiTrends = CreateKPITrends();
        await context.KPITrends.AddRangeAsync(kpiTrends);
        await context.SaveChangesAsync();

        // Create ISO Standards (Draft7 - 22+ Standards)
        var isoStandards = CreateISOStandards();
        await context.ISOStandards.AddRangeAsync(isoStandards);
        await context.SaveChangesAsync();

        Console.WriteLine("=== SEED DATA: All APQC + Draft7/Draft8 data seeded successfully! ===");

        // === ISO Module Seeding ===

        // Asset Categories and Assets (ISO 55001)
        var assetCategories = CreateAssetCategories();
        await context.AssetCategories.AddRangeAsync(assetCategories);
        await context.SaveChangesAsync();

        var assets = CreateAssets(assetCategories, processes, orgUnits);
        await context.Assets.AddRangeAsync(assets);
        await context.SaveChangesAsync();

        // Risk Categories and Risks (ISO 31000)
        var riskCategories = CreateRiskCategories();
        await context.RiskCategories.AddRangeAsync(riskCategories);
        await context.SaveChangesAsync();

        var risks = CreateEnterpriseRisks(riskCategories, processes, orgUnits);
        await context.EnterpriseRisks.AddRangeAsync(risks);
        await context.SaveChangesAsync();

        // Feedback Categories and Feedback (ISO 9001)
        var feedbackCategories = CreateFeedbackCategories(orgUnits);
        await context.FeedbackCategories.AddRangeAsync(feedbackCategories);
        await context.SaveChangesAsync();

        var feedbacks = CreateCustomerFeedback(feedbackCategories, services, processes, orgUnits);
        await context.CustomerFeedbacks.AddRangeAsync(feedbacks);
        await context.SaveChangesAsync();

        // SLA Definitions (ISO 20000-1)
        var slaDefinitions = CreateSLADefinitions(services, orgUnits);
        await context.SLADefinitions.AddRangeAsync(slaDefinitions);
        await context.SaveChangesAsync();

        // Incidents and Problems (ISO 20000-1)
        var incidents = CreateIncidents(services, processes, orgUnits);
        await context.Incidents.AddRangeAsync(incidents);
        await context.SaveChangesAsync();

        var problems = CreateProblems(services, processes, orgUnits);
        await context.Problems.AddRangeAsync(problems);
        await context.SaveChangesAsync();

        // Maintenance Schedules & Records (ISO 55001)
        var maintenanceSchedules = CreateMaintenanceSchedules(assets);
        await context.MaintenanceSchedules.AddRangeAsync(maintenanceSchedules);
        await context.SaveChangesAsync();

        var maintenanceRecords = CreateMaintenanceRecords(assets, maintenanceSchedules);
        await context.MaintenanceRecords.AddRangeAsync(maintenanceRecords);
        await context.SaveChangesAsync();

        // Risk Action Plans (ISO 31000)
        var riskActionPlans = CreateRiskActionPlans(risks);
        await context.RiskActionPlans.AddRangeAsync(riskActionPlans);
        await context.SaveChangesAsync();

        // SLA Breaches (ISO 20000-1)
        var slaBreaches = CreateSLABreaches(slaDefinitions, incidents);
        await context.SLABreaches.AddRangeAsync(slaBreaches);
        await context.SaveChangesAsync();

        // Comments
        var incidentComments = CreateIncidentComments(incidents);
        await context.IncidentComments.AddRangeAsync(incidentComments);
        await context.SaveChangesAsync();

        var problemComments = CreateProblemComments(problems);
        await context.ProblemComments.AddRangeAsync(problemComments);
        await context.SaveChangesAsync();

        var crComments = CreateChangeRequestComments(changeRequests);
        await context.ChangeRequestComments.AddRangeAsync(crComments);
        await context.SaveChangesAsync();

        // Junction tables
        var assetRisks = CreateAssetRisks(assets, risks);
        await context.AssetRisks.AddRangeAsync(assetRisks);
        await context.SaveChangesAsync();

        var serviceAssets = CreateServiceAssets(services, assets);
        await context.ServiceAssets.AddRangeAsync(serviceAssets);
        await context.SaveChangesAsync();

        var serviceRisks = CreateServiceRisks(services, risks);
        await context.ServiceRisks.AddRangeAsync(serviceRisks);
        await context.SaveChangesAsync();

        var crAssets = CreateChangeRequestAssets(changeRequests, assets);
        await context.ChangeRequestAssets.AddRangeAsync(crAssets);
        await context.SaveChangesAsync();

        var crRisks = CreateChangeRequestRisks(changeRequests, risks);
        await context.ChangeRequestRisks.AddRangeAsync(crRisks);
        await context.SaveChangesAsync();

        // Process/Service Measurements & Risks
        var processRisksList = CreateProcessRisks(processes, risks);
        await context.ProcessRisks.AddRangeAsync(processRisksList);
        await context.SaveChangesAsync();

        var processMeasurements = CreateProcessMeasurements(processes);
        await context.ProcessMeasurements.AddRangeAsync(processMeasurements);
        await context.SaveChangesAsync();

        var serviceMeasurements = CreateServiceMeasurements(services);
        await context.ServiceMeasurements.AddRangeAsync(serviceMeasurements);
        await context.SaveChangesAsync();

        // RACI Matrix
        var processRacis = CreateProcessRacis(processes, orgUnits);
        await context.ProcessRacis.AddRangeAsync(processRacis);
        await context.SaveChangesAsync();

        var activityRacis = CreateActivityRacis(activities, orgUnits);
        await context.ActivityRacis.AddRangeAsync(activityRacis);
        await context.SaveChangesAsync();

        var taskRacis = CreateTaskRacis(tasks, orgUnits);
        await context.TaskRacis.AddRangeAsync(taskRacis);
        await context.SaveChangesAsync();

        // Improvement Actions & Measurements
        var improvementActions = CreateImprovementActions(improvements);
        await context.ImprovementActions.AddRangeAsync(improvementActions);
        await context.SaveChangesAsync();

        var improvementMeasurements = CreateImprovementMeasurements(improvements);
        await context.ImprovementMeasurements.AddRangeAsync(improvementMeasurements);
        await context.SaveChangesAsync();

        // BPMN Versions
        var bpmnVersions = CreateProcessBpmnVersions(processes);
        await context.ProcessBpmnVersions.AddRangeAsync(bpmnVersions);
        await context.SaveChangesAsync();

        Console.WriteLine("=== SEED DATA: All ISO module data seeded successfully! ===");
    }

    /// <summary>
    /// Creates MBRHE Organization Hierarchy based on official structure
    /// Includes Sectors, Departments, and Sections
    /// </summary>
    private static List<OrganizationUnit> CreateOrganizationUnits()
    {
        var orgUnits = new List<OrganizationUnit>();

        // OrganizationUnit.Id is now a DB identity (int) — never set client-side.
        // Hierarchy is wired through the Parent navigation property; EF assigns
        // identities on SaveChanges and resolves the parent FKs from the nav.
        // Local helper: build a unit, add it, and return it for use as a parent.
        OrganizationUnit Add(string nameEn, string nameAr, string code, int level,
            int displayOrder, string descEn, string descAr, OrganizationUnit? parent)
        {
            var u = new OrganizationUnit
            {
                NameEn = nameEn,
                NameAr = nameAr,
                Code = code,
                Level = level,
                IsActive = true,
                DisplayOrder = displayOrder,
                DescriptionEn = descEn,
                DescriptionAr = descAr,
                Parent = parent
            };
            orgUnits.Add(u);
            return u;
        }

        // ═══════════════════════════════════════════════════════════════════
        // LEVEL 0: MBRHE (Root Organization - CEO)
        // ═══════════════════════════════════════════════════════════════════

        var ceoRoot = Add("MBRHE", "مؤسسة محمد بن راشد للإسكان", "MBRHE", 0, 1,
            "Mohammed Bin Rashid Housing Establishment", "مؤسسة محمد بن راشد للإسكان", null);

        // ═══════════════════════════════════════════════════════════════════
        // LEVEL 1: CEO Direct Reports (Offices)
        // ═══════════════════════════════════════════════════════════════════

        Add("Internal Audit & Risk Management Office", "مكتب التدقيق الداخلي وإدارة المخاطر", "IAO", 1, 1, "Internal Audit & Risk Management Office", "مكتب التدقيق الداخلي وإدارة المخاطر", ceoRoot);

        Add("Chief Executive Office", "مكتب الرئيس التنفيذي", "CEOO", 1, 2, "Chief Executive Office", "مكتب الرئيس التنفيذي", ceoRoot);

        Add("Legal Affairs Office", "مكتب الشؤون القانونية", "LEG", 1, 3, "Legal Affairs Office", "مكتب الشؤون القانونية", ceoRoot);

        // ═══════════════════════════════════════════════════════════════════
        // LEVEL 1: Strategy & Development Department (under CEO)
        // ═══════════════════════════════════════════════════════════════════

        var str = Add("Strategy & Development Department", "إدارة الاستراتيجية والتطوير", "STR", 1, 4, "Strategy & Development Department", "إدارة الاستراتيجية والتطوير", ceoRoot);

        // Sections under Strategy
        Add("Strategy & Performance Section", "قسم الاستراتيجية والأداء", "STR-SP", 2, 1, "Strategy & Performance Section", "قسم الاستراتيجية والأداء", str);
        Add("Excellence & Institutional Leadership Section", "قسم التميز والريادة المؤسسية", "STR-EX", 2, 2, "Excellence & Institutional Leadership Section", "قسم التميز والريادة المؤسسية", str);
        Add("Housing Policy Section", "قسم السياسات الإسكانية", "STR-HP", 2, 3, "Housing Policy Section", "قسم السياسات الإسكانية", str);
        Add("Community Studies Section", "قسم الدراسات المجتمعية", "STR-CS", 2, 4, "Community Studies Section", "قسم الدراسات المجتمعية", str);

        // ═══════════════════════════════════════════════════════════════════
        // LEVEL 1: SECTORS (under CEO)
        // ═══════════════════════════════════════════════════════════════════

        // Corporate Support Sector (قطاع الدعم المؤسسي)
        var css = Add("Corporate Support Sector", "قطاع الدعم المؤسسي", "CSS", 1, 5,
            "Corporate Support Sector - Administrative and digital support services",
            "قطاع الدعم المؤسسي - الخدمات الإدارية والرقمية المساندة", ceoRoot);

        // Housing Sector (قطاع الإسكان)
        var hsg = Add("Housing Sector", "قطاع الإسكان", "HSG", 1, 6,
            "Housing Sector - Engineering, customer service, investment and branches",
            "قطاع الإسكان - الهندسة وخدمة المتعاملين والاستثمار والفروع", ceoRoot);

        // ═══════════════════════════════════════════════════════════════════
        // LEVEL 2: CORPORATE SUPPORT SECTOR - Departments
        // ═══════════════════════════════════════════════════════════════════

        var ssv = Add("Support Services Department", "إدارة الخدمات المساندة", "SSV", 2, 1, "Support Services Department", "إدارة الخدمات المساندة", css);

        // Sections under Support Services
        Add("Planning & Budget Section", "قسم التخطيط والموازنة", "SSV-PB", 3, 1, "Planning & Budget Section", "قسم التخطيط والموازنة", ssv);
        Add("Revenue & Collection Section", "قسم الإيرادات والتحصيل", "SSV-RC", 3, 2, "Revenue & Collection Section", "قسم الإيرادات والتحصيل", ssv);
        Add("Contracts & Procurement Section", "قسم العقود والمشتريات", "SSV-CP", 3, 3, "Contracts & Procurement Section", "قسم العقود والمشتريات", ssv);
        Add("Human Resources Section", "قسم الموارد البشرية", "SSV-HR", 3, 4, "Human Resources Section", "قسم الموارد البشرية", ssv);
        Add("Administrative Affairs Section", "قسم الشؤون الإدارية", "SSV-AD", 3, 5, "Administrative Affairs Section", "قسم الشؤون الإدارية", ssv);

        var dig = Add("Digital Transformation Department", "إدارة التحول الرقمي", "DIG", 2, 2, "Digital Transformation Department", "إدارة التحول الرقمي", css);

        // Sections under Digital Transformation
        Add("Smart Systems & Services Development Section", "قسم تطوير النظم والخدمات الذكية", "DIG-SD", 3, 1, "Smart Systems & Services Development Section", "قسم تطوير النظم والخدمات الذكية", dig);
        Add("Technical Support Services Section", "قسم خدمات الدعم التقني", "DIG-TS", 3, 2, "Technical Support Services Section", "قسم خدمات الدعم التقني", dig);

        var com = Add("Communication & Marketing Department", "إدارة الاتصال والتسويق", "COM", 2, 3, "Communication & Marketing Department", "إدارة الاتصال والتسويق", css);

        // Sections under Communication & Marketing
        Add("Communication Section", "قسم الاتصال", "COM-CM", 3, 1, "Communication Section", "قسم الاتصال", com);
        Add("Marketing Section", "قسم التسويق", "COM-MK", 3, 2, "Marketing Section", "قسم التسويق", com);

        // ═══════════════════════════════════════════════════════════════════
        // LEVEL 2: HOUSING SECTOR - Departments
        // ═══════════════════════════════════════════════════════════════════

        var eng = Add("Engineering Projects Department", "إدارة المشاريع الهندسية", "ENG", 2, 1, "Engineering Projects Department", "إدارة المشاريع الهندسية", hsg);

        // Sections under Engineering Projects
        Add("Planning & Design Section", "قسم التخطيط والتصميم", "ENG-PD", 3, 1, "Planning & Design Section", "قسم التخطيط والتصميم", eng);
        Add("Engineering Supervision Section", "قسم الرقابة الهندسية", "ENG-ES", 3, 2, "Engineering Supervision Section", "قسم الرقابة الهندسية", eng);
        Add("Maintenance Section", "قسم الصيانة", "ENG-MT", 3, 3, "Maintenance Section", "قسم الصيانة", eng);
        Add("Assets Section", "قسم الأصول", "ENG-AS", 3, 4, "Assets Section", "قسم الأصول", eng);

        var chp = Add("Customer Happiness Department", "إدارة إسعاد المتعاملين", "CHP", 2, 2, "Customer Happiness Department", "إدارة إسعاد المتعاملين", hsg);

        // Sections under Customer Happiness
        Add("Service Excellence Section", "قسم ريادة الخدمات", "CHP-SE", 3, 1, "Service Excellence Section", "قسم ريادة الخدمات", chp);
        Add("Customer Care Section", "قسم عناية المتعاملين", "CHP-CC", 3, 2, "Customer Care Section", "قسم عناية المتعاملين", chp);
        Add("Request Processing Section", "قسم معالجة الطلبات", "CHP-RP", 3, 3, "Request Processing Section", "قسم معالجة الطلبات", chp);

        var inv = Add("Investment Department", "إدارة الاستثمار", "INV", 2, 3, "Investment Department", "إدارة الاستثمار", hsg);

        // Sections under Investment
        Add("Business Sustainability Section", "قسم استدامة الأعمال", "INV-BS", 3, 1, "Business Sustainability Section", "قسم استدامة الأعمال", inv);
        Add("Housing Partnerships Section", "قسم الشراكات الإسكانية", "INV-HP", 3, 2, "Housing Partnerships Section", "قسم الشراكات الإسكانية", inv);

        var ext = Add("External Branches Department", "إدارة الفروع الخارجية", "EXT", 2, 4, "External Branches Department", "إدارة الفروع الخارجية", hsg);

        // Sections under External Branches
        Add("Dubai Integrated Housing Center", "مركز إسكان دبي المتكامل", "EXT-DI", 3, 1, "Dubai Integrated Housing Center", "مركز إسكان دبي المتكامل", ext);
        Add("Outsourcing Centers", "مراكز التعهيد", "EXT-OS", 3, 2, "Outsourcing Centers", "مراكز التعهيد", ext);

        return orgUnits;
    }

    /// <summary>
    /// Creates MBRHE Strategic Objectives (2022-2026)
    /// Based on official MBRHE strategic plan from Draft8
    /// </summary>
    private static List<StrategicObjective> CreateStrategicObjectives()
    {
        return new List<StrategicObjective>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Deliver an Exceptional Customer Experience with Proactive and Innovative Services", NameAr = "تقديم تجربة عملاء استثنائية بخدمات استباقية ومبتكرة", Code = "SO1", Level = 1, TargetYear = 2026, TargetValue = 98.75m, CurrentValue = 95, UnitOfMeasure = "%", DescriptionEn = "Achieve 98.75% customer happiness through proactive and innovative housing services", DescriptionAr = "تحقيق 98.75% سعادة العملاء من خلال خدمات إسكانية استباقية ومبتكرة", IsActive = true, DisplayOrder = 1 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Raise Awareness and Instill Confidence in the Housing Sector", NameAr = "رفع الوعي وبناء الثقة في القطاع الإسكاني", Code = "SO2", Level = 1, TargetYear = 2026, TargetValue = 90, CurrentValue = 82, UnitOfMeasure = "%", DescriptionEn = "Increase public awareness and confidence in MBRHE housing sector initiatives", DescriptionAr = "زيادة الوعي العام وبناء الثقة في مبادرات القطاع الإسكاني", IsActive = true, DisplayOrder = 2 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Enhance the Housing Ecosystem in Dubai", NameAr = "تعزيز المنظومة الإسكانية في دبي", Code = "SO3", Level = 1, TargetYear = 2026, TargetValue = 1000, CurrentValue = 750, UnitOfMeasure = "units", DescriptionEn = "Develop and advance the housing ecosystem across Dubai emirate", DescriptionAr = "تطوير وتعزيز المنظومة الإسكانية في إمارة دبي", IsActive = true, DisplayOrder = 3 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Transform into a Data-driven Housing Sector", NameAr = "التحول إلى قطاع إسكاني قائم على البيانات", Code = "SO4", Level = 1, TargetYear = 2026, TargetValue = 94.8m, CurrentValue = 75, UnitOfMeasure = "%", DescriptionEn = "Achieve 94.8% digital adoption through data-driven transformation", DescriptionAr = "تحقيق 94.8% تبني رقمي من خلال التحول القائم على البيانات", IsActive = true, DisplayOrder = 4 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Maximize and Diversify Financial Resources", NameAr = "تعظيم وتنويع الموارد المالية", Code = "SO5", Level = 1, TargetYear = 2026, TargetValue = 53, CurrentValue = 40, UnitOfMeasure = "M AED", DescriptionEn = "Achieve AED 53M in financial savings through optimization and diversification", DescriptionAr = "تحقيق 53 مليون درهم وفورات مالية من خلال التحسين والتنويع", IsActive = true, DisplayOrder = 5 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Increase Role of Partnerships in the Sustainable Development of the Housing Sector", NameAr = "تعزيز دور الشراكات في التنمية المستدامة للقطاع الإسكاني", Code = "SO6", Level = 1, TargetYear = 2026, TargetValue = 25, CurrentValue = 15, UnitOfMeasure = "partnerships", DescriptionEn = "Strengthen public-private partnerships for sustainable housing development", DescriptionAr = "تعزيز الشراكات بين القطاعين العام والخاص للتنمية الإسكانية المستدامة", IsActive = true, DisplayOrder = 6 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Enable Digital Transformation", NameAr = "تمكين التحول الرقمي", Code = "SO7", Level = 1, TargetYear = 2026, TargetValue = 100, CurrentValue = 80, UnitOfMeasure = "%", DescriptionEn = "Enable end-to-end digital transformation of housing services", DescriptionAr = "تمكين التحول الرقمي الشامل لخدمات الإسكان", IsActive = true, DisplayOrder = 7 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Instill a Culture of Pioneering, Innovation and Corporate Governance", NameAr = "ترسيخ ثقافة الريادة والابتكار والحوكمة المؤسسية", Code = "SO8", Level = 1, TargetYear = 2026, TargetValue = 95, CurrentValue = 85, UnitOfMeasure = "%", DescriptionEn = "Foster a culture of pioneering, innovation, and strong corporate governance", DescriptionAr = "تعزيز ثقافة الريادة والابتكار والحوكمة المؤسسية الرشيدة", IsActive = true, DisplayOrder = 8 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Improve Employee Experience and Corporate Identity", NameAr = "تحسين تجربة الموظفين والهوية المؤسسية", Code = "SO9", Level = 1, TargetYear = 2026, TargetValue = 94.9m, CurrentValue = 88, UnitOfMeasure = "%", DescriptionEn = "Enhance employee happiness to 94.9% and strengthen corporate identity", DescriptionAr = "تعزيز سعادة الموظفين إلى 94.9% وتقوية الهوية المؤسسية", IsActive = true, DisplayOrder = 9 }
        };
    }

    private static List<SystemDefinition> CreateSystemDefinitions()
    {
        return new List<SystemDefinition>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Housing Management System", NameAr = "نظام إدارة الإسكان", Code = "HMS", DisplayOrder = 1, DescriptionEn = "Core system for housing applications, allocations and grants management", DescriptionAr = "النظام الأساسي لإدارة طلبات الإسكان والتخصيصات والمنح", SystemType = "Core Business", Vendor = "Internal Development", SystemVersion = "3.5.0", IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Customer Portal", NameAr = "بوابة العملاء", Code = "CPL", DisplayOrder = 2, DescriptionEn = "Self-service portal for customers to apply and track requests", DescriptionAr = "بوابة الخدمة الذاتية للعملاء لتقديم الطلبات وتتبعها", SystemType = "Customer Facing", Vendor = "Internal Development", SystemVersion = "2.1.0", IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "ERP System", NameAr = "نظام تخطيط موارد المؤسسة", Code = "ERP", DisplayOrder = 3, DescriptionEn = "Enterprise Resource Planning for finance and HR", DescriptionAr = "تخطيط موارد المؤسسة للمالية والموارد البشرية", SystemType = "Enterprise", Vendor = "Oracle", SystemVersion = "R12", IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Document Management System", NameAr = "نظام إدارة الوثائق", Code = "DMS", DisplayOrder = 4, DescriptionEn = "Electronic document storage and workflow management", DescriptionAr = "تخزين الوثائق الإلكترونية وإدارة سير العمل", SystemType = "Support", Vendor = "SharePoint", SystemVersion = "Online", IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "CRM System", NameAr = "نظام إدارة علاقات العملاء", Code = "CRM", DisplayOrder = 5, DescriptionEn = "Customer relationship and complaint management", DescriptionAr = "إدارة علاقات العملاء والشكاوى", SystemType = "Customer Facing", Vendor = "Salesforce", SystemVersion = "Enterprise", IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Mobile Application", NameAr = "تطبيق الجوال", Code = "APP", DisplayOrder = 6, DescriptionEn = "Mobile app for customers and employees", DescriptionAr = "تطبيق الجوال للعملاء والموظفين", SystemType = "Customer Facing", Vendor = "Internal Development", SystemVersion = "1.8.0", IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Business Intelligence", NameAr = "ذكاء الأعمال", Code = "BI", DisplayOrder = 7, DescriptionEn = "Reporting and analytics platform", DescriptionAr = "منصة التقارير والتحليلات", SystemType = "Analytics", Vendor = "Power BI", SystemVersion = "Pro", IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "UAE Pass Integration", NameAr = "تكامل الهوية الرقمية", Code = "UAEPASS", DisplayOrder = 8, DescriptionEn = "Digital identity integration with UAE Pass", DescriptionAr = "تكامل الهوية الرقمية مع الهوية الإماراتية", SystemType = "Integration", Vendor = "UAE Government", SystemVersion = "2.0", IsActive = true }
        };
    }

    private static List<JobPosition> CreateJobPositions()
    {
        // Generic catalog covering the typical MBRHE staffing pattern.
        // MBRHE refines this list at deploy time to match their actual roster.
        return new List<JobPosition>
        {
            new() { Code = "DIR",       NameEn = "Director",          NameAr = "مدير الإدارة",          Category = "Leadership",     IsLeadership = true,  DisplayOrder = 1 },
            new() { Code = "DEP-DIR",   NameEn = "Deputy Director",   NameAr = "نائب المدير",           Category = "Leadership",     IsLeadership = true,  DisplayOrder = 2 },
            new() { Code = "SEC-HEAD",  NameEn = "Section Head",      NameAr = "رئيس قسم",              Category = "Leadership",     IsLeadership = true,  DisplayOrder = 3 },
            new() { Code = "SR-SPEC",   NameEn = "Senior Specialist", NameAr = "أخصائي أول",            Category = "Specialist",     IsLeadership = false, DisplayOrder = 4 },
            new() { Code = "SPEC",      NameEn = "Specialist",        NameAr = "أخصائي",                Category = "Specialist",     IsLeadership = false, DisplayOrder = 5 },
            new() { Code = "COORD",     NameEn = "Coordinator",       NameAr = "منسق",                  Category = "Administrative", IsLeadership = false, DisplayOrder = 6 },
            new() { Code = "REVIEWER",  NameEn = "Reviewer",          NameAr = "مراجع",                 Category = "Specialist",     IsLeadership = false, DisplayOrder = 7 },
            new() { Code = "OFFICER",   NameEn = "Officer",           NameAr = "موظف",                  Category = "Administrative", IsLeadership = false, DisplayOrder = 8 }
        };
    }

    private static List<Category> CreateCategories(List<OrganizationUnit> orgUnits)
    {
        return new List<Category>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Operating Processes", NameAr = "العمليات التشغيلية", Code = "1.0", DescriptionEn = "Core operating processes", DescriptionAr = "العمليات التشغيلية الأساسية" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Customer Services", NameAr = "خدمات العملاء", Code = "2.0", DescriptionEn = "Customer-facing services", DescriptionAr = "الخدمات الموجهة للعملاء" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Support Services", NameAr = "خدمات الدعم", Code = "3.0", DescriptionEn = "Internal support processes", DescriptionAr = "عمليات الدعم الداخلية" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Management Processes", NameAr = "العمليات الإدارية", Code = "4.0", DescriptionEn = "Management and governance", DescriptionAr = "الإدارة والحوكمة" }
        };
    }

    private static List<ProcessGroup> CreateProcessGroups(List<Category> categories, List<OrganizationUnit> orgUnits)
    {
        return new List<ProcessGroup>
        {
            // Operating Processes
            new() { Id = Guid.NewGuid().ToString(), CategoryId = categories[0].Id, NameEn = "Housing Application Processing", NameAr = "معالجة طلبات الإسكان", Code = "1.1" },
            new() { Id = Guid.NewGuid().ToString(), CategoryId = categories[0].Id, NameEn = "Property Management", NameAr = "إدارة العقارات", Code = "1.2" },
            new() { Id = Guid.NewGuid().ToString(), CategoryId = categories[0].Id, NameEn = "Construction & Maintenance", NameAr = "البناء والصيانة", Code = "1.3" },
            // Customer Services
            new() { Id = Guid.NewGuid().ToString(), CategoryId = categories[1].Id, NameEn = "Customer Inquiries", NameAr = "استفسارات العملاء", Code = "2.1" },
            new() { Id = Guid.NewGuid().ToString(), CategoryId = categories[1].Id, NameEn = "Complaints Management", NameAr = "إدارة الشكاوى", Code = "2.2" },
            // Support Services
            new() { Id = Guid.NewGuid().ToString(), CategoryId = categories[2].Id, NameEn = "IT Services", NameAr = "خدمات تقنية المعلومات", Code = "3.1" },
            new() { Id = Guid.NewGuid().ToString(), CategoryId = categories[2].Id, NameEn = "HR Services", NameAr = "خدمات الموارد البشرية", Code = "3.2" },
            new() { Id = Guid.NewGuid().ToString(), CategoryId = categories[2].Id, NameEn = "Financial Services", NameAr = "الخدمات المالية", Code = "3.3" },
            // Management Processes
            new() { Id = Guid.NewGuid().ToString(), CategoryId = categories[3].Id, NameEn = "Strategic Planning", NameAr = "التخطيط الاستراتيجي", Code = "4.1" },
            new() { Id = Guid.NewGuid().ToString(), CategoryId = categories[3].Id, NameEn = "Performance Management", NameAr = "إدارة الأداء", Code = "4.2" }
        };
    }

    private static List<Process> CreateProcesses(List<ProcessGroup> groups, List<OrganizationUnit> orgUnits, List<StrategicObjective> objectives, List<SystemDefinition> systems)
    {
        // Use comprehensive BPMN diagrams with full visual layout from templates
        var applicationSubmissionBpmn = BpmnDiagramTemplates.ApplicationSubmission;
        var eligibilityVerificationBpmn = BpmnDiagramTemplates.EligibilityVerification;
        var documentReviewBpmn = BpmnDiagramTemplates.DocumentReview;
        var approvalContractBpmn = BpmnDiagramTemplates.ApprovalContract;
        var customerInquiryBpmn = BpmnDiagramTemplates.CustomerInquiry;
        var complaintResolutionBpmn = BpmnDiagramTemplates.ComplaintResolution;
        var incidentManagementBpmn = BpmnDiagramTemplates.IncidentManagement;
        var employeeOnboardingBpmn = BpmnDiagramTemplates.EmployeeOnboarding;

        return new List<Process>
        {
            // Housing Application Processing - with BPMN diagrams
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[0].Id, NameEn = "Application Submission", NameAr = "تقديم الطلب", Code = "1.1.1", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 1, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[0].Id, SystemId = systems[0].Id, EstimatedDuration = 30, DurationUnit = TimeUnit.Minutes, HasDetailedBreakdown = true, BpmnDiagram = applicationSubmissionBpmn, DescriptionEn = "Process for customers to submit housing applications online", DescriptionAr = "عملية تقديم طلبات الإسكان إلكترونياً من قبل العملاء" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[0].Id, NameEn = "Eligibility Verification", NameAr = "التحقق من الأهلية", Code = "1.1.2", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 2, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[1].Id, SystemId = systems[0].Id, EstimatedDuration = 2, DurationUnit = TimeUnit.Days, HasDetailedBreakdown = true, BpmnDiagram = eligibilityVerificationBpmn, DescriptionEn = "Automated verification of applicant eligibility criteria", DescriptionAr = "التحقق الآلي من معايير أهلية مقدم الطلب" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[0].Id, NameEn = "Document Review", NameAr = "مراجعة المستندات", Code = "1.1.3", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 3, OwningUnitId = orgUnits[1].Id, SystemId = systems[3].Id, EstimatedDuration = 1, DurationUnit = TimeUnit.Days, HasDetailedBreakdown = true, BpmnDiagram = documentReviewBpmn, DescriptionEn = "Manual review and verification of submitted documents", DescriptionAr = "المراجعة والتحقق اليدوي من المستندات المقدمة" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[0].Id, NameEn = "Final Approval", NameAr = "الموافقة النهائية", Code = "1.1.4", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 4, OwningUnitId = orgUnits[1].Id, SystemId = systems[0].Id, EstimatedDuration = 3, DurationUnit = TimeUnit.Days, BpmnDiagram = approvalContractBpmn, DescriptionEn = "Committee review and final decision on application", DescriptionAr = "مراجعة اللجنة والقرار النهائي بشأن الطلب" },
            // Property Management
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[1].Id, NameEn = "Property Allocation", NameAr = "تخصيص العقار", Code = "1.2.1", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 1, OwningUnitId = orgUnits[1].Id, SystemId = systems[0].Id, EstimatedDuration = 5, DurationUnit = TimeUnit.Days, DescriptionEn = "Match approved applicants with available properties", DescriptionAr = "مطابقة المتقدمين المعتمدين مع العقارات المتاحة" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[1].Id, NameEn = "Lease Agreement", NameAr = "عقد الإيجار", Code = "1.2.2", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 2, OwningUnitId = orgUnits[1].Id, SystemId = systems[3].Id, EstimatedDuration = 2, DurationUnit = TimeUnit.Days, DescriptionEn = "Generate and sign lease agreements", DescriptionAr = "إنشاء وتوقيع عقود الإيجار" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[1].Id, NameEn = "Property Handover", NameAr = "تسليم العقار", Code = "1.2.3", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 3, OwningUnitId = orgUnits[1].Id, EstimatedDuration = 1, DurationUnit = TimeUnit.Days, DescriptionEn = "Physical handover of property to beneficiary", DescriptionAr = "التسليم الفعلي للعقار للمستفيد" },
            // Construction & Maintenance
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[2].Id, NameEn = "Maintenance Request Handling", NameAr = "معالجة طلبات الصيانة", Code = "1.3.1", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 1, OwningUnitId = orgUnits[1].Id, SystemId = systems[4].Id, EstimatedDuration = 4, DurationUnit = TimeUnit.Hours, DescriptionEn = "Receive and process maintenance requests", DescriptionAr = "استلام ومعالجة طلبات الصيانة" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[2].Id, NameEn = "Contractor Assignment", NameAr = "تعيين المقاول", Code = "1.3.2", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 2, OwningUnitId = orgUnits[1].Id, EstimatedDuration = 1, DurationUnit = TimeUnit.Days, DescriptionEn = "Assign qualified contractors for maintenance work", DescriptionAr = "تعيين مقاولين مؤهلين لأعمال الصيانة" },
            // Customer Inquiries - with BPMN
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[3].Id, NameEn = "Inquiry Registration", NameAr = "تسجيل الاستفسار", Code = "2.1.1", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 1, OwningUnitId = orgUnits[2].Id, StrategicObjectiveId = objectives[0].Id, SystemId = systems[4].Id, EstimatedDuration = 5, DurationUnit = TimeUnit.Minutes, BpmnDiagram = customerInquiryBpmn, DescriptionEn = "Register customer inquiries from all channels", DescriptionAr = "تسجيل استفسارات العملاء من جميع القنوات" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[3].Id, NameEn = "Inquiry Response", NameAr = "الرد على الاستفسار", Code = "2.1.2", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 2, OwningUnitId = orgUnits[2].Id, SystemId = systems[4].Id, EstimatedDuration = 15, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Provide response to customer inquiries", DescriptionAr = "تقديم الرد على استفسارات العملاء" },
            // Complaints Management - with BPMN
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[4].Id, NameEn = "Complaint Registration", NameAr = "تسجيل الشكوى", Code = "2.2.1", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 1, OwningUnitId = orgUnits[2].Id, SystemId = systems[4].Id, EstimatedDuration = 10, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Register customer complaints", DescriptionAr = "تسجيل شكاوى العملاء" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[4].Id, NameEn = "Complaint Investigation", NameAr = "التحقيق في الشكوى", Code = "2.2.2", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 2, OwningUnitId = orgUnits[2].Id, EstimatedDuration = 2, DurationUnit = TimeUnit.Days, DescriptionEn = "Investigate and analyze complaint root cause", DescriptionAr = "التحقيق وتحليل السبب الجذري للشكوى" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[4].Id, NameEn = "Complaint Resolution", NameAr = "حل الشكوى", Code = "2.2.3", ProcessType = ProcessType.Core, Status = ProcessStatus.Active, DisplayOrder = 3, OwningUnitId = orgUnits[2].Id, SystemId = systems[4].Id, EstimatedDuration = 1, DurationUnit = TimeUnit.Days, BpmnDiagram = complaintResolutionBpmn, DescriptionEn = "Resolve complaint and communicate with customer", DescriptionAr = "حل الشكوى والتواصل مع العميل" },
            // IT Services - with BPMN
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[5].Id, NameEn = "IT Support Request", NameAr = "طلب دعم تقني", Code = "3.1.1", ProcessType = ProcessType.Support, Status = ProcessStatus.Active, DisplayOrder = 1, OwningUnitId = orgUnits[3].Id, EstimatedDuration = 30, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Handle IT support requests from employees", DescriptionAr = "معالجة طلبات الدعم التقني من الموظفين" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[5].Id, NameEn = "System Access Request", NameAr = "طلب صلاحية نظام", Code = "3.1.2", ProcessType = ProcessType.Support, Status = ProcessStatus.Active, DisplayOrder = 2, OwningUnitId = orgUnits[3].Id, EstimatedDuration = 1, DurationUnit = TimeUnit.Days, DescriptionEn = "Process system access and permission requests", DescriptionAr = "معالجة طلبات الوصول والصلاحيات للأنظمة" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[5].Id, NameEn = "Incident Management", NameAr = "إدارة الحوادث", Code = "3.1.3", ProcessType = ProcessType.Support, Status = ProcessStatus.Active, DisplayOrder = 3, OwningUnitId = orgUnits[3].Id, EstimatedDuration = 4, DurationUnit = TimeUnit.Hours, BpmnDiagram = incidentManagementBpmn, DescriptionEn = "Manage and resolve IT incidents", DescriptionAr = "إدارة وحل الحوادث التقنية" },
            // HR Services - with BPMN
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[6].Id, NameEn = "Employee Onboarding", NameAr = "تهيئة الموظف الجديد", Code = "3.2.1", ProcessType = ProcessType.Support, Status = ProcessStatus.Active, DisplayOrder = 1, OwningUnitId = orgUnits[5].Id, SystemId = systems[2].Id, EstimatedDuration = 5, DurationUnit = TimeUnit.Days, BpmnDiagram = employeeOnboardingBpmn, DescriptionEn = "Complete onboarding for new employees", DescriptionAr = "إتمام تهيئة الموظفين الجدد" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[6].Id, NameEn = "Leave Management", NameAr = "إدارة الإجازات", Code = "3.2.2", ProcessType = ProcessType.Support, Status = ProcessStatus.Active, DisplayOrder = 2, OwningUnitId = orgUnits[5].Id, SystemId = systems[2].Id, EstimatedDuration = 1, DurationUnit = TimeUnit.Days, DescriptionEn = "Process employee leave requests", DescriptionAr = "معالجة طلبات إجازات الموظفين" },
            // Financial Services
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[7].Id, NameEn = "Payment Processing", NameAr = "معالجة الدفعات", Code = "3.3.1", ProcessType = ProcessType.Support, Status = ProcessStatus.Active, DisplayOrder = 1, OwningUnitId = orgUnits[4].Id, SystemId = systems[2].Id, EstimatedDuration = 2, DurationUnit = TimeUnit.Days, DescriptionEn = "Process payments to beneficiaries and vendors", DescriptionAr = "معالجة الدفعات للمستفيدين والموردين" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[7].Id, NameEn = "Budget Management", NameAr = "إدارة الميزانية", Code = "3.3.2", ProcessType = ProcessType.Management, Status = ProcessStatus.Active, DisplayOrder = 2, OwningUnitId = orgUnits[4].Id, SystemId = systems[2].Id, EstimatedDuration = 5, DurationUnit = TimeUnit.Days, DescriptionEn = "Manage organizational budget and allocations", DescriptionAr = "إدارة ميزانية المنظمة والتخصيصات" },
            // Strategic Planning
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[8].Id, NameEn = "Strategy Development", NameAr = "تطوير الاستراتيجية", Code = "4.1.1", ProcessType = ProcessType.Management, Status = ProcessStatus.Active, DisplayOrder = 1, OwningUnitId = orgUnits[0].Id, EstimatedDuration = 30, DurationUnit = TimeUnit.Days, DescriptionEn = "Develop organizational strategic plans", DescriptionAr = "تطوير الخطط الاستراتيجية للمنظمة" },
            // Performance Management
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[9].Id, NameEn = "KPI Monitoring", NameAr = "مراقبة مؤشرات الأداء", Code = "4.2.1", ProcessType = ProcessType.Management, Status = ProcessStatus.Active, DisplayOrder = 1, OwningUnitId = orgUnits[7].Id, SystemId = systems[6].Id, EstimatedDuration = 1, DurationUnit = TimeUnit.Days, DescriptionEn = "Monitor and report on organizational KPIs", DescriptionAr = "مراقبة والإبلاغ عن مؤشرات الأداء المؤسسي" },
            new() { Id = Guid.NewGuid().ToString(), ProcessGroupId = groups[9].Id, NameEn = "Process Improvement", NameAr = "تحسين العمليات", Code = "4.2.2", ProcessType = ProcessType.Management, Status = ProcessStatus.Active, DisplayOrder = 2, OwningUnitId = orgUnits[7].Id, EstimatedDuration = 15, DurationUnit = TimeUnit.Days, DescriptionEn = "Identify and implement process improvements", DescriptionAr = "تحديد وتنفيذ تحسينات العمليات" }
        };
    }

    /// <summary>
    /// Creates 12 real MBRHE Housing Services from Draft8
    /// Key Results: 98.75% happiness, AED 384 avg MBRHE savings, AED 17k avg customer savings,
    /// 1.8 hrs avg MBRHE time saving, 2.3 hrs avg customer time saving, 5 months avg waiting time saving
    /// </summary>
    private static List<Service> CreateServices(List<OrganizationUnit> orgUnits, List<StrategicObjective> objectives)
    {
        return new List<Service>
        {
            // Loans (5 services)
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Loan for the Purchase of Ready-made Housing Owned by MBRHE", NameAr = "قرض شراء مسكن جاهز مملوك لمؤسسة محمد بن راشد للإسكان", Code = "SVC-001", ServiceType = ServiceType.External, Channel = ChannelType.Hybrid, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[0].Id, SLADays = 30, TargetDeliveryDays = 15, ActualDeliveryDays = 12, CustomerSatisfactionScore = 97.5m, AnnualTransactionCount = 850, DisplayOrder = 1, IsActive = true, Tags = "Loan,Purchase,Housing", DescriptionEn = "Housing loan for purchasing ready-made units owned by MBRHE", DescriptionAr = "قرض إسكاني لشراء وحدات جاهزة مملوكة للمؤسسة" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Construction Loan", NameAr = "قرض البناء", Code = "SVC-002", ServiceType = ServiceType.External, Channel = ChannelType.Hybrid, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[0].Id, SLADays = 45, TargetDeliveryDays = 20, ActualDeliveryDays = 18, CustomerSatisfactionScore = 96.8m, AnnualTransactionCount = 1200, DisplayOrder = 2, IsActive = true, Tags = "Loan,Construction", DescriptionEn = "Loan for constructing a new residential building on owned land", DescriptionAr = "قرض لبناء مبنى سكني جديد على أرض مملوكة" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Loan for the Purchase of Ready-made Housing Not Owned by MBRHE", NameAr = "قرض شراء مسكن جاهز غير مملوك للمؤسسة", Code = "SVC-003", ServiceType = ServiceType.External, Channel = ChannelType.Hybrid, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[0].Id, SLADays = 30, TargetDeliveryDays = 15, ActualDeliveryDays = 14, CustomerSatisfactionScore = 97.2m, AnnualTransactionCount = 620, DisplayOrder = 3, IsActive = true, Tags = "Loan,Purchase,External", DescriptionEn = "Housing loan for purchasing ready-made units not owned by MBRHE", DescriptionAr = "قرض إسكاني لشراء وحدات جاهزة غير مملوكة للمؤسسة" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Loan for Replacement Works", NameAr = "قرض أعمال الإحلال", Code = "SVC-004", ServiceType = ServiceType.External, Channel = ChannelType.Hybrid, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[0].Id, SLADays = 30, TargetDeliveryDays = 15, ActualDeliveryDays = 13, CustomerSatisfactionScore = 98.1m, AnnualTransactionCount = 340, DisplayOrder = 4, IsActive = true, Tags = "Loan,Replacement", DescriptionEn = "Loan for replacing existing housing structures", DescriptionAr = "قرض لإحلال المباني السكنية القائمة" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Loan for Maintenance or Addition Works", NameAr = "قرض أعمال الصيانة أو الإضافة", Code = "SVC-005", ServiceType = ServiceType.External, Channel = ChannelType.Hybrid, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[0].Id, SLADays = 20, TargetDeliveryDays = 10, ActualDeliveryDays = 8, CustomerSatisfactionScore = 98.5m, AnnualTransactionCount = 580, DisplayOrder = 5, IsActive = true, Tags = "Loan,Maintenance,Addition", DescriptionEn = "Loan for maintenance or extension works on existing housing", DescriptionAr = "قرض لأعمال الصيانة أو الإضافة على المسكن القائم" },
            // Grants (7 services)
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Residential Land Grant", NameAr = "منحة أرض سكنية", Code = "SVC-006", ServiceType = ServiceType.External, Channel = ChannelType.Hybrid, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[2].Id, SLADays = 60, TargetDeliveryDays = 30, ActualDeliveryDays = 25, CustomerSatisfactionScore = 98.0m, AnnualTransactionCount = 450, DisplayOrder = 6, IsActive = true, Tags = "Grant,Land", DescriptionEn = "Grant of residential land for eligible citizens", DescriptionAr = "منحة أرض سكنية للمواطنين المستحقين" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Grant for Replacement Works", NameAr = "منحة أعمال الإحلال", Code = "SVC-007", ServiceType = ServiceType.External, Channel = ChannelType.Hybrid, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[2].Id, SLADays = 30, TargetDeliveryDays = 15, ActualDeliveryDays = 12, CustomerSatisfactionScore = 98.3m, AnnualTransactionCount = 280, DisplayOrder = 7, IsActive = true, Tags = "Grant,Replacement", DescriptionEn = "Grant for replacing housing structures", DescriptionAr = "منحة لإحلال المباني السكنية" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Purchase of Ready-Made House Owned by MBRHE", NameAr = "شراء مسكن جاهز مملوك للمؤسسة", Code = "SVC-008", ServiceType = ServiceType.External, Channel = ChannelType.Hybrid, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[2].Id, SLADays = 30, TargetDeliveryDays = 15, ActualDeliveryDays = 10, CustomerSatisfactionScore = 99.0m, AnnualTransactionCount = 380, DisplayOrder = 8, IsActive = true, Tags = "Purchase,Housing", DescriptionEn = "Direct purchase of ready-made housing unit from MBRHE", DescriptionAr = "شراء مباشر لوحدة سكنية جاهزة من المؤسسة" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Residential Apartment Grant", NameAr = "منحة شقة سكنية", Code = "SVC-009", ServiceType = ServiceType.External, Channel = ChannelType.Hybrid, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[2].Id, SLADays = 45, TargetDeliveryDays = 20, ActualDeliveryDays = 16, CustomerSatisfactionScore = 98.7m, AnnualTransactionCount = 520, DisplayOrder = 9, IsActive = true, Tags = "Grant,Apartment", DescriptionEn = "Grant of residential apartment for eligible citizens", DescriptionAr = "منحة شقة سكنية للمواطنين المستحقين" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Construction Grant", NameAr = "منحة البناء", Code = "SVC-010", ServiceType = ServiceType.External, Channel = ChannelType.Hybrid, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[2].Id, SLADays = 45, TargetDeliveryDays = 20, ActualDeliveryDays = 17, CustomerSatisfactionScore = 97.9m, AnnualTransactionCount = 680, DisplayOrder = 10, IsActive = true, Tags = "Grant,Construction", DescriptionEn = "Grant for constructing a new residential building", DescriptionAr = "منحة لبناء مبنى سكني جديد" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Grant for Maintenance or Addition Works", NameAr = "منحة أعمال الصيانة أو الإضافة", Code = "SVC-011", ServiceType = ServiceType.External, Channel = ChannelType.Hybrid, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[0].Id, SLADays = 20, TargetDeliveryDays = 10, ActualDeliveryDays = 8, CustomerSatisfactionScore = 98.9m, AnnualTransactionCount = 410, DisplayOrder = 11, IsActive = true, Tags = "Grant,Maintenance,Addition", DescriptionEn = "Grant for maintenance or addition works on existing housing", DescriptionAr = "منحة لأعمال الصيانة أو الإضافة على المسكن القائم" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "House Grant", NameAr = "منحة مسكن", Code = "SVC-012", ServiceType = ServiceType.External, Channel = ChannelType.Hybrid, OwningUnitId = orgUnits[1].Id, StrategicObjectiveId = objectives[2].Id, SLADays = 60, TargetDeliveryDays = 30, ActualDeliveryDays = 22, CustomerSatisfactionScore = 99.2m, AnnualTransactionCount = 310, DisplayOrder = 12, IsActive = true, Tags = "Grant,House", DescriptionEn = "Full house grant for eligible citizens", DescriptionAr = "منحة مسكن كامل للمواطنين المستحقين" }
        };
    }

    private static List<ImprovementInitiative> CreateImprovements(List<Process> processes, List<Service> services, List<OrganizationUnit> orgUnits)
    {
        // Real MBRHE initiatives from Draft8 Prioritization Framework with 9-criteria scores
        return new List<ImprovementInitiative>
        {
            // ═══ HORIZON 1: Current Business Model (2023-2025) - Enhancement ═══
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Outsourcing Loans", TitleAr = "الاستعانة بمصادر خارجية للقروض", NameEn = "Outsourcing Loans", NameAr = "الاستعانة بمصادر خارجية للقروض", DescriptionEn = "Outsource housing loan processing to accelerate service delivery and reduce operational costs", DescriptionAr = "الاستعانة بمصادر خارجية لمعالجة القروض السكنية لتسريع تقديم الخدمات وخفض التكاليف التشغيلية", ImpactScore = 8, EffortScore = 4, Quadrant = ImprovementQuadrant.QuickWins, Status = ImprovementStatus.InProgress, Priority = 1, OwningUnitId = orgUnits[9].Id, EstimatedTimeSavings = 120, EstimatedCostSavings = 200000, ProgressPercentage = 65, InnovationType = InnovationType.Incremental, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 7, BudgetEstimation = 8, ProjectDependency = 6, StrategicAlignmentScore = 9, LeadershipDirections = 8, QualityOfLife = 7, InnovationAndFutureShaping = 5, FinancialAndEconomicImpact = 9, SustainabilityScore = 6 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Unified Housing Designs", TitleAr = "التصاميم السكنية الموحدة", NameEn = "Unified Housing Designs", NameAr = "التصاميم السكنية الموحدة", DescriptionEn = "Standardize housing designs to reduce construction time and ensure quality consistency", DescriptionAr = "توحيد التصاميم السكنية لتقليل وقت البناء وضمان اتساق الجودة", ImpactScore = 8, EffortScore = 3, Quadrant = ImprovementQuadrant.QuickWins, Status = ImprovementStatus.Completed, Priority = 2, OwningUnitId = orgUnits[21].Id, EstimatedTimeSavings = 150, EstimatedCostSavings = 350000, ProgressPercentage = 100, InnovationType = InnovationType.Incremental, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 8, BudgetEstimation = 7, ProjectDependency = 5, StrategicAlignmentScore = 8, LeadershipDirections = 9, QualityOfLife = 8, InnovationAndFutureShaping = 6, FinancialAndEconomicImpact = 8, SustainabilityScore = 7 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Smart Housing Matrix", TitleAr = "مصفوفة الإسكان الذكية", NameEn = "Smart Housing Matrix", NameAr = "مصفوفة الإسكان الذكية", DescriptionEn = "AI-driven smart matrix for housing eligibility assessment and allocation optimization", DescriptionAr = "مصفوفة ذكية مدعومة بالذكاء الاصطناعي لتقييم أهلية الإسكان وتحسين التخصيص", ImpactScore = 9, EffortScore = 7, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.InProgress, Priority = 3, OwningUnitId = orgUnits[15].Id, EstimatedTimeSavings = 200, EstimatedCostSavings = 500000, ProgressPercentage = 45, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 5, BudgetEstimation = 4, ProjectDependency = 7, StrategicAlignmentScore = 9, LeadershipDirections = 9, QualityOfLife = 9, InnovationAndFutureShaping = 8, FinancialAndEconomicImpact = 7, SustainabilityScore = 6 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Digital Housing Services (Emarati)", TitleAr = "الخدمات الإسكانية الرقمية (إماراتي)", NameEn = "Digital Housing Services (Emarati)", NameAr = "الخدمات الإسكانية الرقمية (إماراتي)", DescriptionEn = "End-to-end digital housing services platform achieving 94.8% digital adoption rate", DescriptionAr = "منصة خدمات إسكانية رقمية متكاملة تحقق معدل تبني رقمي 94.8%", ImpactScore = 9, EffortScore = 4, Quadrant = ImprovementQuadrant.QuickWins, Status = ImprovementStatus.InProgress, Priority = 4, OwningUnitId = orgUnits[15].Id, EstimatedTimeSavings = 250, EstimatedCostSavings = 800000, ProgressPercentage = 70, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 7, BudgetEstimation = 6, ProjectDependency = 8, StrategicAlignmentScore = 10, LeadershipDirections = 10, QualityOfLife = 9, InnovationAndFutureShaping = 8, FinancialAndEconomicImpact = 9, SustainabilityScore = 7 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Metaverse Housing Designs", TitleAr = "تصاميم إسكانية في الميتافيرس", NameEn = "Metaverse Housing Designs", NameAr = "تصاميم إسكانية في الميتافيرس", DescriptionEn = "Virtual reality-based housing design visualization in metaverse environments", DescriptionAr = "تصور تصاميم إسكانية قائمة على الواقع الافتراضي في بيئات الميتافيرس", ImpactScore = 7, EffortScore = 8, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Proposed, Priority = 5, OwningUnitId = orgUnits[16].Id, EstimatedTimeSavings = 60, EstimatedCostSavings = 150000, ProgressPercentage = 10, InnovationType = InnovationType.Disruptive, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 3, BudgetEstimation = 3, ProjectDependency = 4, StrategicAlignmentScore = 7, LeadershipDirections = 8, QualityOfLife = 7, InnovationAndFutureShaping = 10, FinancialAndEconomicImpact = 5, SustainabilityScore = 4 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Cloud Transformation", TitleAr = "التحول السحابي", NameEn = "Cloud Transformation", NameAr = "التحول السحابي", DescriptionEn = "Migration of core systems to cloud infrastructure for scalability and resilience", DescriptionAr = "ترحيل الأنظمة الأساسية إلى البنية التحتية السحابية لتحقيق قابلية التوسع والمرونة", ImpactScore = 8, EffortScore = 7, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.InProgress, Priority = 6, OwningUnitId = orgUnits[15].Id, EstimatedTimeSavings = 100, EstimatedCostSavings = 400000, ProgressPercentage = 55, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 5, BudgetEstimation = 4, ProjectDependency = 9, StrategicAlignmentScore = 8, LeadershipDirections = 9, QualityOfLife = 5, InnovationAndFutureShaping = 7, FinancialAndEconomicImpact = 8, SustainabilityScore = 8 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Paperless Initiatives", TitleAr = "مبادرات التحول اللاورقي", NameEn = "Paperless Initiatives", NameAr = "مبادرات التحول اللاورقي", DescriptionEn = "Complete elimination of paper-based processes achieving 100% digital transactions", DescriptionAr = "التخلص الكامل من العمليات الورقية لتحقيق 100% معاملات رقمية", ImpactScore = 8, EffortScore = 3, Quadrant = ImprovementQuadrant.QuickWins, Status = ImprovementStatus.Completed, Priority = 7, OwningUnitId = orgUnits[15].Id, EstimatedTimeSavings = 80, EstimatedCostSavings = 250000, ProgressPercentage = 100, InnovationType = InnovationType.Incremental, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 8, BudgetEstimation = 9, ProjectDependency = 3, StrategicAlignmentScore = 8, LeadershipDirections = 9, QualityOfLife = 6, InnovationAndFutureShaping = 5, FinancialAndEconomicImpact = 8, SustainabilityScore = 10 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "AI Virtual Assistant", TitleAr = "المساعد الافتراضي بالذكاء الاصطناعي", NameEn = "AI Virtual Assistant", NameAr = "المساعد الافتراضي بالذكاء الاصطناعي", DescriptionEn = "AI-powered virtual assistant for 24/7 customer support and service guidance", DescriptionAr = "مساعد افتراضي مدعوم بالذكاء الاصطناعي لدعم العملاء على مدار الساعة وإرشاد الخدمات", ImpactScore = 8, EffortScore = 5, Quadrant = ImprovementQuadrant.QuickWins, Status = ImprovementStatus.InProgress, Priority = 8, OwningUnitId = orgUnits[16].Id, EstimatedTimeSavings = 150, EstimatedCostSavings = 300000, ProgressPercentage = 50, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 6, BudgetEstimation = 6, ProjectDependency = 5, StrategicAlignmentScore = 8, LeadershipDirections = 8, QualityOfLife = 9, InnovationAndFutureShaping = 9, FinancialAndEconomicImpact = 7, SustainabilityScore = 5 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "GEO Makani for Housing Data", TitleAr = "جيو مكاني لبيانات الإسكان", NameEn = "GEO Makani for Housing Data", NameAr = "جيو مكاني لبيانات الإسكان", DescriptionEn = "GIS-based spatial analytics platform for housing data management and urban planning", DescriptionAr = "منصة تحليلات مكانية قائمة على نظم المعلومات الجغرافية لإدارة بيانات الإسكان والتخطيط الحضري", ImpactScore = 7, EffortScore = 6, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Approved, Priority = 9, OwningUnitId = orgUnits[16].Id, EstimatedTimeSavings = 60, EstimatedCostSavings = 180000, ProgressPercentage = 30, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 5, BudgetEstimation = 5, ProjectDependency = 6, StrategicAlignmentScore = 7, LeadershipDirections = 7, QualityOfLife = 6, InnovationAndFutureShaping = 7, FinancialAndEconomicImpact = 6, SustainabilityScore = 7 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Housing Sustainability Systems", TitleAr = "أنظمة استدامة الإسكان", NameEn = "Housing Sustainability Systems", NameAr = "أنظمة استدامة الإسكان", DescriptionEn = "Smart sustainability monitoring for housing projects including energy and water management", DescriptionAr = "مراقبة ذكية للاستدامة في المشاريع السكنية بما في ذلك إدارة الطاقة والمياه", ImpactScore = 8, EffortScore = 7, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.InProgress, Priority = 10, OwningUnitId = orgUnits[21].Id, EstimatedTimeSavings = 40, EstimatedCostSavings = 600000, ProgressPercentage = 40, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 4, BudgetEstimation = 4, ProjectDependency = 5, StrategicAlignmentScore = 9, LeadershipDirections = 8, QualityOfLife = 8, InnovationAndFutureShaping = 7, FinancialAndEconomicImpact = 7, SustainabilityScore = 10 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "House of the Future", TitleAr = "بيت المستقبل", NameEn = "House of the Future", NameAr = "بيت المستقبل", DescriptionEn = "Futuristic housing prototype with smart home IoT, sustainable materials, and adaptive design", DescriptionAr = "نموذج سكني مستقبلي مع إنترنت الأشياء الذكية والمواد المستدامة والتصميم التكيفي", ImpactScore = 9, EffortScore = 9, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Proposed, Priority = 11, OwningUnitId = orgUnits[22].Id, EstimatedTimeSavings = 30, EstimatedCostSavings = 100000, ProgressPercentage = 15, InnovationType = InnovationType.Disruptive, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 2, BudgetEstimation = 2, ProjectDependency = 3, StrategicAlignmentScore = 9, LeadershipDirections = 10, QualityOfLife = 10, InnovationAndFutureShaping = 10, FinancialAndEconomicImpact = 5, SustainabilityScore = 10 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Innovation Lab", TitleAr = "مختبر الابتكار", NameEn = "Innovation Lab", NameAr = "مختبر الابتكار", DescriptionEn = "Dedicated innovation lab for prototyping and testing new housing solutions", DescriptionAr = "مختبر ابتكار مخصص لنمذجة واختبار الحلول السكنية الجديدة", ImpactScore = 7, EffortScore = 6, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Approved, Priority = 12, OwningUnitId = orgUnits[6].Id, EstimatedTimeSavings = 20, EstimatedCostSavings = 80000, ProgressPercentage = 20, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 5, BudgetEstimation = 4, ProjectDependency = 4, StrategicAlignmentScore = 8, LeadershipDirections = 9, QualityOfLife = 5, InnovationAndFutureShaping = 10, FinancialAndEconomicImpact = 5, SustainabilityScore = 6 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Proactive Services", TitleAr = "الخدمات الاستباقية", NameEn = "Proactive Services", NameAr = "الخدمات الاستباقية", DescriptionEn = "Proactive service delivery by anticipating customer needs using data analytics", DescriptionAr = "تقديم خدمات استباقية من خلال توقع احتياجات العملاء باستخدام تحليلات البيانات", ImpactScore = 9, EffortScore = 5, Quadrant = ImprovementQuadrant.QuickWins, Status = ImprovementStatus.InProgress, Priority = 13, OwningUnitId = orgUnits[26].Id, EstimatedTimeSavings = 100, EstimatedCostSavings = 450000, ProgressPercentage = 60, InnovationType = InnovationType.Incremental, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 6, BudgetEstimation = 7, ProjectDependency = 6, StrategicAlignmentScore = 10, LeadershipDirections = 9, QualityOfLife = 10, InnovationAndFutureShaping = 7, FinancialAndEconomicImpact = 8, SustainabilityScore = 5 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Project Monitoring with Drones", TitleAr = "مراقبة المشاريع بالطائرات المسيرة", NameEn = "Project Monitoring with Drones", NameAr = "مراقبة المشاريع بالطائرات المسيرة", DescriptionEn = "AI-integrated drone monitoring for construction quality and progress tracking", DescriptionAr = "مراقبة بطائرات مسيرة مدعومة بالذكاء الاصطناعي لجودة البناء وتتبع التقدم", ImpactScore = 7, EffortScore = 6, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Approved, Priority = 14, OwningUnitId = orgUnits[23].Id, EstimatedTimeSavings = 50, EstimatedCostSavings = 200000, ProgressPercentage = 25, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 4, BudgetEstimation = 4, ProjectDependency = 5, StrategicAlignmentScore = 7, LeadershipDirections = 8, QualityOfLife = 6, InnovationAndFutureShaping = 9, FinancialAndEconomicImpact = 7, SustainabilityScore = 6 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Smart Designer", TitleAr = "المصمم الذكي", NameEn = "Smart Designer", NameAr = "المصمم الذكي", DescriptionEn = "AI-powered design tool for automated housing layout generation and optimization", DescriptionAr = "أداة تصميم مدعومة بالذكاء الاصطناعي لتوليد مخططات الإسكان وتحسينها تلقائياً", ImpactScore = 8, EffortScore = 7, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.InProgress, Priority = 15, OwningUnitId = orgUnits[22].Id, EstimatedTimeSavings = 80, EstimatedCostSavings = 250000, ProgressPercentage = 35, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 4, BudgetEstimation = 5, ProjectDependency = 6, StrategicAlignmentScore = 8, LeadershipDirections = 8, QualityOfLife = 7, InnovationAndFutureShaping = 9, FinancialAndEconomicImpact = 7, SustainabilityScore = 5 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Service Advisor", TitleAr = "مستشار الخدمات", NameEn = "Service Advisor", NameAr = "مستشار الخدمات", DescriptionEn = "AI service advisor guiding customers to the most suitable housing services", DescriptionAr = "مستشار خدمات بالذكاء الاصطناعي يرشد العملاء إلى أنسب الخدمات السكنية", ImpactScore = 7, EffortScore = 3, Quadrant = ImprovementQuadrant.QuickWins, Status = ImprovementStatus.Completed, Priority = 16, OwningUnitId = orgUnits[27].Id, EstimatedTimeSavings = 40, EstimatedCostSavings = 120000, ProgressPercentage = 100, InnovationType = InnovationType.Incremental, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 8, BudgetEstimation = 8, ProjectDependency = 3, StrategicAlignmentScore = 7, LeadershipDirections = 7, QualityOfLife = 8, InnovationAndFutureShaping = 6, FinancialAndEconomicImpact = 6, SustainabilityScore = 4 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Iskankom App", TitleAr = "تطبيق إسكانكم", NameEn = "Iskankom App", NameAr = "تطبيق إسكانكم", DescriptionEn = "Mobile app for comprehensive housing services access and application tracking", DescriptionAr = "تطبيق جوال للوصول الشامل للخدمات السكنية وتتبع الطلبات", ImpactScore = 9, EffortScore = 4, Quadrant = ImprovementQuadrant.QuickWins, Status = ImprovementStatus.Completed, Priority = 17, OwningUnitId = orgUnits[16].Id, EstimatedTimeSavings = 90, EstimatedCostSavings = 350000, ProgressPercentage = 100, InnovationType = InnovationType.Incremental, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 8, BudgetEstimation = 7, ProjectDependency = 5, StrategicAlignmentScore = 9, LeadershipDirections = 9, QualityOfLife = 9, InnovationAndFutureShaping = 7, FinancialAndEconomicImpact = 8, SustainabilityScore = 5 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Ask Omair Chatbot", TitleAr = "اسأل عمير - روبوت محادثة", NameEn = "Ask Omair Chatbot", NameAr = "اسأل عمير - روبوت محادثة", DescriptionEn = "AI chatbot 'Ask Omair' providing instant answers to housing-related queries", DescriptionAr = "روبوت محادثة ذكي 'اسأل عمير' يقدم إجابات فورية للاستفسارات السكنية", ImpactScore = 8, EffortScore = 4, Quadrant = ImprovementQuadrant.QuickWins, Status = ImprovementStatus.InProgress, Priority = 18, OwningUnitId = orgUnits[16].Id, EstimatedTimeSavings = 100, EstimatedCostSavings = 280000, ProgressPercentage = 80, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 7, BudgetEstimation = 7, ProjectDependency = 4, StrategicAlignmentScore = 8, LeadershipDirections = 8, QualityOfLife = 9, InnovationAndFutureShaping = 8, FinancialAndEconomicImpact = 7, SustainabilityScore = 4 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "WhatsApp Service", TitleAr = "خدمة واتساب", NameEn = "WhatsApp Service", NameAr = "خدمة واتساب", DescriptionEn = "WhatsApp-based service channel for convenient customer interaction and notifications", DescriptionAr = "قناة خدمة عبر واتساب للتفاعل المريح مع العملاء والإشعارات", ImpactScore = 8, EffortScore = 3, Quadrant = ImprovementQuadrant.QuickWins, Status = ImprovementStatus.Completed, Priority = 19, OwningUnitId = orgUnits[28].Id, EstimatedTimeSavings = 60, EstimatedCostSavings = 150000, ProgressPercentage = 100, InnovationType = InnovationType.Incremental, Horizon = ImprovementHorizon.Horizon1_Current, EaseOfImplementation = 9, BudgetEstimation = 9, ProjectDependency = 3, StrategicAlignmentScore = 7, LeadershipDirections = 8, QualityOfLife = 8, InnovationAndFutureShaping = 5, FinancialAndEconomicImpact = 7, SustainabilityScore = 5 },
            // ═══ HORIZON 2: Expand Business Model (2026-2028) - Experimentation ═══
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Collect Rainwater and AC Runoff", TitleAr = "جمع مياه الأمطار ومكثفات التكييف", NameEn = "Collect Rainwater and AC Runoff", NameAr = "جمع مياه الأمطار ومكثفات التكييف", DescriptionEn = "Water harvesting systems for housing projects to collect rainwater and AC condensate", DescriptionAr = "أنظمة حصاد المياه للمشاريع السكنية لجمع مياه الأمطار ومكثفات المكيفات", ImpactScore = 6, EffortScore = 5, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Proposed, Priority = 20, OwningUnitId = orgUnits[24].Id, EstimatedTimeSavings = 10, EstimatedCostSavings = 80000, ProgressPercentage = 5, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 5, BudgetEstimation = 5, ProjectDependency = 3, StrategicAlignmentScore = 7, LeadershipDirections = 6, QualityOfLife = 7, InnovationAndFutureShaping = 6, FinancialAndEconomicImpact = 5, SustainabilityScore = 10 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Baitee App", TitleAr = "تطبيق بيتي", NameEn = "Baitee App", NameAr = "تطبيق بيتي", DescriptionEn = "Smart home management app for residents to control and monitor their housing units", DescriptionAr = "تطبيق إدارة المنزل الذكي للسكان للتحكم ومراقبة وحداتهم السكنية", ImpactScore = 7, EffortScore = 4, Quadrant = ImprovementQuadrant.QuickWins, Status = ImprovementStatus.InProgress, Priority = 21, OwningUnitId = orgUnits[16].Id, EstimatedTimeSavings = 30, EstimatedCostSavings = 120000, ProgressPercentage = 40, InnovationType = InnovationType.Incremental, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 7, BudgetEstimation = 7, ProjectDependency = 4, StrategicAlignmentScore = 7, LeadershipDirections = 7, QualityOfLife = 8, InnovationAndFutureShaping = 6, FinancialAndEconomicImpact = 5, SustainabilityScore = 7 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Solar-Covered Parking", TitleAr = "مواقف مغطاة بالطاقة الشمسية", NameEn = "Solar-Covered Parking", NameAr = "مواقف مغطاة بالطاقة الشمسية", DescriptionEn = "Solar panel-covered parking structures generating clean energy for housing communities", DescriptionAr = "هياكل مواقف مغطاة بألواح شمسية لتوليد طاقة نظيفة للمجتمعات السكنية", ImpactScore = 7, EffortScore = 7, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Proposed, Priority = 22, OwningUnitId = orgUnits[24].Id, EstimatedTimeSavings = 5, EstimatedCostSavings = 200000, ProgressPercentage = 0, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 4, BudgetEstimation = 3, ProjectDependency = 3, StrategicAlignmentScore = 8, LeadershipDirections = 7, QualityOfLife = 7, InnovationAndFutureShaping = 7, FinancialAndEconomicImpact = 6, SustainabilityScore = 10 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Preventive Maintenance with Drones", TitleAr = "الصيانة الوقائية بالطائرات المسيرة", NameEn = "Preventive Maintenance with Drones", NameAr = "الصيانة الوقائية بالطائرات المسيرة", DescriptionEn = "Drone-based preventive maintenance inspections for housing infrastructure", DescriptionAr = "عمليات فحص الصيانة الوقائية بالطائرات المسيرة للبنية التحتية السكنية", ImpactScore = 7, EffortScore = 6, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Proposed, Priority = 23, OwningUnitId = orgUnits[24].Id, EstimatedTimeSavings = 40, EstimatedCostSavings = 180000, ProgressPercentage = 10, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 4, BudgetEstimation = 4, ProjectDependency = 5, StrategicAlignmentScore = 6, LeadershipDirections = 7, QualityOfLife = 6, InnovationAndFutureShaping = 8, FinancialAndEconomicImpact = 7, SustainabilityScore = 7 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Investment Portal", TitleAr = "بوابة الاستثمار", NameEn = "Investment Portal", NameAr = "بوابة الاستثمار", DescriptionEn = "Digital portal for housing investment opportunities and portfolio management", DescriptionAr = "بوابة رقمية لفرص الاستثمار السكني وإدارة المحافظ", ImpactScore = 7, EffortScore = 5, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Approved, Priority = 24, OwningUnitId = orgUnits[30].Id, EstimatedTimeSavings = 20, EstimatedCostSavings = 300000, ProgressPercentage = 15, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 5, BudgetEstimation = 5, ProjectDependency = 4, StrategicAlignmentScore = 8, LeadershipDirections = 7, QualityOfLife = 5, InnovationAndFutureShaping = 7, FinancialAndEconomicImpact = 9, SustainabilityScore = 5 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Crowd Funding Portal", TitleAr = "بوابة التمويل الجماعي", NameEn = "Crowd Funding Portal", NameAr = "بوابة التمويل الجماعي", DescriptionEn = "Crowdfunding platform for community-supported housing development projects", DescriptionAr = "منصة تمويل جماعي لمشاريع التطوير السكني المدعومة من المجتمع", ImpactScore = 6, EffortScore = 6, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Proposed, Priority = 25, OwningUnitId = orgUnits[31].Id, EstimatedTimeSavings = 10, EstimatedCostSavings = 150000, ProgressPercentage = 5, InnovationType = InnovationType.Disruptive, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 4, BudgetEstimation = 5, ProjectDependency = 4, StrategicAlignmentScore = 7, LeadershipDirections = 6, QualityOfLife = 5, InnovationAndFutureShaping = 8, FinancialAndEconomicImpact = 8, SustainabilityScore = 5 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Meta-Majlis", TitleAr = "ميتا مجلس", NameEn = "Meta-Majlis", NameAr = "ميتا مجلس", DescriptionEn = "Metaverse-based virtual majlis for community engagement and stakeholder consultations", DescriptionAr = "مجلس افتراضي في الميتافيرس للمشاركة المجتمعية واستشارات أصحاب المصلحة", ImpactScore = 5, EffortScore = 7, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Proposed, Priority = 26, OwningUnitId = orgUnits[18].Id, EstimatedTimeSavings = 15, EstimatedCostSavings = 50000, ProgressPercentage = 0, InnovationType = InnovationType.Disruptive, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 3, BudgetEstimation = 3, ProjectDependency = 3, StrategicAlignmentScore = 6, LeadershipDirections = 6, QualityOfLife = 6, InnovationAndFutureShaping = 9, FinancialAndEconomicImpact = 3, SustainabilityScore = 4 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Housing Blockchain", TitleAr = "بلوك تشين الإسكان", NameEn = "Housing Blockchain", NameAr = "بلوك تشين الإسكان", DescriptionEn = "Blockchain-based housing transaction verification and smart contract implementation", DescriptionAr = "التحقق من المعاملات السكنية وتنفيذ العقود الذكية القائمة على البلوك تشين", ImpactScore = 6, EffortScore = 8, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Proposed, Priority = 27, OwningUnitId = orgUnits[15].Id, EstimatedTimeSavings = 30, EstimatedCostSavings = 200000, ProgressPercentage = 5, InnovationType = InnovationType.Disruptive, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 3, BudgetEstimation = 3, ProjectDependency = 6, StrategicAlignmentScore = 7, LeadershipDirections = 7, QualityOfLife = 4, InnovationAndFutureShaping = 9, FinancialAndEconomicImpact = 7, SustainabilityScore = 4 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Proactive Services (Expanded)", TitleAr = "الخدمات الاستباقية (موسعة)", NameEn = "Proactive Services (Expanded)", NameAr = "الخدمات الاستباقية (موسعة)", DescriptionEn = "Expansion of proactive services with predictive analytics and automated workflows", DescriptionAr = "توسيع الخدمات الاستباقية مع التحليلات التنبؤية وسير العمل الآلي", ImpactScore = 8, EffortScore = 6, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Approved, Priority = 28, OwningUnitId = orgUnits[26].Id, EstimatedTimeSavings = 80, EstimatedCostSavings = 350000, ProgressPercentage = 10, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 5, BudgetEstimation = 5, ProjectDependency = 7, StrategicAlignmentScore = 9, LeadershipDirections = 8, QualityOfLife = 9, InnovationAndFutureShaping = 7, FinancialAndEconomicImpact = 7, SustainabilityScore = 5 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Low-Cost Hybrid Wall Panel System", TitleAr = "نظام ألواح جدران هجينة منخفضة التكلفة", NameEn = "Low-Cost Hybrid Wall Panel System", NameAr = "نظام ألواح جدران هجينة منخفضة التكلفة", DescriptionEn = "Innovative low-cost hybrid wall panel construction system for affordable housing", DescriptionAr = "نظام بناء مبتكر لألواح جدران هجينة منخفضة التكلفة للإسكان الميسور", ImpactScore = 7, EffortScore = 7, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.UnderReview, Priority = 29, OwningUnitId = orgUnits[22].Id, EstimatedTimeSavings = 60, EstimatedCostSavings = 400000, ProgressPercentage = 10, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 4, BudgetEstimation = 5, ProjectDependency = 4, StrategicAlignmentScore = 8, LeadershipDirections = 7, QualityOfLife = 7, InnovationAndFutureShaping = 8, FinancialAndEconomicImpact = 8, SustainabilityScore = 9 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Sustainability and Cultural Preservation", TitleAr = "الاستدامة والحفاظ على التراث الثقافي", NameEn = "Sustainability and Cultural Preservation", NameAr = "الاستدامة والحفاظ على التراث الثقافي", DescriptionEn = "Integrating cultural preservation elements with sustainable housing practices", DescriptionAr = "دمج عناصر الحفاظ على التراث الثقافي مع ممارسات الإسكان المستدامة", ImpactScore = 5, EffortScore = 4, Quadrant = ImprovementQuadrant.FillIns, Status = ImprovementStatus.Proposed, Priority = 30, OwningUnitId = orgUnits[7].Id, EstimatedTimeSavings = 10, EstimatedCostSavings = 30000, ProgressPercentage = 5, InnovationType = InnovationType.Incremental, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 6, BudgetEstimation = 7, ProjectDependency = 3, StrategicAlignmentScore = 6, LeadershipDirections = 5, QualityOfLife = 7, InnovationAndFutureShaping = 5, FinancialAndEconomicImpact = 3, SustainabilityScore = 9 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Streamline Administration within Housing Sector", TitleAr = "تبسيط الإدارة في قطاع الإسكان", NameEn = "Streamline Administration within Housing Sector", NameAr = "تبسيط الإدارة في قطاع الإسكان", DescriptionEn = "Process re-engineering to streamline administrative operations across departments", DescriptionAr = "إعادة هندسة العمليات لتبسيط العمليات الإدارية عبر الإدارات", ImpactScore = 6, EffortScore = 4, Quadrant = ImprovementQuadrant.FillIns, Status = ImprovementStatus.Approved, Priority = 31, OwningUnitId = orgUnits[14].Id, EstimatedTimeSavings = 50, EstimatedCostSavings = 100000, ProgressPercentage = 15, InnovationType = InnovationType.Incremental, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 7, BudgetEstimation = 8, ProjectDependency = 5, StrategicAlignmentScore = 5, LeadershipDirections = 5, QualityOfLife = 4, InnovationAndFutureShaping = 3, FinancialAndEconomicImpact = 6, SustainabilityScore = 4 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "GPT-Based HR Services", TitleAr = "خدمات موارد بشرية مبنية على GPT", NameEn = "GPT-Based HR Services", NameAr = "خدمات موارد بشرية مبنية على GPT", DescriptionEn = "GPT-powered HR service automation for employee queries and HR processes", DescriptionAr = "أتمتة خدمات الموارد البشرية المدعومة بتقنية GPT لاستفسارات الموظفين والعمليات", ImpactScore = 6, EffortScore = 5, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.Proposed, Priority = 32, OwningUnitId = orgUnits[13].Id, EstimatedTimeSavings = 40, EstimatedCostSavings = 80000, ProgressPercentage = 5, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 5, BudgetEstimation = 6, ProjectDependency = 4, StrategicAlignmentScore = 5, LeadershipDirections = 6, QualityOfLife = 5, InnovationAndFutureShaping = 8, FinancialAndEconomicImpact = 5, SustainabilityScore = 3 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Adopting AI and RPA in HRMS", TitleAr = "تبني الذكاء الاصطناعي وأتمتة العمليات في إدارة الموارد البشرية", NameEn = "Adopting AI and RPA in HRMS", NameAr = "تبني الذكاء الاصطناعي وأتمتة العمليات في إدارة الموارد البشرية", DescriptionEn = "Implementing AI and Robotic Process Automation in HR management system", DescriptionAr = "تطبيق الذكاء الاصطناعي وأتمتة العمليات الروبوتية في نظام إدارة الموارد البشرية", ImpactScore = 6, EffortScore = 6, Quadrant = ImprovementQuadrant.MajorProjects, Status = ImprovementStatus.UnderReview, Priority = 33, OwningUnitId = orgUnits[13].Id, EstimatedTimeSavings = 60, EstimatedCostSavings = 120000, ProgressPercentage = 10, InnovationType = InnovationType.Breakthrough, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 4, BudgetEstimation = 5, ProjectDependency = 6, StrategicAlignmentScore = 5, LeadershipDirections = 6, QualityOfLife = 4, InnovationAndFutureShaping = 7, FinancialAndEconomicImpact = 6, SustainabilityScore = 3 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "Qualifying Employees After Retirement", TitleAr = "تأهيل الموظفين بعد التقاعد", NameEn = "Qualifying Employees After Retirement", NameAr = "تأهيل الموظفين بعد التقاعد", DescriptionEn = "Knowledge transfer and re-skilling programs for retiring employees", DescriptionAr = "برامج نقل المعرفة وإعادة تأهيل الموظفين المتقاعدين", ImpactScore = 4, EffortScore = 3, Quadrant = ImprovementQuadrant.FillIns, Status = ImprovementStatus.Proposed, Priority = 34, OwningUnitId = orgUnits[13].Id, EstimatedTimeSavings = 15, EstimatedCostSavings = 25000, ProgressPercentage = 5, InnovationType = InnovationType.Incremental, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 7, BudgetEstimation = 7, ProjectDependency = 2, StrategicAlignmentScore = 4, LeadershipDirections = 5, QualityOfLife = 5, InnovationAndFutureShaping = 3, FinancialAndEconomicImpact = 3, SustainabilityScore = 5 },
            new() { Id = Guid.NewGuid().ToString(), TitleEn = "New Employees Virtual Training Program", TitleAr = "برنامج تدريب افتراضي للموظفين الجدد", NameEn = "New Employees Virtual Training Program", NameAr = "برنامج تدريب افتراضي للموظفين الجدد", DescriptionEn = "VR-based immersive training program for new employee onboarding", DescriptionAr = "برنامج تدريب غامر قائم على الواقع الافتراضي لتأهيل الموظفين الجدد", ImpactScore = 5, EffortScore = 4, Quadrant = ImprovementQuadrant.FillIns, Status = ImprovementStatus.InProgress, Priority = 35, OwningUnitId = orgUnits[13].Id, EstimatedTimeSavings = 25, EstimatedCostSavings = 40000, ProgressPercentage = 20, InnovationType = InnovationType.Incremental, Horizon = ImprovementHorizon.Horizon2_Expand, EaseOfImplementation = 6, BudgetEstimation = 6, ProjectDependency = 3, StrategicAlignmentScore = 5, LeadershipDirections = 5, QualityOfLife = 4, InnovationAndFutureShaping = 6, FinancialAndEconomicImpact = 4, SustainabilityScore = 4 }
        };
    }

    private static List<Activity> CreateActivities(List<Process> processes, List<OrganizationUnit> orgUnits)
    {
        // Get processes with detailed breakdown
        var appSubmission = processes.First(p => p.Code == "1.1.1");
        var eligibility = processes.First(p => p.Code == "1.1.2");
        var docReview = processes.First(p => p.Code == "1.1.3");

        return new List<Activity>
        {
            // Application Submission Activities (Level 4)
            new() { Id = Guid.NewGuid().ToString(), ProcessId = appSubmission.Id, NameEn = "Customer Authentication", NameAr = "توثيق العميل", Code = "1.1.1.1", ChannelType = ChannelType.Digital, DisplayOrder = 1, OwningUnitId = orgUnits[2].Id, EstimatedDuration = 3, DurationUnit = TimeUnit.Minutes, HasDetailedBreakdown = true, DescriptionEn = "Authenticate customer via UAE Pass digital identity", DescriptionAr = "توثيق العميل عبر الهوية الرقمية الإماراتية" },
            new() { Id = Guid.NewGuid().ToString(), ProcessId = appSubmission.Id, NameEn = "Form Completion", NameAr = "إكمال النموذج", Code = "1.1.1.2", ChannelType = ChannelType.Digital, DisplayOrder = 2, OwningUnitId = orgUnits[2].Id, EstimatedDuration = 15, DurationUnit = TimeUnit.Minutes, HasDetailedBreakdown = true, DescriptionEn = "Customer fills in application form with personal and family details", DescriptionAr = "يقوم العميل بملء نموذج الطلب بالبيانات الشخصية والعائلية" },
            new() { Id = Guid.NewGuid().ToString(), ProcessId = appSubmission.Id, NameEn = "Document Upload", NameAr = "رفع المستندات", Code = "1.1.1.3", ChannelType = ChannelType.Digital, DisplayOrder = 3, OwningUnitId = orgUnits[2].Id, EstimatedDuration = 10, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Upload required supporting documents", DescriptionAr = "رفع المستندات الداعمة المطلوبة" },
            new() { Id = Guid.NewGuid().ToString(), ProcessId = appSubmission.Id, NameEn = "Submission Confirmation", NameAr = "تأكيد التقديم", Code = "1.1.1.4", ChannelType = ChannelType.Digital, DisplayOrder = 4, OwningUnitId = orgUnits[2].Id, EstimatedDuration = 2, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Confirm submission and receive reference number", DescriptionAr = "تأكيد التقديم واستلام الرقم المرجعي" },

            // Eligibility Verification Activities (Level 4)
            new() { Id = Guid.NewGuid().ToString(), ProcessId = eligibility.Id, NameEn = "Data Extraction", NameAr = "استخراج البيانات", Code = "1.1.2.1", ChannelType = ChannelType.Digital, DisplayOrder = 1, OwningUnitId = orgUnits[1].Id, EstimatedDuration = 5, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Extract applicant data from government databases", DescriptionAr = "استخراج بيانات مقدم الطلب من قواعد البيانات الحكومية" },
            new() { Id = Guid.NewGuid().ToString(), ProcessId = eligibility.Id, NameEn = "Income Verification", NameAr = "التحقق من الدخل", Code = "1.1.2.2", ChannelType = ChannelType.Digital, DisplayOrder = 2, OwningUnitId = orgUnits[1].Id, EstimatedDuration = 30, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Verify income level against eligibility criteria", DescriptionAr = "التحقق من مستوى الدخل مقابل معايير الأهلية" },
            new() { Id = Guid.NewGuid().ToString(), ProcessId = eligibility.Id, NameEn = "Property Ownership Check", NameAr = "فحص ملكية العقار", Code = "1.1.2.3", ChannelType = ChannelType.Digital, DisplayOrder = 3, OwningUnitId = orgUnits[1].Id, EstimatedDuration = 15, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Check for existing property ownership", DescriptionAr = "فحص ملكية العقارات الحالية" },
            new() { Id = Guid.NewGuid().ToString(), ProcessId = eligibility.Id, NameEn = "Eligibility Decision", NameAr = "قرار الأهلية", Code = "1.1.2.4", ChannelType = ChannelType.Digital, DisplayOrder = 4, OwningUnitId = orgUnits[1].Id, EstimatedDuration = 10, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Calculate eligibility score and make decision", DescriptionAr = "حساب درجة الأهلية واتخاذ القرار" },

            // Document Review Activities (Level 4)
            new() { Id = Guid.NewGuid().ToString(), ProcessId = docReview.Id, NameEn = "Document Completeness Check", NameAr = "فحص اكتمال المستندات", Code = "1.1.3.1", ChannelType = ChannelType.Hybrid, DisplayOrder = 1, OwningUnitId = orgUnits[1].Id, EstimatedDuration = 20, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Verify all required documents are submitted", DescriptionAr = "التحقق من تقديم جميع المستندات المطلوبة" },
            new() { Id = Guid.NewGuid().ToString(), ProcessId = docReview.Id, NameEn = "Document Authenticity Verification", NameAr = "التحقق من صحة المستندات", Code = "1.1.3.2", ChannelType = ChannelType.Hybrid, DisplayOrder = 2, OwningUnitId = orgUnits[1].Id, EstimatedDuration = 45, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Verify authenticity of submitted documents", DescriptionAr = "التحقق من صحة المستندات المقدمة" },
            new() { Id = Guid.NewGuid().ToString(), ProcessId = docReview.Id, NameEn = "Cross-Reference Validation", NameAr = "التحقق المتقاطع", Code = "1.1.3.3", ChannelType = ChannelType.Digital, DisplayOrder = 3, OwningUnitId = orgUnits[1].Id, EstimatedDuration = 30, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Cross-reference documents with external sources", DescriptionAr = "مطابقة المستندات مع المصادر الخارجية" }
        };
    }

    private static List<ProcessTask> CreateTasks(List<Activity> activities, List<OrganizationUnit> orgUnits, List<SystemDefinition> systems)
    {
        // Get activities for task breakdown
        var customerAuth = activities.First(a => a.Code == "1.1.1.1");
        var formCompletion = activities.First(a => a.Code == "1.1.1.2");

        return new List<ProcessTask>
        {
            // Customer Authentication Tasks (Level 5)
            new() { Id = Guid.NewGuid().ToString(), ActivityId = customerAuth.Id, NameEn = "Open Application Portal", NameAr = "فتح بوابة الطلبات", Code = "1.1.1.1.1", DisplayOrder = 1, OwningUnitId = orgUnits[2].Id, SystemId = systems.First(s => s.Code == "CPL").Id, IsAutomated = false, EstimatedDuration = 30, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Customer opens the online application portal", DescriptionAr = "يفتح العميل بوابة الطلبات الإلكترونية" },
            new() { Id = Guid.NewGuid().ToString(), ActivityId = customerAuth.Id, NameEn = "Click UAE Pass Login", NameAr = "النقر على تسجيل الدخول بالهوية", Code = "1.1.1.1.2", DisplayOrder = 2, OwningUnitId = orgUnits[2].Id, SystemId = systems.First(s => s.Code == "UAEPASS").Id, IsAutomated = false, EstimatedDuration = 10, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Customer clicks UAE Pass authentication button", DescriptionAr = "ينقر العميل على زر التوثيق بالهوية الرقمية" },
            new() { Id = Guid.NewGuid().ToString(), ActivityId = customerAuth.Id, NameEn = "Biometric Verification", NameAr = "التحقق البيومتري", Code = "1.1.1.1.3", DisplayOrder = 3, OwningUnitId = orgUnits[2].Id, SystemId = systems.First(s => s.Code == "UAEPASS").Id, IsAutomated = true, EstimatedDuration = 15, DurationUnit = TimeUnit.Minutes, DescriptionEn = "System performs biometric verification", DescriptionAr = "يقوم النظام بالتحقق البيومتري" },
            new() { Id = Guid.NewGuid().ToString(), ActivityId = customerAuth.Id, NameEn = "Load User Profile", NameAr = "تحميل ملف المستخدم", Code = "1.1.1.1.4", DisplayOrder = 4, OwningUnitId = orgUnits[2].Id, SystemId = systems.First(s => s.Code == "HMS").Id, IsAutomated = true, EstimatedDuration = 5, DurationUnit = TimeUnit.Minutes, DescriptionEn = "System loads user profile from UAE Pass", DescriptionAr = "يحمل النظام ملف المستخدم من الهوية الرقمية" },

            // Form Completion Tasks (Level 5)
            new() { Id = Guid.NewGuid().ToString(), ActivityId = formCompletion.Id, NameEn = "Enter Personal Information", NameAr = "إدخال المعلومات الشخصية", Code = "1.1.1.2.1", DisplayOrder = 1, OwningUnitId = orgUnits[2].Id, SystemId = systems.First(s => s.Code == "HMS").Id, IsAutomated = false, EstimatedDuration = 5, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Customer enters personal details (auto-filled from UAE Pass)", DescriptionAr = "يدخل العميل البيانات الشخصية (معبأة تلقائياً من الهوية)" },
            new() { Id = Guid.NewGuid().ToString(), ActivityId = formCompletion.Id, NameEn = "Enter Family Information", NameAr = "إدخال معلومات العائلة", Code = "1.1.1.2.2", DisplayOrder = 2, OwningUnitId = orgUnits[2].Id, SystemId = systems.First(s => s.Code == "HMS").Id, IsAutomated = false, EstimatedDuration = 5, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Customer enters family composition details", DescriptionAr = "يدخل العميل تفاصيل تكوين الأسرة" },
            new() { Id = Guid.NewGuid().ToString(), ActivityId = formCompletion.Id, NameEn = "Enter Employment Details", NameAr = "إدخال تفاصيل العمل", Code = "1.1.1.2.3", DisplayOrder = 3, OwningUnitId = orgUnits[2].Id, SystemId = systems.First(s => s.Code == "HMS").Id, IsAutomated = false, EstimatedDuration = 3, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Customer enters employment and income details", DescriptionAr = "يدخل العميل تفاصيل العمل والدخل" },
            new() { Id = Guid.NewGuid().ToString(), ActivityId = formCompletion.Id, NameEn = "Select Housing Preference", NameAr = "اختيار تفضيلات السكن", Code = "1.1.1.2.4", DisplayOrder = 4, OwningUnitId = orgUnits[2].Id, SystemId = systems.First(s => s.Code == "HMS").Id, IsAutomated = false, EstimatedDuration = 2, DurationUnit = TimeUnit.Minutes, DescriptionEn = "Customer selects preferred housing type and location", DescriptionAr = "يختار العميل نوع السكن والموقع المفضل" }
        };
    }

    private static List<ChangeRequest> CreateChangeRequests(List<Process> processes, List<Service> services, List<OrganizationUnit> orgUnits)
    {
        return new List<ChangeRequest>
        {
            new() { Id = Guid.NewGuid().ToString(), Code = "CR-2024-001", NameEn = "Add SMS Notifications for Application Status", NameAr = "إضافة إشعارات SMS لحالة الطلب", DescriptionEn = "Implement SMS notifications at each stage of application processing", DescriptionAr = "تطبيق إشعارات SMS في كل مرحلة من معالجة الطلب", Justification = "Customer feedback indicates need for proactive communication", Status = ChangeRequestStatus.Approved, OwningUnitId = orgUnits[2].Id, ProcessId = processes.First(p => p.Code == "1.1.1").Id, ImplementationDate = DateTime.Now.AddDays(10), DisplayOrder = 1 },
            new() { Id = Guid.NewGuid().ToString(), Code = "CR-2024-002", NameEn = "Reduce Document Requirements", NameAr = "تقليل متطلبات المستندات", DescriptionEn = "Reduce required documents by integrating with government databases", DescriptionAr = "تقليل المستندات المطلوبة من خلال التكامل مع قواعد البيانات الحكومية", Justification = "Digital transformation initiative to reduce customer burden", Status = ChangeRequestStatus.Implemented, OwningUnitId = orgUnits[1].Id, ProcessId = processes.First(p => p.Code == "1.1.3").Id, ImplementationDate = DateTime.Now.AddDays(-15), DisplayOrder = 2 },
            new() { Id = Guid.NewGuid().ToString(), Code = "CR-2024-003", NameEn = "Add Video Call Support", NameAr = "إضافة دعم مكالمات الفيديو", DescriptionEn = "Enable video call support for customer inquiries", DescriptionAr = "تمكين دعم مكالمات الفيديو لاستفسارات العملاء", Justification = "Improve customer experience for complex inquiries", Status = ChangeRequestStatus.UnderReview, OwningUnitId = orgUnits[2].Id, ServiceId = services.First(s => s.Code == "SVC-005").Id, DisplayOrder = 3 },
            new() { Id = Guid.NewGuid().ToString(), Code = "CR-2024-004", NameEn = "Automate Income Verification", NameAr = "أتمتة التحقق من الدخل", DescriptionEn = "Integrate with Ministry of Finance for automatic income verification", DescriptionAr = "التكامل مع وزارة المالية للتحقق التلقائي من الدخل", Justification = "Reduce processing time and manual errors", Status = ChangeRequestStatus.Submitted, OwningUnitId = orgUnits[1].Id, ProcessId = processes.First(p => p.Code == "1.1.2").Id, DisplayOrder = 4 },
            new() { Id = Guid.NewGuid().ToString(), Code = "CR-2024-005", NameEn = "Mobile App Maintenance Requests", NameAr = "طلبات الصيانة عبر التطبيق", DescriptionEn = "Add maintenance request feature with photo upload to mobile app", DescriptionAr = "إضافة ميزة طلب الصيانة مع رفع الصور في تطبيق الجوال", Justification = "Customer convenience and faster issue documentation", Status = ChangeRequestStatus.Approved, OwningUnitId = orgUnits[3].Id, ServiceId = services.First(s => s.Code == "SVC-003").Id, DisplayOrder = 5 },
            new() { Id = Guid.NewGuid().ToString(), Code = "CR-2024-006", NameEn = "Update Payment Gateway", NameAr = "تحديث بوابة الدفع", DescriptionEn = "Upgrade to new payment gateway with Apple Pay and Google Pay support", DescriptionAr = "الترقية إلى بوابة دفع جديدة مع دعم Apple Pay و Google Pay", Justification = "Expand payment options for customer convenience", Status = ChangeRequestStatus.Rejected, OwningUnitId = orgUnits[4].Id, ServiceId = services.First(s => s.Code == "SVC-004").Id, RejectionReason = "Budget constraints for current fiscal year", DisplayOrder = 6 }
        };
    }


    private static List<AssetCategory> CreateAssetCategories()
    {
        var hardwareId = Guid.NewGuid().ToString();
        var softwareId = Guid.NewGuid().ToString();
        var facilitiesId = Guid.NewGuid().ToString();
        // MBRHE's primary asset class is real estate (housing projects, villas,
        // buildings, plots). Top-level "AST-RE" category gives the housing
        // portfolio a first-class home rather than nesting it under Facilities.
        var realEstateId = Guid.NewGuid().ToString();
        // Information assets (ISO 27001 A.5.9 / PDPL) get their own top-level
        // root so data, documents, datasets, and application data show up in
        // the register alongside physical assets — same scoping, same risk
        // linkage, no duplicated entity.
        var informationId = Guid.NewGuid().ToString();

        return new List<AssetCategory>
        {
            new() { Id = hardwareId, NameEn = "IT Hardware", NameAr = "أجهزة تقنية المعلومات", Code = "AST-HW", DescriptionEn = "Computer hardware and peripherals", DescriptionAr = "أجهزة الكمبيوتر والملحقات", DefaultDepreciationRate = 20, DefaultUsefulLifeYears = 5 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Servers", NameAr = "الخوادم", Code = "AST-HW-SRV", ParentCategoryId = hardwareId, DescriptionEn = "Physical and virtual servers", DescriptionAr = "الخوادم الفعلية والافتراضية", DefaultDepreciationRate = 15, DefaultUsefulLifeYears = 7 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Workstations", NameAr = "محطات العمل", Code = "AST-HW-WS", ParentCategoryId = hardwareId, DescriptionEn = "Desktop computers and laptops", DescriptionAr = "أجهزة الكمبيوتر المكتبية والمحمولة", DefaultDepreciationRate = 25, DefaultUsefulLifeYears = 4 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Network Equipment", NameAr = "معدات الشبكة", Code = "AST-HW-NET", ParentCategoryId = hardwareId, DescriptionEn = "Routers, switches, and firewalls", DescriptionAr = "أجهزة التوجيه والمحولات وجدران الحماية", DefaultDepreciationRate = 15, DefaultUsefulLifeYears = 7 },
            new() { Id = softwareId, NameEn = "Software Assets", NameAr = "الأصول البرمجية", Code = "AST-SW", DescriptionEn = "Software licenses and applications", DescriptionAr = "تراخيص البرامج والتطبيقات", DefaultDepreciationRate = 33, DefaultUsefulLifeYears = 3 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Enterprise Applications", NameAr = "تطبيقات المؤسسة", Code = "AST-SW-ENT", ParentCategoryId = softwareId, DescriptionEn = "Core business applications", DescriptionAr = "تطبيقات الأعمال الأساسية", DefaultDepreciationRate = 20, DefaultUsefulLifeYears = 5 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Productivity Software", NameAr = "برامج الإنتاجية", Code = "AST-SW-PROD", ParentCategoryId = softwareId, DescriptionEn = "Office and productivity tools", DescriptionAr = "أدوات المكتب والإنتاجية", DefaultDepreciationRate = 33, DefaultUsefulLifeYears = 3 },
            new() { Id = facilitiesId, NameEn = "Facilities", NameAr = "المرافق", Code = "AST-FAC", DescriptionEn = "Building and facility assets", DescriptionAr = "أصول المباني والمرافق", DefaultDepreciationRate = 5, DefaultUsefulLifeYears = 20 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Office Equipment", NameAr = "معدات المكتب", Code = "AST-FAC-OFF", ParentCategoryId = facilitiesId, DescriptionEn = "Furniture and office equipment", DescriptionAr = "الأثاث ومعدات المكتب", DefaultDepreciationRate = 10, DefaultUsefulLifeYears = 10 },
            // Real-estate hierarchy. Long useful lives + low depreciation reflect
            // the typical 30+ year horizon for housing stock; tune per client.
            new() { Id = realEstateId, NameEn = "Real Estate", NameAr = "العقارات", Code = "AST-RE", DescriptionEn = "Land and built real-estate assets", DescriptionAr = "الأراضي والأصول العقارية المبنية", DefaultDepreciationRate = 3, DefaultUsefulLifeYears = 30 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Housing Project", NameAr = "مشروع إسكاني", Code = "AST-RE-PROJ", ParentCategoryId = realEstateId, DescriptionEn = "A multi-unit housing development (parent of villas / buildings)", DescriptionAr = "مشروع إسكاني متعدد الوحدات (يجمع الفلل / المباني)", DefaultDepreciationRate = 3, DefaultUsefulLifeYears = 40 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Villa", NameAr = "فيلا", Code = "AST-RE-VILLA", ParentCategoryId = realEstateId, DescriptionEn = "Stand-alone or attached residential villa", DescriptionAr = "فيلا سكنية مستقلة أو متصلة", DefaultDepreciationRate = 4, DefaultUsefulLifeYears = 30 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Building", NameAr = "مبنى", Code = "AST-RE-BLDG", ParentCategoryId = realEstateId, DescriptionEn = "Multi-floor residential or mixed-use building", DescriptionAr = "مبنى سكني أو متعدد الاستخدامات", DefaultDepreciationRate = 4, DefaultUsefulLifeYears = 35 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Plot", NameAr = "قطعة أرض", Code = "AST-RE-PLOT", ParentCategoryId = realEstateId, DescriptionEn = "Undeveloped land parcel or building plot", DescriptionAr = "قطعة أرض غير مطورة أو قطعة بناء", DefaultDepreciationRate = 0, DefaultUsefulLifeYears = 99 },
            // Information-asset hierarchy. Depreciation is 0 and useful-life
            // is open-ended (99y) because information assets don't depreciate
            // financially — their value is governed by retention policy + risk.
            new() { Id = informationId, NameEn = "Information Asset", NameAr = "أصل معلوماتي", Code = "AST-INFO", DescriptionEn = "Data, documents, datasets and other information records under ISO 27001 / PDPL", DescriptionAr = "البيانات والمستندات والسجلات المعلوماتية وفق ISO 27001 / قانون حماية البيانات", DefaultDepreciationRate = 0, DefaultUsefulLifeYears = 99 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Database", NameAr = "قاعدة بيانات", Code = "AST-INFO-DB", ParentCategoryId = informationId, DescriptionEn = "Structured database holding business records (SQL / NoSQL)", DescriptionAr = "قاعدة بيانات منظمة تحتوي على سجلات الأعمال (SQL / NoSQL)", DefaultDepreciationRate = 0, DefaultUsefulLifeYears = 99 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Document set", NameAr = "مجموعة مستندات", Code = "AST-INFO-DOC", ParentCategoryId = informationId, DescriptionEn = "A managed collection of documents (policies, contracts, records)", DescriptionAr = "مجموعة مستندات مُدارة (السياسات والعقود والسجلات)", DefaultDepreciationRate = 0, DefaultUsefulLifeYears = 99 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Dataset", NameAr = "مجموعة بيانات", Code = "AST-INFO-DATA", ParentCategoryId = informationId, DescriptionEn = "Analytical dataset (CSV / parquet / JSONL) for reporting or ML", DescriptionAr = "مجموعة بيانات تحليلية للتقارير أو الذكاء الاصطناعي", DefaultDepreciationRate = 0, DefaultUsefulLifeYears = 99 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Application data", NameAr = "بيانات تطبيقية", Code = "AST-INFO-APP", ParentCategoryId = informationId, DescriptionEn = "Data owned by a specific application (logs, config, audit trails)", DescriptionAr = "بيانات يملكها تطبيق محدد (السجلات والإعدادات ومسارات التدقيق)", DefaultDepreciationRate = 0, DefaultUsefulLifeYears = 99 }
        };
    }

    private static List<Asset> CreateAssets(List<AssetCategory> categories, List<Process> processes, List<OrganizationUnit> orgUnits)
    {
        var serverCategory = categories.First(c => c.Code == "AST-HW-SRV");
        var workstationCategory = categories.First(c => c.Code == "AST-HW-WS");
        var networkCategory = categories.First(c => c.Code == "AST-HW-NET");
        var enterpriseAppCategory = categories.First(c => c.Code == "AST-SW-ENT");

        return new List<Asset>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Production Database Server", NameAr = "خادم قاعدة البيانات الإنتاجي", AssetTag = "SRV-DB-001", CategoryId = serverCategory.Id, Status = AssetStatus.Operational, AssignedToUnitId = orgUnits[3].Id, PurchaseDate = DateTime.Now.AddYears(-2), PurchaseCost = 150000, CurrentValue = 90000, Location = "Main Data Center - Rack A1", Manufacturer = "Dell", Model = "PowerEdge R750", SerialNumber = "DB2022R750001", DescriptionEn = "Primary SQL Server database server for HMS", DescriptionAr = "خادم قاعدة بيانات SQL الرئيسي لنظام إدارة الإسكان" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Application Server 1", NameAr = "خادم التطبيقات 1", AssetTag = "SRV-APP-001", CategoryId = serverCategory.Id, Status = AssetStatus.Operational, AssignedToUnitId = orgUnits[3].Id, PurchaseDate = DateTime.Now.AddYears(-1), PurchaseCost = 120000, CurrentValue = 96000, Location = "Main Data Center - Rack A2", Manufacturer = "HP", Model = "ProLiant DL380", SerialNumber = "APP2023DL380001", DescriptionEn = "Primary application server for customer portal", DescriptionAr = "خادم التطبيقات الرئيسي لبوابة العملاء" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Core Network Switch", NameAr = "محول الشبكة الأساسي", AssetTag = "NET-SW-001", CategoryId = networkCategory.Id, Status = AssetStatus.Operational, AssignedToUnitId = orgUnits[3].Id, PurchaseDate = DateTime.Now.AddYears(-3), PurchaseCost = 80000, CurrentValue = 40000, Location = "Main Data Center - Network Rack", Manufacturer = "Cisco", Model = "Catalyst 9500", SerialNumber = "NET2021C9500001", DescriptionEn = "Core network switch for data center", DescriptionAr = "محول الشبكة الأساسي لمركز البيانات" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Housing Management System License", NameAr = "ترخيص نظام إدارة الإسكان", AssetTag = "SW-HMS-001", CategoryId = enterpriseAppCategory.Id, Status = AssetStatus.Operational, AssignedToUnitId = orgUnits[3].Id, PurchaseDate = DateTime.Now.AddYears(-2), PurchaseCost = 500000, CurrentValue = 350000, ProcessId = processes.First(p => p.Code == "1.1.1").Id, DescriptionEn = "Core housing management system software license", DescriptionAr = "ترخيص برنامج نظام إدارة الإسكان الأساسي", Notes = "Enterprise Perpetual License - Valid until 2029" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "CRM System License", NameAr = "ترخيص نظام إدارة العملاء", AssetTag = "SW-CRM-001", CategoryId = enterpriseAppCategory.Id, Status = AssetStatus.Operational, AssignedToUnitId = orgUnits[2].Id, PurchaseDate = DateTime.Now.AddYears(-1), PurchaseCost = 200000, CurrentValue = 160000, DescriptionEn = "Salesforce CRM license for customer service", DescriptionAr = "ترخيص Salesforce لإدارة خدمة العملاء", Notes = "Annual Subscription - Renewal in 6 months" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "IT Department Workstations", NameAr = "محطات عمل قسم تقنية المعلومات", AssetTag = "WS-IT-BATCH001", CategoryId = workstationCategory.Id, Status = AssetStatus.Operational, AssignedToUnitId = orgUnits[3].Id, PurchaseDate = DateTime.Now.AddMonths(-6), PurchaseCost = 75000, CurrentValue = 67500, Manufacturer = "Dell", Model = "OptiPlex 7090", DescriptionEn = "15 workstations for IT department", DescriptionAr = "15 محطة عمل لقسم تقنية المعلومات" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Backup Server", NameAr = "خادم النسخ الاحتياطي", AssetTag = "SRV-BKP-001", CategoryId = serverCategory.Id, Status = AssetStatus.Operational, AssignedToUnitId = orgUnits[3].Id, PurchaseDate = DateTime.Now.AddYears(-2), PurchaseCost = 100000, CurrentValue = 60000, Location = "DR Site - Rack B1", Manufacturer = "Dell", Model = "PowerEdge R740", SerialNumber = "BKP2022R740001", DescriptionEn = "Disaster recovery backup server", DescriptionAr = "خادم النسخ الاحتياطي للتعافي من الكوارث" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Firewall Appliance", NameAr = "جهاز جدار الحماية", AssetTag = "NET-FW-001", CategoryId = networkCategory.Id, Status = AssetStatus.Operational, AssignedToUnitId = orgUnits[3].Id, PurchaseDate = DateTime.Now.AddMonths(-18), PurchaseCost = 120000, CurrentValue = 84000, Location = "Main Data Center - Security Zone", Manufacturer = "Palo Alto", Model = "PA-5220", SerialNumber = "FW2023PA5220001", DescriptionEn = "Primary firewall for perimeter security", DescriptionAr = "جدار الحماية الرئيسي لأمن المحيط" }
        };
    }


    private static List<RiskCategory> CreateRiskCategories()
    {
        return new List<RiskCategory>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Strategic Risk", NameAr = "المخاطر الاستراتيجية", Code = "RSK-STR", DescriptionEn = "Risks affecting strategic objectives", DescriptionAr = "المخاطر المؤثرة على الأهداف الاستراتيجية", DefaultReviewFrequencyDays = 90 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Operational Risk", NameAr = "المخاطر التشغيلية", Code = "RSK-OPS", DescriptionEn = "Risks affecting day-to-day operations", DescriptionAr = "المخاطر المؤثرة على العمليات اليومية", DefaultReviewFrequencyDays = 30 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Financial Risk", NameAr = "المخاطر المالية", Code = "RSK-FIN", DescriptionEn = "Risks affecting financial performance", DescriptionAr = "المخاطر المؤثرة على الأداء المالي", DefaultReviewFrequencyDays = 60 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Compliance Risk", NameAr = "مخاطر الامتثال", Code = "RSK-CMP", DescriptionEn = "Risks related to regulatory compliance", DescriptionAr = "المخاطر المتعلقة بالامتثال التنظيمي", DefaultReviewFrequencyDays = 90 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Technology Risk", NameAr = "المخاطر التقنية", Code = "RSK-TEC", DescriptionEn = "Risks related to IT and cybersecurity", DescriptionAr = "المخاطر المتعلقة بتقنية المعلومات", DefaultReviewFrequencyDays = 30 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Reputational Risk", NameAr = "مخاطر السمعة", Code = "RSK-REP", DescriptionEn = "Risks affecting reputation", DescriptionAr = "المخاطر المؤثرة على السمعة", DefaultReviewFrequencyDays = 60 }
        };
    }

    private static List<EnterpriseRisk> CreateEnterpriseRisks(List<RiskCategory> categories, List<Process> processes, List<OrganizationUnit> orgUnits)
    {
        var opsCat = categories.First(c => c.Code == "RSK-OPS");
        var techCat = categories.First(c => c.Code == "RSK-TEC");
        var cmpCat = categories.First(c => c.Code == "RSK-CMP");
        var finCat = categories.First(c => c.Code == "RSK-FIN");

        return new List<EnterpriseRisk>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "System Downtime", NameAr = "توقف النظام", RiskNumber = "RSK-2024-001", CategoryId = techCat.Id, OrganizationUnitId = orgUnits[3].Id, ProcessId = processes.First(p => p.Code == "1.1.1").Id, DescriptionEn = "HMS system unavailability during peak periods", DescriptionAr = "عدم توفر نظام HMS خلال فترات الذروة", RiskLevel = RiskLevel.High, Likelihood = 3, Impact = 4, InherentRiskScore = 12, ResponseStrategy = "High availability cluster implementation", CurrentControls = "Daily monitoring, backup systems", ControlEffectiveness = 4, LastReviewDate = DateTime.Now.AddDays(-30), NextReviewDate = DateTime.Now.AddDays(60), IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Data Breach", NameAr = "اختراق البيانات", RiskNumber = "RSK-2024-002", CategoryId = techCat.Id, OrganizationUnitId = orgUnits[3].Id, DescriptionEn = "Unauthorized access to customer data", DescriptionAr = "الوصول غير المصرح به للبيانات", RiskLevel = RiskLevel.Critical, Likelihood = 2, Impact = 5, InherentRiskScore = 10, ResponseStrategy = "Enhanced encryption and 24/7 monitoring", CurrentControls = "Encryption, firewalls, access controls", ControlEffectiveness = 4, LastReviewDate = DateTime.Now.AddDays(-15), NextReviewDate = DateTime.Now.AddDays(30), IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Processing Delays", NameAr = "تأخير المعالجة", RiskNumber = "RSK-2024-003", CategoryId = opsCat.Id, OrganizationUnitId = orgUnits[1].Id, ProcessId = processes.First(p => p.Code == "1.1.2").Id, DescriptionEn = "Exceeding SLA for application processing", DescriptionAr = "تجاوز اتفاقية مستوى الخدمة", RiskLevel = RiskLevel.Medium, Likelihood = 4, Impact = 3, InherentRiskScore = 12, ResponseStrategy = "Process automation and staff training", CurrentControls = "SLA monitoring dashboards", ControlEffectiveness = 3, LastReviewDate = DateTime.Now.AddDays(-45), NextReviewDate = DateTime.Now.AddDays(45), IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Privacy Non-Compliance", NameAr = "عدم الامتثال للخصوصية", RiskNumber = "RSK-2024-004", CategoryId = cmpCat.Id, OrganizationUnitId = orgUnits[6].Id, DescriptionEn = "Violating UAE data protection regulations", DescriptionAr = "انتهاك لوائح حماية البيانات", RiskLevel = RiskLevel.High, Likelihood = 2, Impact = 5, InherentRiskScore = 10, ResponseStrategy = "Regular compliance audits and training", CurrentControls = "Privacy policy, data handling procedures", ControlEffectiveness = 4, LastReviewDate = DateTime.Now.AddDays(-20), NextReviewDate = DateTime.Now.AddDays(40), IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Budget Overrun", NameAr = "تجاوز الميزانية", RiskNumber = "RSK-2024-005", CategoryId = finCat.Id, OrganizationUnitId = orgUnits[8].Id, DescriptionEn = "Exceeding construction project budget", DescriptionAr = "تجاوز ميزانية المشاريع", RiskLevel = RiskLevel.Medium, Likelihood = 3, Impact = 4, InherentRiskScore = 12, ResponseStrategy = "Enhanced project monitoring and controls", CurrentControls = "Monthly budget reviews, approval workflows", ControlEffectiveness = 3, LastReviewDate = DateTime.Now.AddDays(-60), NextReviewDate = DateTime.Now.AddDays(30), IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Staff Shortage", NameAr = "نقص الموظفين", RiskNumber = "RSK-2024-006", CategoryId = opsCat.Id, OrganizationUnitId = orgUnits[2].Id, DescriptionEn = "Insufficient staff for customer service", DescriptionAr = "نقص موظفي خدمة العملاء", RiskLevel = RiskLevel.Low, Likelihood = 3, Impact = 2, InherentRiskScore = 6, ResponseStrategy = "Cross-training program implementation", CurrentControls = "Resource planning, contractor pool", ControlEffectiveness = 4, ResidualLikelihood = 2, ResidualImpact = 2, ResidualRiskScore = 4, LastReviewDate = DateTime.Now.AddDays(-10), NextReviewDate = DateTime.Now.AddDays(90), IsActive = true }
        };
    }

    private static List<FeedbackCategory> CreateFeedbackCategories(List<OrganizationUnit> orgUnits)
    {
        return new List<FeedbackCategory>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Service Quality", NameAr = "جودة الخدمة", Code = "FB-SQ", DescriptionEn = "Feedback on service quality", DescriptionAr = "ملاحظات على جودة الخدمة", DefaultPriority = 2, ExpectedResponseTimeHours = 24, DefaultAssignedToUnitId = orgUnits[7].Id },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Staff Behavior", NameAr = "سلوك الموظفين", Code = "FB-SB", DescriptionEn = "Feedback on staff behavior", DescriptionAr = "ملاحظات على سلوك الموظفين", DefaultPriority = 2, ExpectedResponseTimeHours = 24, DefaultAssignedToUnitId = orgUnits[5].Id },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "System Issues", NameAr = "مشاكل النظام", Code = "FB-SI", DescriptionEn = "Feedback on system issues", DescriptionAr = "ملاحظات على مشاكل النظام", DefaultPriority = 1, ExpectedResponseTimeHours = 8, DefaultAssignedToUnitId = orgUnits[3].Id },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Process Efficiency", NameAr = "كفاءة العمليات", Code = "FB-PE", DescriptionEn = "Feedback on process efficiency", DescriptionAr = "ملاحظات على كفاءة العمليات", DefaultPriority = 3, ExpectedResponseTimeHours = 48, DefaultAssignedToUnitId = orgUnits[7].Id },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Suggestions", NameAr = "اقتراحات", Code = "FB-SUG", DescriptionEn = "Customer suggestions", DescriptionAr = "اقتراحات العملاء", DefaultPriority = 4, ExpectedResponseTimeHours = 72, DefaultAssignedToUnitId = orgUnits[2].Id },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Compliments", NameAr = "ثناء", Code = "FB-CMP", DescriptionEn = "Customer compliments", DescriptionAr = "ثناء العملاء", DefaultPriority = 4, ExpectedResponseTimeHours = 48, DefaultAssignedToUnitId = orgUnits[2].Id }
        };
    }

    private static List<CustomerFeedback> CreateCustomerFeedback(List<FeedbackCategory> categories, List<Service> services, List<Process> processes, List<OrganizationUnit> orgUnits)
    {
        var sqCat = categories.First(c => c.Code == "FB-SQ");
        var sbCat = categories.First(c => c.Code == "FB-SB");
        var siCat = categories.First(c => c.Code == "FB-SI");
        var sugCat = categories.First(c => c.Code == "FB-SUG");
        var cmpCat = categories.First(c => c.Code == "FB-CMP");

        return new List<CustomerFeedback>
        {
            new() { Id = Guid.NewGuid().ToString(), FeedbackNumber = "FB-2024-001", NameEn = "Positive Application Experience", NameAr = "تجربة إيجابية للتقديم", DescriptionEn = "The application process was very smooth and easy to follow.", DescriptionAr = "كانت عملية التقديم سلسة وسهلة المتابعة", Type = FeedbackType.Compliment, CategoryId = sqCat.Id, ServiceId = services.First(s => s.Code == "SVC-001").Id, CustomerName = "Ahmed Al Maktoum", CustomerEmail = "ahmed@example.com", CustomerPhone = "+971501234567", SatisfactionRating = 5, Status = FeedbackStatus.Closed, SubmittedDate = DateTime.Now.AddDays(-20), AssignedToUnitId = orgUnits[1].Id, ResponseDate = DateTime.Now.AddDays(-18), Response = "Thank you for your kind feedback!" },
            new() { Id = Guid.NewGuid().ToString(), FeedbackNumber = "FB-2024-002", NameEn = "Website Performance Issue", NameAr = "مشكلة أداء الموقع", DescriptionEn = "The website was slow during peak hours.", DescriptionAr = "كان الموقع بطيئاً خلال ساعات الذروة", Type = FeedbackType.Complaint, CategoryId = siCat.Id, ServiceId = services.First(s => s.Code == "SVC-002").Id, CustomerName = "Fatima Al Rashid", CustomerEmail = "fatima@example.com", CustomerPhone = "+971502345678", SatisfactionRating = 3, Status = FeedbackStatus.InProgress, SubmittedDate = DateTime.Now.AddDays(-5), AssignedToUnitId = orgUnits[3].Id, Priority = 2 },
            new() { Id = Guid.NewGuid().ToString(), FeedbackNumber = "FB-2024-003", NameEn = "Excellent Staff Service", NameAr = "خدمة موظفين ممتازة", DescriptionEn = "Staff at customer service was very helpful and professional.", DescriptionAr = "كان موظفو خدمة العملاء متعاونين ومحترفين للغاية", Type = FeedbackType.Compliment, CategoryId = sbCat.Id, ProcessId = processes.First(p => p.Code == "2.1.1").Id, CustomerName = "Mohammed Al Suwaidi", CustomerEmail = "mohammed@example.com", SatisfactionRating = 5, Status = FeedbackStatus.Closed, SubmittedDate = DateTime.Now.AddDays(-15), AssignedToUnitId = orgUnits[2].Id, ResponseDate = DateTime.Now.AddDays(-14), Response = "We will share your feedback with our team!" },
            new() { Id = Guid.NewGuid().ToString(), FeedbackNumber = "FB-2024-004", NameEn = "Mobile App Suggestion", NameAr = "اقتراح تطبيق جوال", DescriptionEn = "Would be great to have a mobile app for tracking applications.", DescriptionAr = "سيكون من الرائع وجود تطبيق جوال لتتبع الطلبات", Type = FeedbackType.Suggestion, CategoryId = sugCat.Id, CustomerName = "Sara Al Nahyan", CustomerEmail = "sara@example.com", SatisfactionRating = 4, Status = FeedbackStatus.New, SubmittedDate = DateTime.Now.AddDays(-2), AssignedToUnitId = orgUnits[3].Id, Priority = 4 },
            new() { Id = Guid.NewGuid().ToString(), FeedbackNumber = "FB-2024-005", NameEn = "Fast Maintenance Response", NameAr = "استجابة صيانة سريعة", DescriptionEn = "Maintenance team fixed my issue within 24 hours. Excellent service!", DescriptionAr = "قام فريق الصيانة بإصلاح مشكلتي خلال 24 ساعة. خدمة ممتازة!", Type = FeedbackType.Compliment, CategoryId = cmpCat.Id, ServiceId = services.First(s => s.Code == "SVC-003").Id, CustomerName = "Khalid Al Qasimi", CustomerEmail = "khalid@example.com", SatisfactionRating = 5, Status = FeedbackStatus.Closed, SubmittedDate = DateTime.Now.AddDays(-10), AssignedToUnitId = orgUnits[1].Id, ResponseDate = DateTime.Now.AddDays(-9), Response = "Thank you! We strive to provide timely service." },
            new() { Id = Guid.NewGuid().ToString(), FeedbackNumber = "FB-2024-006", NameEn = "Confusing Payment Process", NameAr = "عملية دفع مربكة", DescriptionEn = "Payment process was confusing. Needs improvement.", DescriptionAr = "كانت عملية الدفع مربكة وتحتاج إلى تحسين", Type = FeedbackType.Complaint, CategoryId = sqCat.Id, ServiceId = services.First(s => s.Code == "SVC-004").Id, CustomerName = "Aisha Al Mazrouei", CustomerEmail = "aisha@example.com", SatisfactionRating = 2, Status = FeedbackStatus.InProgress, SubmittedDate = DateTime.Now.AddDays(-3), AssignedToUnitId = orgUnits[4].Id, Priority = 2 }
        };
    }

    private static List<SLADefinition> CreateSLADefinitions(List<Service> services, List<OrganizationUnit> orgUnits)
    {
        return new List<SLADefinition>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Housing Grant Application SLA", NameAr = "اتفاقية طلب المنحة السكنية", Code = "SLA-001", ServiceId = services.First(s => s.Code == "SVC-001").Id, ResponsibleUnitId = orgUnits[1].Id, MetricName = "Resolution Time", TargetValue = 120, Unit = "Hours", WarningThreshold = 80, MeasurementFrequency = "Daily", IsActive = true, DescriptionEn = "SLA for housing grant applications - 120 hour resolution target", DescriptionAr = "اتفاقية مستوى الخدمة لطلبات المنح السكنية - هدف الحل 120 ساعة", PenaltyForBreach = "Escalation to Director" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Housing Loan Application SLA", NameAr = "اتفاقية طلب القرض السكني", Code = "SLA-002", ServiceId = services.First(s => s.Code == "SVC-002").Id, ResponsibleUnitId = orgUnits[1].Id, MetricName = "Resolution Time", TargetValue = 240, Unit = "Hours", WarningThreshold = 75, MeasurementFrequency = "Daily", IsActive = true, DescriptionEn = "SLA for housing loan applications - 240 hour resolution target", DescriptionAr = "اتفاقية مستوى الخدمة لطلبات القروض السكنية - هدف الحل 240 ساعة", PenaltyForBreach = "Escalation to Director" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Maintenance Request SLA", NameAr = "اتفاقية طلب الصيانة", Code = "SLA-003", ServiceId = services.First(s => s.Code == "SVC-003").Id, ResponsibleUnitId = orgUnits[1].Id, MetricName = "Resolution Time", TargetValue = 72, Unit = "Hours", WarningThreshold = 85, MeasurementFrequency = "Daily", IsActive = true, DescriptionEn = "SLA for maintenance requests - 72 hour resolution target", DescriptionAr = "اتفاقية مستوى الخدمة لطلبات الصيانة - هدف الحل 72 ساعة", PenaltyForBreach = "Customer compensation" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Customer Inquiry SLA", NameAr = "اتفاقية الاستفسار", Code = "SLA-004", ServiceId = services.First(s => s.Code == "SVC-005").Id, ResponsibleUnitId = orgUnits[2].Id, MetricName = "First Response Time", TargetValue = 1, Unit = "Hours", WarningThreshold = 90, MeasurementFrequency = "Hourly", IsActive = true, DescriptionEn = "SLA for customer inquiries - 1 hour first response target", DescriptionAr = "اتفاقية مستوى الخدمة للاستفسارات - هدف الاستجابة الأولى ساعة واحدة", PenaltyForBreach = "Agent performance review" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Complaint Resolution SLA", NameAr = "اتفاقية حل الشكاوى", Code = "SLA-005", ServiceId = services.First(s => s.Code == "SVC-006").Id, ResponsibleUnitId = orgUnits[2].Id, MetricName = "Resolution Time", TargetValue = 48, Unit = "Hours", WarningThreshold = 80, MeasurementFrequency = "Daily", IsActive = true, DescriptionEn = "SLA for complaint resolution - 48 hour resolution target", DescriptionAr = "اتفاقية مستوى الخدمة لحل الشكاوى - هدف الحل 48 ساعة", PenaltyForBreach = "Manager escalation and customer follow-up" }
        };
    }

    private static List<Incident> CreateIncidents(List<Service> services, List<Process> processes, List<OrganizationUnit> orgUnits)
    {
        return new List<Incident>
        {
            new() { Id = Guid.NewGuid().ToString(), IncidentNumber = "INC-2024-001", NameEn = "Customer Portal Login Failure", NameAr = "فشل تسجيل الدخول للبوابة", DescriptionEn = "Customers unable to login to portal", DescriptionAr = "العملاء غير قادرين على تسجيل الدخول", ServiceId = services.First(s => s.Code == "SVC-001").Id, ProcessId = processes.First(p => p.Code == "1.1.1").Id, Status = IncidentStatus.Resolved, Priority = 2, Impact = 2, Urgency = 1, Category = "Authentication", ReportedAt = DateTime.Now.AddDays(-10), AssignedToUnitId = orgUnits[3].Id, ResolvedAt = DateTime.Now.AddDays(-10).AddHours(2), ResolutionNotes = "Authentication service restarted", SlaTargetHours = 8, SlaDueDate = DateTime.Now.AddDays(-10).AddHours(8) },
            new() { Id = Guid.NewGuid().ToString(), IncidentNumber = "INC-2024-002", NameEn = "Slow Application Processing", NameAr = "بطء معالجة الطلبات", DescriptionEn = "Application processing taking longer than expected", DescriptionAr = "معالجة الطلبات تستغرق وقتاً أطول", ServiceId = services.First(s => s.Code == "SVC-001").Id, ProcessId = processes.First(p => p.Code == "1.1.2").Id, Status = IncidentStatus.InProgress, Priority = 3, Impact = 3, Urgency = 3, Category = "Performance", ReportedAt = DateTime.Now.AddDays(-2), AssignedToUnitId = orgUnits[1].Id, SlaTargetHours = 24, SlaDueDate = DateTime.Now.AddDays(-2).AddHours(24) },
            new() { Id = Guid.NewGuid().ToString(), IncidentNumber = "INC-2024-003", NameEn = "Payment Gateway Timeout", NameAr = "انتهاء مهلة بوابة الدفع", DescriptionEn = "Payment transactions timing out", DescriptionAr = "انتهاء مهلة معاملات الدفع", ServiceId = services.First(s => s.Code == "SVC-004").Id, Status = IncidentStatus.Resolved, Priority = 1, Impact = 1, Urgency = 1, Category = "Payment", ReportedAt = DateTime.Now.AddDays(-5), AssignedToUnitId = orgUnits[3].Id, ResolvedAt = DateTime.Now.AddDays(-5).AddHours(1), ResolutionNotes = "Payment gateway vendor resolved connectivity issue", SlaTargetHours = 4, SlaDueDate = DateTime.Now.AddDays(-5).AddHours(4) },
            new() { Id = Guid.NewGuid().ToString(), IncidentNumber = "INC-2024-004", NameEn = "Email Notifications Not Sending", NameAr = "عدم إرسال إشعارات البريد", DescriptionEn = "System email notifications not being delivered", DescriptionAr = "إشعارات البريد الإلكتروني لا ترسل", Status = IncidentStatus.New, Priority = 3, Impact = 3, Urgency = 2, Category = "Communication", ReportedAt = DateTime.Now.AddHours(-4), AssignedToUnitId = orgUnits[3].Id, SlaTargetHours = 24, SlaDueDate = DateTime.Now.AddHours(-4).AddHours(24) },
            new() { Id = Guid.NewGuid().ToString(), IncidentNumber = "INC-2024-005", NameEn = "Document Upload Error", NameAr = "خطأ في رفع المستندات", DescriptionEn = "Customers receiving error when uploading documents", DescriptionAr = "العملاء يتلقون خطأ عند رفع المستندات", ServiceId = services.First(s => s.Code == "SVC-001").Id, ProcessId = processes.First(p => p.Code == "1.1.1").Id, Status = IncidentStatus.Resolved, Priority = 2, Impact = 2, Urgency = 2, Category = "File Management", ReportedAt = DateTime.Now.AddDays(-7), AssignedToUnitId = orgUnits[3].Id, ResolvedAt = DateTime.Now.AddDays(-7).AddHours(4), ResolutionNotes = "Increased storage allocation on file server", SlaTargetHours = 8, SlaDueDate = DateTime.Now.AddDays(-7).AddHours(8) }
        };
    }

    private static List<Problem> CreateProblems(List<Service> services, List<Process> processes, List<OrganizationUnit> orgUnits)
    {
        return new List<Problem>
        {
            new() { Id = Guid.NewGuid().ToString(), ProblemNumber = "PRB-2024-001", NameEn = "Recurring Login Failures", NameAr = "فشل تسجيل الدخول المتكرر", DescriptionEn = "Multiple incidents of login failures during peak hours", DescriptionAr = "حوادث متعددة لفشل تسجيل الدخول خلال الذروة", ServiceId = services.First(s => s.Code == "SVC-001").Id, Status = ProblemStatus.RootCauseIdentified, Priority = 2, Impact = 2, Category = "Authentication", IdentifiedAt = DateTime.Now.AddDays(-15), RootCauseIdentifiedAt = DateTime.Now.AddDays(-12), AssignedToUnitId = orgUnits[3].Id, RootCauseAnalysis = "Authentication service memory leak under load causing session timeout", Workaround = "Scheduled service restart every 12 hours", RelatedIncidentCount = 5, EstimatedCostImpact = 15000 },
            new() { Id = Guid.NewGuid().ToString(), ProblemNumber = "PRB-2024-002", NameEn = "Slow Database Queries", NameAr = "بطء استعلامات قاعدة البيانات", DescriptionEn = "Database performance degradation affecting multiple services", DescriptionAr = "تدهور أداء قاعدة البيانات يؤثر على الخدمات", Status = ProblemStatus.InvestigationInProgress, Priority = 2, Impact = 2, Category = "Database", IdentifiedAt = DateTime.Now.AddDays(-8), AssignedToUnitId = orgUnits[3].Id, RelatedIncidentCount = 3, EstimatedCostImpact = 25000 },
            new() { Id = Guid.NewGuid().ToString(), ProblemNumber = "PRB-2024-003", NameEn = "Email Delivery Delays", NameAr = "تأخير تسليم البريد", DescriptionEn = "Systemic delays in email notification delivery", DescriptionAr = "تأخيرات منهجية في تسليم إشعارات البريد", Status = ProblemStatus.Resolved, Priority = 3, Impact = 3, Category = "Communication", IdentifiedAt = DateTime.Now.AddDays(-20), RootCauseIdentifiedAt = DateTime.Now.AddDays(-18), ResolvedAt = DateTime.Now.AddDays(-15), AssignedToUnitId = orgUnits[3].Id, RootCauseAnalysis = "Email server queue configuration causing bottleneck during peak hours", Workaround = "N/A", PermanentSolution = "Reconfigured email queue parameters and added secondary SMTP server for load balancing", RelatedIncidentCount = 8, EstimatedCostImpact = 5000, ActualCostImpact = 3500 }
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // ISO Module Dependent Seeding Methods
    // ═══════════════════════════════════════════════════════════════════

    private static List<MaintenanceSchedule> CreateMaintenanceSchedules(List<Asset> assets)
    {
        return new List<MaintenanceSchedule>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Server Monthly Health Check", NameAr = "فحص صحة الخادم الشهري", DescriptionEn = "Monthly preventive maintenance for production servers", DescriptionAr = "صيانة وقائية شهرية لخوادم الإنتاج", AssetId = assets[0].Id, Type = MaintenanceType.Preventive, FrequencyDays = 30, NextScheduledDate = DateTime.Now.AddDays(15), EstimatedDurationHours = 2, EstimatedCost = 500, LastPerformedDate = DateTime.Now.AddDays(-15) },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Network Equipment Firmware Update", NameAr = "تحديث برنامج معدات الشبكة", DescriptionEn = "Quarterly firmware updates for network switches", DescriptionAr = "تحديثات البرامج الثابتة الفصلية لمحولات الشبكة", AssetId = assets[2].Id, Type = MaintenanceType.Preventive, FrequencyDays = 90, NextScheduledDate = DateTime.Now.AddDays(45), EstimatedDurationHours = 4, EstimatedCost = 1000, LastPerformedDate = DateTime.Now.AddDays(-45) },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Firewall Rule Audit", NameAr = "تدقيق قواعد جدار الحماية", DescriptionEn = "Bi-weekly firewall rule review and cleanup", DescriptionAr = "مراجعة وتنظيف قواعد جدار الحماية نصف شهرية", AssetId = assets[7].Id, Type = MaintenanceType.Predictive, FrequencyDays = 14, NextScheduledDate = DateTime.Now.AddDays(7), EstimatedDurationHours = 3, EstimatedCost = 750, LastPerformedDate = DateTime.Now.AddDays(-7) }
        };
    }

    private static List<MaintenanceRecord> CreateMaintenanceRecords(List<Asset> assets, List<MaintenanceSchedule> schedules)
    {
        return new List<MaintenanceRecord>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Server Health Check - Jan 2024", NameAr = "فحص صحة الخادم - يناير 2024", AssetId = assets[0].Id, MaintenanceScheduleId = schedules[0].Id, Type = MaintenanceType.Preventive, PerformedDate = DateTime.Now.AddDays(-15), DurationHours = 1.5m, Cost = 450, WorkPerformed = "Checked CPU, memory, disk usage. Updated OS patches.", IsCompleted = true, NextMaintenanceDue = DateTime.Now.AddDays(15) },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Emergency Switch Replacement", NameAr = "استبدال محول طارئ", AssetId = assets[2].Id, Type = MaintenanceType.Emergency, PerformedDate = DateTime.Now.AddDays(-30), DurationHours = 6, Cost = 15000, WorkPerformed = "Replaced failed line card on core switch", PartsReplaced = "Catalyst 9500 Line Card", IssuesFound = "Line card failure due to power surge", DowntimeHours = 4, IsCompleted = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Firewall Configuration Backup", NameAr = "نسخ احتياطي لإعدادات جدار الحماية", AssetId = assets[7].Id, MaintenanceScheduleId = schedules[2].Id, Type = MaintenanceType.Preventive, PerformedDate = DateTime.Now.AddDays(-7), DurationHours = 1, Cost = 200, WorkPerformed = "Backed up firewall configuration and reviewed active rules", IsCompleted = true, NextMaintenanceDue = DateTime.Now.AddDays(7) }
        };
    }

    private static List<RiskActionPlan> CreateRiskActionPlans(List<EnterpriseRisk> risks)
    {
        return new List<RiskActionPlan>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Implement HA Cluster", NameAr = "تنفيذ مجموعة التوفر العالي", DescriptionEn = "Deploy high-availability cluster for HMS", DescriptionAr = "نشر مجموعة التوفر العالي لنظام HMS", RiskId = risks[0].Id, Priority = 1, Status = ESEMS.Web.Models.Enums.RiskActionPlanStatus.InProgress, ProgressPercentage = 60, TargetDate = DateTime.Now.AddDays(30), EstimatedCost = 50000, ExpectedRiskReduction = 40 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Deploy SIEM Solution", NameAr = "نشر حل SIEM", DescriptionEn = "Implement Security Information and Event Management", DescriptionAr = "تنفيذ نظام إدارة المعلومات والأحداث الأمنية", RiskId = risks[1].Id, Priority = 1, Status = ESEMS.Web.Models.Enums.RiskActionPlanStatus.NotStarted, ProgressPercentage = 0, TargetDate = DateTime.Now.AddDays(90), EstimatedCost = 120000, ExpectedRiskReduction = 50 },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Automate Application Workflow", NameAr = "أتمتة سير عمل الطلبات", DescriptionEn = "Automate manual steps in application processing", DescriptionAr = "أتمتة الخطوات اليدوية في معالجة الطلبات", RiskId = risks[2].Id, Priority = 2, Status = ESEMS.Web.Models.Enums.RiskActionPlanStatus.Completed, ProgressPercentage = 100, TargetDate = DateTime.Now.AddDays(-10), CompletionDate = DateTime.Now.AddDays(-12), EstimatedCost = 30000, ActualCost = 28000, ExpectedRiskReduction = 35 }
        };
    }

    private static List<SLABreach> CreateSLABreaches(List<SLADefinition> slaDefinitions, List<Incident> incidents)
    {
        return new List<SLABreach>
        {
            new() { Id = Guid.NewGuid().ToString(), BreachNumber = "SLAB-2024-001", NameEn = "Grant Application SLA Breach", NameAr = "انتهاك اتفاقية طلب المنحة", DescriptionEn = "Housing grant processing exceeded 120-hour target", DescriptionAr = "تجاوزت معالجة المنحة السكنية هدف 120 ساعة", SLADefinitionId = slaDefinitions[0].Id, BreachDate = DateTime.Now.AddDays(-25), TargetValue = 120, ActualValue = 156, Variance = 36, VariancePercentage = 30, Severity = 2, RootCause = "Staff shortage during holiday period", CorrectiveAction = "Assigned additional resources", IsResolved = true, ResolvedDate = DateTime.Now.AddDays(-20) },
            new() { Id = Guid.NewGuid().ToString(), BreachNumber = "SLAB-2024-002", NameEn = "Inquiry Response SLA Breach", NameAr = "انتهاك اتفاقية الاستجابة", DescriptionEn = "Customer inquiry first response exceeded 1 hour", DescriptionAr = "تجاوزت الاستجابة الأولى لاستفسار العميل ساعة واحدة", SLADefinitionId = slaDefinitions[3].Id, IncidentId = incidents[3].Id, BreachDate = DateTime.Now.AddDays(-3), TargetValue = 1, ActualValue = 2.5m, Variance = 1.5m, VariancePercentage = 150, Severity = 1, RootCause = "High volume of inquiries", IsResolved = false },
            new() { Id = Guid.NewGuid().ToString(), BreachNumber = "SLAB-2024-003", NameEn = "Maintenance Request SLA Breach", NameAr = "انتهاك اتفاقية طلب الصيانة", DescriptionEn = "Maintenance request resolution exceeded 72-hour target", DescriptionAr = "تجاوز حل طلب الصيانة هدف 72 ساعة", SLADefinitionId = slaDefinitions[2].Id, BreachDate = DateTime.Now.AddDays(-15), TargetValue = 72, ActualValue = 96, Variance = 24, VariancePercentage = 33.3m, Severity = 3, RootCause = "Waiting for spare parts", CorrectiveAction = "Expedited parts delivery", PreventiveAction = "Maintain spare parts inventory", IsResolved = true, ResolvedDate = DateTime.Now.AddDays(-12), FinancialImpact = 2500 }
        };
    }

    private static List<IncidentComment> CreateIncidentComments(List<Incident> incidents)
    {
        return new List<IncidentComment>
        {
            new() { Id = Guid.NewGuid().ToString(), IncidentId = incidents[0].Id, Comment = "Authentication service restarted. Monitoring for recurrence.", CreatedAt = DateTime.Now.AddDays(-10).AddHours(1), IsInternal = true },
            new() { Id = Guid.NewGuid().ToString(), IncidentId = incidents[0].Id, Comment = "Root cause identified as memory leak in auth module. Patch applied.", CreatedAt = DateTime.Now.AddDays(-10).AddHours(2), IsInternal = true },
            new() { Id = Guid.NewGuid().ToString(), IncidentId = incidents[1].Id, Comment = "Database team investigating slow query performance. Estimated fix in 4 hours.", CreatedAt = DateTime.Now.AddDays(-1), IsInternal = false }
        };
    }

    private static List<ProblemComment> CreateProblemComments(List<Problem> problems)
    {
        return new List<ProblemComment>
        {
            new() { Id = Guid.NewGuid().ToString(), ProblemId = problems[0].Id, Comment = "Memory profiling shows gradual increase in heap usage over 6 hours.", CreatedAt = DateTime.Now.AddDays(-14), IsInternal = true },
            new() { Id = Guid.NewGuid().ToString(), ProblemId = problems[0].Id, Comment = "Vendor confirmed bug in authentication module v2.3. Patch v2.3.1 available.", CreatedAt = DateTime.Now.AddDays(-12), IsInternal = false },
            new() { Id = Guid.NewGuid().ToString(), ProblemId = problems[1].Id, Comment = "Query execution plans collected. Missing indexes identified on 3 tables.", CreatedAt = DateTime.Now.AddDays(-6), IsInternal = true }
        };
    }

    private static List<ChangeRequestComment> CreateChangeRequestComments(List<ChangeRequest> changeRequests)
    {
        return new List<ChangeRequestComment>
        {
            new() { Id = Guid.NewGuid().ToString(), ChangeRequestId = changeRequests[0].Id, Comment = "SMS provider selected. Integration development starting next week.", CreatedAt = DateTime.Now.AddDays(-5) },
            new() { Id = Guid.NewGuid().ToString(), ChangeRequestId = changeRequests[1].Id, Comment = "Integration with government databases completed. Testing phase started.", CreatedAt = DateTime.Now.AddDays(-20) },
            new() { Id = Guid.NewGuid().ToString(), ChangeRequestId = changeRequests[2].Id, Comment = "Video call vendor evaluation in progress. Three vendors shortlisted.", CreatedAt = DateTime.Now.AddDays(-3) }
        };
    }

    private static List<AssetRisk> CreateAssetRisks(List<Asset> assets, List<EnterpriseRisk> risks)
    {
        return new List<AssetRisk>
        {
            new() { Id = Guid.NewGuid().ToString(), AssetId = assets[0].Id, RiskId = risks[0].Id, ImpactLevel = 4, SpecificControls = "Daily backups, monitoring alerts", Notes = "Database server critical for HMS uptime" },
            new() { Id = Guid.NewGuid().ToString(), AssetId = assets[0].Id, RiskId = risks[1].Id, ImpactLevel = 5, SpecificControls = "Encryption at rest, access controls, audit logging", Notes = "Contains sensitive customer data" },
            new() { Id = Guid.NewGuid().ToString(), AssetId = assets[7].Id, RiskId = risks[1].Id, ImpactLevel = 4, SpecificControls = "Regular firmware updates, rule audits", Notes = "Perimeter security device" }
        };
    }

    private static List<ServiceAsset> CreateServiceAssets(List<Service> services, List<Asset> assets)
    {
        return new List<ServiceAsset>
        {
            new() { Id = Guid.NewGuid().ToString(), ServiceId = services[0].Id, AssetId = assets[0].Id, Criticality = 1, IsRequired = true, UsageDescription = "Primary database for housing applications" },
            new() { Id = Guid.NewGuid().ToString(), ServiceId = services[0].Id, AssetId = assets[1].Id, Criticality = 1, IsRequired = true, UsageDescription = "Application server hosting customer portal" },
            new() { Id = Guid.NewGuid().ToString(), ServiceId = services[0].Id, AssetId = assets[3].Id, Criticality = 1, IsRequired = true, UsageDescription = "Core HMS software license" }
        };
    }

    private static List<ServiceRisk> CreateServiceRisks(List<Service> services, List<EnterpriseRisk> risks)
    {
        return new List<ServiceRisk>
        {
            new() { Id = Guid.NewGuid().ToString(), ServiceId = services[0].Id, RiskId = risks[0].Id, ImpactLevel = 4, SpecificControls = "HA cluster, automated failover" },
            new() { Id = Guid.NewGuid().ToString(), ServiceId = services[0].Id, RiskId = risks[2].Id, ImpactLevel = 3, SpecificControls = "SLA monitoring dashboard, auto-escalation" },
            new() { Id = Guid.NewGuid().ToString(), ServiceId = services[3].Id, RiskId = risks[4].Id, ImpactLevel = 3, SpecificControls = "Monthly budget reviews, approval gates" }
        };
    }

    private static List<ChangeRequestAsset> CreateChangeRequestAssets(List<ChangeRequest> changeRequests, List<Asset> assets)
    {
        return new List<ChangeRequestAsset>
        {
            new() { Id = Guid.NewGuid().ToString(), ChangeRequestId = changeRequests[0].Id, AssetId = assets[1].Id, ImpactType = "Modify", ImpactDescription = "Application server config update for SMS integration", IsCritical = false },
            new() { Id = Guid.NewGuid().ToString(), ChangeRequestId = changeRequests[1].Id, AssetId = assets[3].Id, ImpactType = "Modify", ImpactDescription = "HMS software update for government DB integration", IsCritical = true },
            new() { Id = Guid.NewGuid().ToString(), ChangeRequestId = changeRequests[5].Id, AssetId = assets[2].Id, ImpactType = "Modify", ImpactDescription = "Network configuration for new payment gateway", IsCritical = false }
        };
    }

    private static List<ChangeRequestRisk> CreateChangeRequestRisks(List<ChangeRequest> changeRequests, List<EnterpriseRisk> risks)
    {
        return new List<ChangeRequestRisk>
        {
            new() { Id = Guid.NewGuid().ToString(), ChangeRequestId = changeRequests[0].Id, RiskId = risks[0].Id, RelationshipType = "Mitigates", ImpactDescription = "SMS notifications reduce missed SLA risk", ExpectedRiskChange = "Decrease" },
            new() { Id = Guid.NewGuid().ToString(), ChangeRequestId = changeRequests[1].Id, RiskId = risks[2].Id, RelationshipType = "Mitigates", ImpactDescription = "Fewer documents reduces processing delays", ExpectedRiskChange = "Decrease" },
            new() { Id = Guid.NewGuid().ToString(), ChangeRequestId = changeRequests[3].Id, RiskId = risks[3].Id, RelationshipType = "Mitigates", ImpactDescription = "Automated verification improves compliance", ExpectedRiskChange = "Decrease" }
        };
    }

    private static List<ProcessRisk> CreateProcessRisks(List<Process> processes, List<EnterpriseRisk> risks)
    {
        return new List<ProcessRisk>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Application Data Loss", NameAr = "فقدان بيانات الطلب", DescriptionEn = "Risk of losing application data during processing", DescriptionAr = "خطر فقدان بيانات الطلب أثناء المعالجة", ProcessId = processes.First(p => p.Code == "1.1.1").Id, Code = "PR-001", Category = "Data", EnterpriseRiskId = risks[0].Id, LikelihoodScore = 2, ImpactScore = 4, MitigationStrategy = "Automated backups every 15 minutes" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Processing Bottleneck", NameAr = "اختناق المعالجة", DescriptionEn = "Risk of processing delays during peak periods", DescriptionAr = "خطر تأخر المعالجة خلال فترات الذروة", ProcessId = processes.First(p => p.Code == "1.1.2").Id, Code = "PR-002", Category = "Operational", EnterpriseRiskId = risks[2].Id, LikelihoodScore = 3, ImpactScore = 3, MitigationStrategy = "Auto-scaling and load balancing" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Unauthorized Access", NameAr = "وصول غير مصرح به", DescriptionEn = "Risk of unauthorized access to customer records", DescriptionAr = "خطر الوصول غير المصرح به لسجلات العملاء", ProcessId = processes.First(p => p.Code == "1.1.3").Id, Code = "PR-003", Category = "Security", EnterpriseRiskId = risks[1].Id, LikelihoodScore = 2, ImpactScore = 5, MitigationStrategy = "Role-based access control, audit logging" }
        };
    }

    private static List<ProcessMeasurement> CreateProcessMeasurements(List<Process> processes)
    {
        return new List<ProcessMeasurement>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Application Processing Time", NameAr = "وقت معالجة الطلب", DescriptionEn = "Average time to process a housing application", DescriptionAr = "متوسط وقت معالجة طلب الإسكان", ProcessId = processes.First(p => p.Code == "1.1.1").Id, Code = "PM-001", UnitOfMeasure = "Hours", TargetValue = 48, ActualValue = 52, Frequency = "Weekly", DataSource = "HMS System Reports" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Document Verification Accuracy", NameAr = "دقة التحقق من المستندات", DescriptionEn = "Percentage of correctly verified documents", DescriptionAr = "نسبة المستندات التي تم التحقق منها بشكل صحيح", ProcessId = processes.First(p => p.Code == "1.1.2").Id, Code = "PM-002", UnitOfMeasure = "%", TargetValue = 99, ActualValue = 97.5m, Frequency = "Monthly", DataSource = "Quality Audit Reports" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Customer Satisfaction Score", NameAr = "درجة رضا العملاء", DescriptionEn = "Customer satisfaction for application process", DescriptionAr = "رضا العملاء عن عملية التقديم", ProcessId = processes.First(p => p.Code == "1.1.3").Id, Code = "PM-003", UnitOfMeasure = "%", TargetValue = 95, ActualValue = 92, Frequency = "Monthly", DataSource = "Customer Feedback System" }
        };
    }

    private static List<ServiceMeasurement> CreateServiceMeasurements(List<Service> services)
    {
        return new List<ServiceMeasurement>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Service Availability", NameAr = "توفر الخدمة", DescriptionEn = "Housing portal uptime percentage", DescriptionAr = "نسبة تشغيل بوابة الإسكان", ServiceId = services[0].Id, Code = "SM-001", UnitOfMeasure = "%", TargetValue = 99.9m, ActualValue = 99.7m, Frequency = "Daily", DataSource = "Infrastructure Monitoring" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Average Response Time", NameAr = "متوسط وقت الاستجابة", DescriptionEn = "Average page load time for customer portal", DescriptionAr = "متوسط وقت تحميل الصفحة لبوابة العملاء", ServiceId = services[0].Id, Code = "SM-002", UnitOfMeasure = "Seconds", TargetValue = 2, ActualValue = 1.8m, Frequency = "Daily", DataSource = "APM Tool" },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "First Contact Resolution", NameAr = "الحل من أول اتصال", DescriptionEn = "Percentage of inquiries resolved at first contact", DescriptionAr = "نسبة الاستفسارات المحلولة من أول اتصال", ServiceId = services[4].Id, Code = "SM-003", UnitOfMeasure = "%", TargetValue = 80, ActualValue = 75, Frequency = "Weekly", DataSource = "CRM Reports" }
        };
    }

    private static List<ProcessRaci> CreateProcessRacis(List<Process> processes, List<OrganizationUnit> orgUnits)
    {
        return new List<ProcessRaci>
        {
            new() { Id = Guid.NewGuid().ToString(), ProcessId = processes.First(p => p.Code == "1.1.1").Id, OrganizationUnitId = orgUnits[1].Id, Role = RACIRole.Responsible, Notes = "Handles day-to-day application reception" },
            new() { Id = Guid.NewGuid().ToString(), ProcessId = processes.First(p => p.Code == "1.1.1").Id, OrganizationUnitId = orgUnits[0].Id, Role = RACIRole.Accountable, Notes = "Overall accountability for application process" },
            new() { Id = Guid.NewGuid().ToString(), ProcessId = processes.First(p => p.Code == "1.1.2").Id, OrganizationUnitId = orgUnits[2].Id, Role = RACIRole.Consulted, Notes = "Provides technical verification expertise" }
        };
    }

    private static List<ActivityRaci> CreateActivityRacis(List<Activity> activities, List<OrganizationUnit> orgUnits)
    {
        return new List<ActivityRaci>
        {
            new() { Id = Guid.NewGuid().ToString(), ActivityId = activities[0].Id, OrganizationUnitId = orgUnits[1].Id, Role = RACIRole.Responsible, Notes = "Executes the activity" },
            new() { Id = Guid.NewGuid().ToString(), ActivityId = activities[1].Id, OrganizationUnitId = orgUnits[2].Id, Role = RACIRole.Accountable, Notes = "Approves activity output" },
            new() { Id = Guid.NewGuid().ToString(), ActivityId = activities[2].Id, OrganizationUnitId = orgUnits[3].Id, Role = RACIRole.Informed, Notes = "Receives activity status updates" }
        };
    }

    private static List<TaskRaci> CreateTaskRacis(List<ProcessTask> tasks, List<OrganizationUnit> orgUnits)
    {
        return new List<TaskRaci>
        {
            new() { Id = Guid.NewGuid().ToString(), TaskId = tasks[0].Id, OrganizationUnitId = orgUnits[1].Id, Role = RACIRole.Responsible, Notes = "Performs the task" },
            new() { Id = Guid.NewGuid().ToString(), TaskId = tasks[1].Id, OrganizationUnitId = orgUnits[0].Id, Role = RACIRole.Accountable, Notes = "Signs off on task completion" },
            new() { Id = Guid.NewGuid().ToString(), TaskId = tasks[2].Id, OrganizationUnitId = orgUnits[2].Id, Role = RACIRole.Consulted, Notes = "Provides input before task execution" }
        };
    }

    private static List<ImprovementAction> CreateImprovementActions(List<ImprovementInitiative> improvements)
    {
        return new List<ImprovementAction>
        {
            new() { Id = Guid.NewGuid().ToString(), Code = "IA-001", NameEn = "Implement Online Application Portal", NameAr = "تنفيذ بوابة التقديم الإلكتروني", DescriptionEn = "Develop and deploy an online portal for housing grant applications", DescriptionAr = "تطوير ونشر بوابة إلكترونية لطلبات منح الإسكان", ImprovementId = improvements[0].Id, Priority = 1, Status = ImprovementActionStatus.InProgress, DueDate = DateTime.Now.AddDays(60), CompletionPercentage = 45, DisplayOrder = 1 },
            new() { Id = Guid.NewGuid().ToString(), Code = "IA-002", NameEn = "Train Staff on New Process", NameAr = "تدريب الموظفين على العملية الجديدة", DescriptionEn = "Conduct training sessions for all staff involved in the new process", DescriptionAr = "إجراء دورات تدريبية لجميع الموظفين المشاركين في العملية الجديدة", ImprovementId = improvements[0].Id, Priority = 2, Status = ImprovementActionStatus.Pending, DueDate = DateTime.Now.AddDays(90), CompletionPercentage = 0, DisplayOrder = 2 },
            new() { Id = Guid.NewGuid().ToString(), Code = "IA-003", NameEn = "Deploy Customer Feedback Module", NameAr = "نشر وحدة ملاحظات العملاء", DescriptionEn = "Integrate automated customer feedback collection into the service", DescriptionAr = "دمج جمع ملاحظات العملاء الآلي في الخدمة", ImprovementId = improvements[1].Id, Priority = 1, Status = ImprovementActionStatus.Completed, DueDate = DateTime.Now.AddDays(-5), CompletedDate = DateTime.Now.AddDays(-7), CompletionPercentage = 100, DisplayOrder = 1 }
        };
    }

    private static List<ImprovementMeasurement> CreateImprovementMeasurements(List<ImprovementInitiative> improvements)
    {
        return new List<ImprovementMeasurement>
        {
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Customer Satisfaction Index", NameAr = "مؤشر رضا العملاء", DescriptionEn = "Measure customer satisfaction with housing services", DescriptionAr = "قياس رضا العملاء عن خدمات الإسكان", ImprovementId = improvements[0].Id, MeasurementType = ImprovementMeasurementType.Satisfaction, UnitOfMeasure = "%", TargetValue = 95, AsIsValue = 82, ToBeValue = 95, Weight = 40, DisplayOrder = 1, IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Processing Time Reduction", NameAr = "تقليل وقت المعالجة", DescriptionEn = "Reduce average application processing time", DescriptionAr = "تقليل متوسط وقت معالجة الطلب", ImprovementId = improvements[0].Id, MeasurementType = ImprovementMeasurementType.Time, UnitOfMeasure = "Hours", TargetValue = 24, AsIsValue = 72, ToBeValue = 24, Weight = 35, DisplayOrder = 2, IsActive = true },
            new() { Id = Guid.NewGuid().ToString(), NameEn = "Cost Per Transaction", NameAr = "التكلفة لكل معاملة", DescriptionEn = "Reduce cost per housing application transaction", DescriptionAr = "تقليل التكلفة لكل معاملة طلب إسكان", ImprovementId = improvements[1].Id, MeasurementType = ImprovementMeasurementType.Cost, UnitOfMeasure = "AED", TargetValue = 150, AsIsValue = 350, ToBeValue = 150, Weight = 25, DisplayOrder = 1, IsActive = true }
        };
    }

    private static List<ProcessBpmnVersion> CreateProcessBpmnVersions(List<Process> processes)
    {
        var proc = processes.First(p => p.Code == "1.1.1");
        var bpmnSample = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><bpmn:definitions xmlns:bpmn=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"><bpmn:process id=\"Process_1\"><bpmn:startEvent id=\"Start\" /><bpmn:endEvent id=\"End\" /></bpmn:process></bpmn:definitions>";
        return new List<ProcessBpmnVersion>
        {
            new() { Id = Guid.NewGuid().ToString(), ProcessId = proc.Id, VersionNumber = 1, BpmnXml = bpmnSample, ChangeDescription = "Initial BPMN diagram creation", CreatedByName = "System Admin", CreatedAt = DateTime.Now.AddDays(-90), IsCurrent = false, XmlSizeBytes = bpmnSample.Length },
            new() { Id = Guid.NewGuid().ToString(), ProcessId = proc.Id, VersionNumber = 2, BpmnXml = bpmnSample, ChangeDescription = "Added parallel gateway for document verification", CreatedByName = "System Admin", CreatedAt = DateTime.Now.AddDays(-45), IsCurrent = false, XmlSizeBytes = bpmnSample.Length },
            new() { Id = Guid.NewGuid().ToString(), ProcessId = proc.Id, VersionNumber = 3, BpmnXml = bpmnSample, ChangeDescription = "Optimized approval workflow with automated notifications", CreatedByName = "System Admin", CreatedAt = DateTime.Now.AddDays(-10), IsCurrent = true, XmlSizeBytes = bpmnSample.Length }
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Draft7/Draft8 Seeding Methods
    // ═══════════════════════════════════════════════════════════════════

    private static List<ProcessMaturityAssessment> CreateProcessMaturityAssessments()
    {
        return new List<ProcessMaturityAssessment>
        {
            new() { Id = Guid.NewGuid().ToString(), AssessmentYear = 2020, StrategicAlignment = 3.4m, Governance = 3.3m, ProcessModels = 2.6m, ChangeManagement = 2.3m, ProcessPerformance = 2.5m, ProcessImprovement = 3.5m, ToolsAndTechnology = 2.7m, OverallScore = 2.9m, AssessmentDate = new DateTime(2020, 12, 31), Notes = "Baseline APQC Process Maturity Assessment" },
            new() { Id = Guid.NewGuid().ToString(), AssessmentYear = 2023, StrategicAlignment = 4.8m, Governance = 4.6m, ProcessModels = 4.7m, ChangeManagement = 4.2m, ProcessPerformance = 4.6m, ProcessImprovement = 4.8m, ToolsAndTechnology = 4.5m, OverallScore = 4.6m, AssessmentDate = new DateTime(2023, 12, 31), Notes = "Post-transformation APQC Process Maturity Assessment - Dubai Government Excellence Cycle 2023/2024" }
        };
    }

    private static List<ServiceAssessment> CreateServiceAssessments(List<Service> services)
    {
        // 360 Service Assessment data from Draft8 - Before/After for 12 services × 7 criteria
        // Automation: 0=Not Auto, 1=Semi Auto, 2=Fully Auto
        // SelfService: 0=Not Self, 1=Partial Self, 2=Fully Self
        // DataIntegration: 0=Internal, 1=Partial, 2=Full
        // Proactivity: 0=N/A, 1=Level 1, 2=Level 2, 3=Level 3
        // IntegratedServices: 0=Partial, 1=Full
        // NoPhysicalAttendance: 0=Visit Req., 1=No Visit
        // UnifiedChannels: 0=Entity Channel, 1=Unified Channel
        var assessments = new List<ServiceAssessment>();
        var svcList = services.OrderBy(s => s.Code).ToList();

        // Service code → (Before: Auto, Self, DataInt, Proact, IntSvc, NoVisit, UnifCh), (After: same)
        var data = new (string code, int[] before, int[] after)[] {
            ("SVC-001", new[]{1,1,0,0,0,0,0}, new[]{2,2,2,2,1,1,1}),   // Housing Loans
            ("SVC-002", new[]{1,0,1,2,1,0,1}, new[]{2,2,2,3,1,1,1}),   // Housing Grants
            ("SVC-003", new[]{0,0,0,0,0,0,0}, new[]{2,2,2,0,1,1,1}),   // Residential Lot
            ("SVC-004", new[]{1,1,1,1,1,0,1}, new[]{2,2,2,2,1,1,1}),   // Government Housing
            ("SVC-005", new[]{0,0,0,0,1,0,0}, new[]{2,2,2,0,1,1,1}),   // Maintenance Request
            ("SVC-006", new[]{1,1,2,1,0,0,1}, new[]{2,2,2,2,1,1,1}),   // Housing Expansion
            ("SVC-007", new[]{1,0,0,0,1,0,0}, new[]{2,2,2,0,1,1,1}),   // Housing Exchange
            ("SVC-008", new[]{0,0,2,1,0,0,1}, new[]{2,2,2,2,1,1,1}),   // Loan Rescheduling
            ("SVC-009", new[]{1,2,2,2,1,1,1}, new[]{2,2,2,3,1,1,1}),   // Certificate Request
            ("SVC-010", new[]{0,1,0,0,1,0,0}, new[]{2,2,2,0,1,1,1}),   // Early Settlement
            ("SVC-011", new[]{1,1,1,1,0,0,1}, new[]{2,2,2,2,1,1,1}),   // Housing Assistance
            ("SVC-012", new[]{0,0,2,0,1,0,1}, new[]{2,2,2,0,1,1,1})    // Construction Permit
        };

        foreach (var (code, before, after) in data)
        {
            var svc = svcList.FirstOrDefault(s => s.Code == code);
            if (svc == null) continue;
            assessments.Add(new ServiceAssessment { ServiceId = svc.Id, Period = "Before", Automation = before[0], SelfService = before[1], DataIntegration = before[2], Proactivity = before[3], IntegratedServices = before[4], NoPhysicalAttendance = before[5], UnifiedChannels = before[6], AssessmentDate = new DateTime(2022, 1, 1), Notes = "Before transformation" });
            assessments.Add(new ServiceAssessment { ServiceId = svc.Id, Period = "After", Automation = after[0], SelfService = after[1], DataIntegration = after[2], Proactivity = after[3], IntegratedServices = after[4], NoPhysicalAttendance = after[5], UnifiedChannels = after[6], AssessmentDate = new DateTime(2024, 12, 1), Notes = "After transformation - all targets achieved" });
        }
        return assessments;
    }

    private static List<KPITrend> CreateKPITrends()
    {
        return new List<KPITrend>
        {
            // Customer Happiness Index (Draft7 data: 82.5% → 97.7%)
            new() { Category = "Happiness", KPIName = "Customer Happiness Index", Year = 2018, Value = 82.5m, Unit = "%" },
            new() { Category = "Happiness", KPIName = "Customer Happiness Index", Year = 2019, Value = 85.3m, Unit = "%" },
            new() { Category = "Happiness", KPIName = "Customer Happiness Index", Year = 2020, Value = 88.1m, Unit = "%" },
            new() { Category = "Happiness", KPIName = "Customer Happiness Index", Year = 2021, Value = 91.2m, Unit = "%" },
            new() { Category = "Happiness", KPIName = "Customer Happiness Index", Year = 2022, Value = 94.5m, Unit = "%" },
            new() { Category = "Happiness", KPIName = "Customer Happiness Index", Year = 2023, Value = 97.7m, Unit = "%" },
            new() { Category = "Happiness", KPIName = "Customer Happiness Index", Year = 2024, Value = 98.75m, Unit = "%" },
            // Employee Happiness Index (Draft7 data: 83.8% → 94.9%)
            new() { Category = "Happiness", KPIName = "Employee Happiness Index", Year = 2018, Value = 83.8m, Unit = "%" },
            new() { Category = "Happiness", KPIName = "Employee Happiness Index", Year = 2019, Value = 85.2m, Unit = "%" },
            new() { Category = "Happiness", KPIName = "Employee Happiness Index", Year = 2020, Value = 87.5m, Unit = "%" },
            new() { Category = "Happiness", KPIName = "Employee Happiness Index", Year = 2021, Value = 89.8m, Unit = "%" },
            new() { Category = "Happiness", KPIName = "Employee Happiness Index", Year = 2022, Value = 92.1m, Unit = "%" },
            new() { Category = "Happiness", KPIName = "Employee Happiness Index", Year = 2023, Value = 94.9m, Unit = "%" },
            new() { Category = "Happiness", KPIName = "Employee Happiness Index", Year = 2024, Value = 95.2m, Unit = "%" },
            // Digital Adoption
            new() { Category = "Digital", KPIName = "Digital Adoption Rate", Year = 2020, Value = 65.0m, Unit = "%" },
            new() { Category = "Digital", KPIName = "Digital Adoption Rate", Year = 2021, Value = 78.0m, Unit = "%" },
            new() { Category = "Digital", KPIName = "Digital Adoption Rate", Year = 2022, Value = 88.0m, Unit = "%" },
            new() { Category = "Digital", KPIName = "Digital Adoption Rate", Year = 2023, Value = 94.8m, Unit = "%" },
            new() { Category = "Digital", KPIName = "Digital Adoption Rate", Year = 2024, Value = 95.2m, Unit = "%" },
            // Process Automation
            new() { Category = "Digital", KPIName = "Process Automation Rate", Year = 2020, Value = 40.0m, Unit = "%" },
            new() { Category = "Digital", KPIName = "Process Automation Rate", Year = 2021, Value = 55.0m, Unit = "%" },
            new() { Category = "Digital", KPIName = "Process Automation Rate", Year = 2022, Value = 75.0m, Unit = "%" },
            new() { Category = "Digital", KPIName = "Process Automation Rate", Year = 2023, Value = 95.0m, Unit = "%" },
            new() { Category = "Digital", KPIName = "Process Automation Rate", Year = 2024, Value = 100.0m, Unit = "%" },
            // Cost Savings
            new() { Category = "Financial", KPIName = "Cumulative Cost Savings", Year = 2020, Value = 12.0m, Unit = "AED Million" },
            new() { Category = "Financial", KPIName = "Cumulative Cost Savings", Year = 2021, Value = 22.0m, Unit = "AED Million" },
            new() { Category = "Financial", KPIName = "Cumulative Cost Savings", Year = 2022, Value = 35.0m, Unit = "AED Million" },
            new() { Category = "Financial", KPIName = "Cumulative Cost Savings", Year = 2023, Value = 45.0m, Unit = "AED Million" },
            new() { Category = "Financial", KPIName = "Cumulative Cost Savings", Year = 2024, Value = 53.0m, Unit = "AED Million" },
            // Process Maturity Overall
            new() { Category = "Process", KPIName = "Process Maturity Score", Year = 2020, Value = 2.9m, Unit = "/5", Notes = "APQC 7-Pillar Assessment" },
            new() { Category = "Process", KPIName = "Process Maturity Score", Year = 2023, Value = 4.6m, Unit = "/5", Notes = "APQC 7-Pillar Assessment" }
        };
    }

    private static List<ISOStandard> CreateISOStandards()
    {
        return new List<ISOStandard>
        {
            new() { StandardNumber = "ISO 9001:2015", Version = "2015", Domain = "Quality Management", NameEn = "Quality Management Systems", NameAr = "أنظمة إدارة الجودة", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 22301:2019", Version = "2019", Domain = "Business Continuity", NameEn = "Business Continuity Management", NameAr = "إدارة استمرارية الأعمال", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 10002:2018", Version = "2018", Domain = "Customer", NameEn = "Complaints Management", NameAr = "إدارة الشكاوى", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 27001:2013", Version = "2013", Domain = "Information Security", NameEn = "Information Security Management", NameAr = "إدارة أمن المعلومات", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 31000:2018", Version = "2018", Domain = "Risk Management", NameEn = "Risk Management", NameAr = "إدارة المخاطر", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 10004:2018", Version = "2018", Domain = "Customer", NameEn = "Customer Satisfaction Monitoring", NameAr = "مراقبة رضا العملاء", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 10001:2018", Version = "2018", Domain = "Customer", NameEn = "Customer Surveys", NameAr = "استطلاعات العملاء", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 37000:2021", Version = "2021", Domain = "Governance", NameEn = "Governance of Organizations", NameAr = "حوكمة المؤسسات", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 55001:2014", Version = "2014", Domain = "Asset Management", NameEn = "Asset Management", NameAr = "إدارة الأصول", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 45001:2018", Version = "2018", Domain = "HSE", NameEn = "Occupational Health and Safety", NameAr = "الصحة والسلامة المهنية", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 56002:2019", Version = "2019", Domain = "Innovation", NameEn = "Innovation Management", NameAr = "إدارة الابتكار", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 26000:2010", Version = "2010", Domain = "Social Responsibility", NameEn = "Social Responsibility", NameAr = "المسؤولية الاجتماعية", IsCompliant = true, CompliancePercentage = 95 },
            new() { StandardNumber = "ISO 38500:2015", Version = "2015", Domain = "IT Governance", NameEn = "Digital Transformation Governance", NameAr = "حوكمة التحول الرقمي", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 44001:2017", Version = "2017", Domain = "Collaboration", NameEn = "Collaborative Business Relationships", NameAr = "العلاقات التجارية التعاونية", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 21502:2020", Version = "2020", Domain = "Project Management", NameEn = "Project Management Guidance", NameAr = "إرشادات إدارة المشاريع", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 30401:2018", Version = "2018", Domain = "Knowledge", NameEn = "Knowledge Management Systems", NameAr = "أنظمة إدارة المعرفة", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 15392:2019", Version = "2019", Domain = "Sustainability", NameEn = "Sustainability in Buildings", NameAr = "الاستدامة في المباني", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 23593:2021", Version = "2021", Domain = "Service Excellence", NameEn = "Service Excellence Principles and Model", NameAr = "مبادئ ونموذج التميز في الخدمة", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 25010:2011", Version = "2011", Domain = "IT", NameEn = "Systems and Software Engineering", NameAr = "هندسة الأنظمة والبرمجيات", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 20000-1:2018", Version = "2018", Domain = "IT Service", NameEn = "Information Technology Service Management", NameAr = "إدارة خدمات تكنولوجيا المعلومات", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 20400:2017", Version = "2017", Domain = "Procurement", NameEn = "Sustainable Procurement", NameAr = "المشتريات المستدامة", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 8000-1:2022", Version = "2022", Domain = "Data Quality", NameEn = "Data Quality", NameAr = "جودة البيانات", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 18295-1:2017", Version = "2017", Domain = "Customer", NameEn = "Customer Contact Center", NameAr = "مركز اتصال العملاء", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 20121:2012", Version = "2012", Domain = "Events", NameEn = "Event Sustainability Management", NameAr = "إدارة استدامة الفعاليات", IsCompliant = true, CompliancePercentage = 90 },
            new() { StandardNumber = "ISO 32310:2022", Version = "2022", Domain = "Sustainability", NameEn = "Financial Sustainability", NameAr = "الاستدامة المالية", IsCompliant = true, CompliancePercentage = 100 },
            new() { StandardNumber = "ISO 21542:2021", Version = "2021", Domain = "Accessibility", NameEn = "Building Accessibility and Usability", NameAr = "إمكانية الوصول وقابلية استخدام المباني", IsCompliant = true, CompliancePercentage = 100 }
        };
    }
}
