# Role x Controller Action Matrix

**Generated:** 2026-05-06 from controller [Authorize(Policy=...)] annotations.
**Coverage:** 39 controllers, 359 actions.

## How to read this

Each row is one MVC action. The Policy column is the named
authorization policy required to reach that action. A user without
the underlying permission gets a 403 Forbidden at the framework
layer -- never a 200 -- so the audit trail of role X tried URL Y
is enforced server-side, not just hidden in the menu.

## Unprotected actions (70)

These actions fall through to either bare [Authorize] (authenticated-only)
or no class-level attribute. AI + AIBpmnRead + BpmnImport are admin-curated.
Account is correctly anonymous for Login/Logout. Verify any non-Account hit
below has either an intentional [AllowAnonymous] or a class-level [Authorize]
you can confirm covers it.

- AIBpmnRead.GetProcessBpmn [GET]
- AIBpmnRead.GetProcesses [GET]
- AIBpmnRead.GetProcessesWithBPMN [GET]
- AIBpmnRead.LoadProcessBPMN [GET]
- AIBpmnRead.GetProcessTasksWithBPMN [GET]
- AIBpmnRead.LoadProcessTaskBPMN [GET]
- AI.Diagrams [GET]
- AI.ProcessAnalyzer [GET]
- AI.AnalyzeProcessGroup [POST]
- AI.AnalyzeProcess [POST]
- AI.OptimizeProcess [POST]
- AI.GenerateProcessImprovements [POST]
- AI.AnalyzeRisk [POST]
- AI.AnalyzeEnterpriseRisk [POST]
- AI.SummarizeAuditLogs [POST]
- AI.GenerateRACISuggestions [POST]
- AI.AnalyzeServicePerformance [POST]
- AI.AnalyzeIncident [POST]
- AI.AnalyzeProblem [POST]
- AI.AnalyzeChangeRequest [POST]
- AI.AnalyzeCustomerFeedback [POST]
- AI.AnalyzeImprovement [POST]
- AI.GenerateBPMN [POST]
- AI.RefineBPMN [POST]
- AI.OptimizePrompt [POST]
- AI.ImportVisio [POST]
- AI.AskAssistant [POST]
- AI.SaveBPMNToProcess [POST]
- AI.ImportBpmnFromFiles [POST]
- Account.Login [GET]
- Account.Login [POST]
- Account.WindowsLogin [GET]
- Account.Logout [POST]
- Account.AccessDenied [GET]
- Account.Profile [GET]
- Help.View [GET]
- Home.Index [GET]
- Home.Privacy [GET]
- Home.UserManual [GET]
- Home.Error [GET]
- Home.StatusCode [GET]
- MySpace.View [GET]
- Export.ExportProcessesToExcel [GET]
- Export.ExportProcessesToPdf [GET]
- Export.ExportServicesToExcel [GET]
- Export.ExportServicesToPdf [GET]
- Export.ExportRisksToExcel [GET]
- Export.ExportRisksToPdf [GET]
- Export.ExportIncidentsToExcel [GET]
- Export.ExportImprovementsToExcel [GET]
- ... +20 more

## Per-controller matrix

### AIBpmnRead

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | GetProcessBpmn | `(unprotected)` |
| GET | GetProcesses | `(unprotected)` |
| GET | GetProcessesWithBPMN | `(unprotected)` |
| GET | LoadProcessBPMN | `(unprotected)` |
| GET | GetProcessTasksWithBPMN | `(unprotected)` |
| GET | LoadProcessTaskBPMN | `(unprotected)` |

### AI

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Diagrams | `(unprotected)` |
| GET | ProcessAnalyzer | `(unprotected)` |
| POST | AnalyzeProcessGroup | `(unprotected)` |
| POST | AnalyzeProcess | `(unprotected)` |
| POST | OptimizeProcess | `(unprotected)` |
| POST | GenerateProcessImprovements | `(unprotected)` |
| POST | AnalyzeRisk | `(unprotected)` |
| POST | AnalyzeEnterpriseRisk | `(unprotected)` |
| POST | SummarizeAuditLogs | `(unprotected)` |
| POST | GenerateRACISuggestions | `(unprotected)` |
| POST | AnalyzeServicePerformance | `(unprotected)` |
| POST | AnalyzeIncident | `(unprotected)` |
| POST | AnalyzeProblem | `(unprotected)` |
| POST | AnalyzeChangeRequest | `(unprotected)` |
| POST | AnalyzeCustomerFeedback | `(unprotected)` |
| POST | AnalyzeImprovement | `(unprotected)` |
| POST | GenerateBPMN | `(unprotected)` |
| POST | RefineBPMN | `(unprotected)` |
| POST | OptimizePrompt | `(unprotected)` |
| POST | ImportVisio | `(unprotected)` |
| POST | AskAssistant | `(unprotected)` |
| POST | SaveBPMNToProcess | `(unprotected)` |
| POST | ImportBpmnFromFiles | `(unprotected)` |

### Account

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Login | `(unprotected)` |
| POST | Login | `(unprotected)` |
| GET | WindowsLogin | `(unprotected)` |
| POST | Logout | `(unprotected)` |
| GET | AccessDenied | `(unprotected)` |
| GET | Profile | `(unprotected)` |

### Activities

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | RedirectToAction | `Module.Process.View` |
| GET | Create | `Module.Process.Create` |
| POST | Create | `Module.Process.Create` |
| GET | Edit | `Module.Process.Edit` |
| POST | Edit | `Module.Process.Edit` |
| POST | Delete | `Module.Process.Delete` |

### AssetCategories

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.Asset.View` |
| GET | Details | `Module.Asset.View` |
| GET | Create | `Module.Asset.Create` |
| POST | Create | `Module.Asset.Create` |
| GET | Edit | `Module.Asset.Edit` |
| POST | Edit | `Module.Asset.Edit` |
| POST | Delete | `Module.Asset.Delete` |

### Assets

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.Asset.View` |
| GET | Details | `Module.Asset.View` |
| GET | Create | `Module.Asset.Create` |
| POST | Create | `Module.Asset.Create` |
| GET | Edit | `Module.Asset.Edit` |
| POST | Edit | `Module.Asset.Edit` |
| GET | Dashboard | `Module.Asset.View` |
| POST | Delete | `Module.Asset.Delete` |
| POST | LinkRisk | `Module.Asset.Edit` |
| POST | UnlinkRisk | `Module.Asset.Edit` |
| POST | UpdateAssetRisk | `Module.Asset.Edit` |
| GET | GetAvailableRisks | `Module.Asset.View` |
| GET | GetAvailableAssets | `Module.Asset.View` |

### AuditLogs

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `CanAdmin` |

### BpmnImport

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | View | `CanAdmin` |

### Categories

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.Process.View` |
| GET | Details | `Module.Process.View` |
| GET | Create | `Module.Process.Create` |
| POST | Create | `Module.Process.Create` |
| GET | Edit | `Module.Process.Edit` |
| POST | Edit | `Module.Process.Edit` |
| POST | Delete | `CanAdmin` |

### ChangeRequests

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.ChangeRequest.View` |
| GET | Details | `Module.ChangeRequest.View` |
| GET | Create | `Module.ChangeRequest.Create` |
| POST | Create | `Module.ChangeRequest.Create` |
| GET | Edit | `Module.ChangeRequest.Edit` |
| POST | Edit | `Module.ChangeRequest.Edit` |
| POST | Approve | `Module.ChangeRequest.Approve` |
| POST | Reject | `Module.ChangeRequest.Approve` |

### CustomerFeedback

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.Service.View` |
| GET | Details | `Module.Service.View` |
| GET | Create | `Module.Service.Create` |
| POST | Create | `Module.Service.Create` |
| GET | Edit | `Module.Service.Edit` |
| POST | Edit | `Module.Service.Edit` |
| POST | Delete | `Module.Service.Delete` |
| POST | Respond | `Module.Service.Edit` |
| POST | Resolve | `Module.Service.Edit` |

### Dashboard

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.Reports.View` |
| GET | OrganizationView | `Module.Reports.View` |
| GET | ProcessArchitectureView | `Module.Reports.View` |
| GET | QuadrantAnalysis | `Module.Reports.View` |

### EnterpriseRisks

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.Risk.View` |
| GET | Details | `Module.Risk.View` |
| GET | Create | `Module.Risk.Create` |
| POST | Create | `Module.Risk.Create` |
| GET | Edit | `Module.Risk.Edit` |
| POST | Edit | `Module.Risk.Edit` |
| POST | Delete | `Module.Risk.Delete` |
| GET | Dashboard | `Module.Risk.View` |
| GET | HeatMap | `Module.Risk.View` |

### Help

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | View | `(unprotected)` |

### Home

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `(unprotected)` |
| GET | Privacy | `(unprotected)` |
| GET | UserManual | `(unprotected)` |
| GET | Error | `(unprotected)` |
| GET | StatusCode | `(unprotected)` |

### Import

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `CanAdmin` |
| GET | DownloadProcessesTemplate | `CanAdmin` |
| GET | DownloadServicesTemplate | `CanAdmin` |
| GET | DownloadAssetsTemplate | `CanAdmin` |
| GET | DownloadRisksTemplate | `CanAdmin` |
| GET | ExportAssets | `CanAdmin` |
| GET | ExportProcesses | `CanAdmin` |
| GET | ExportServices | `CanAdmin` |
| GET | ExportRisks | `CanAdmin` |
| POST | ImportOrganization | `(unprotected)` |
| POST | ImportProcesses | `(unprotected)` |
| POST | ImportAll | `(unprotected)` |
| POST | ImportProcedures | `(unprotected)` |
| POST | ImportVisioDiagrams | `(unprotected)` |
| POST | ImportSingleVisioDiagram | `(unprotected)` |
| POST | ImportConvertedBpmn | `(unprotected)` |
| GET | BpmnImportProgress | `(unprotected)` |
| POST | ImportExcelVisioBpmn | `(unprotected)` |

### Improvements

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Dashboard | `Module.Improvement.View` |
| GET | Index | `Module.Improvement.View` |
| GET | Details | `Module.Improvement.View` |
| GET | Create | `Module.Improvement.Create` |
| POST | Create | `Module.Improvement.Create` |
| GET | Edit | `Module.Improvement.Edit` |
| POST | Edit | `Module.Improvement.Edit` |
| GET | Kanban | `Module.Improvement.View` |
| POST | UpdateStatus | `Module.Improvement.Edit` |
| POST | Submit | `Module.Improvement.Edit` |
| POST | Approve | `Module.Improvement.Approve` |
| POST | Reject | `Module.Improvement.Approve` |
| POST | Return | `Module.Improvement.Approve` |
| GET | ExportXlsx | `Module.Improvement.Export` |
| GET | ImportTemplate | `Module.Improvement.Export` |
| POST | ImportXlsx | `Module.Improvement.Create` |
| GET | DgepExcellenceExport | `Module.Improvement.View` |
| POST | AddReview | `Module.Improvement.Edit` |
| POST | Close | `Module.Improvement.Edit` |
| POST | RecordReading | `Module.Improvement.Edit` |
| GET | MyPendingReadings | `Module.Improvement.View` |
| POST | SaveBatchReadings | `Module.Improvement.Edit` |
| GET | GetReadings | `Module.Improvement.View` |
| POST | Transition | `Module.Improvement.Edit` |
| GET | Wizard | `Module.Improvement.Create` |
| POST | SaveDraft | `Module.Improvement.Create` |
| GET | LoadDraft | `Module.Improvement.Create` |
| POST | DeleteDraft | `Module.Improvement.Create` |
| GET | MyDrafts | `Module.Improvement.Create` |
| POST | ClearMyDrafts | `Module.Improvement.Create` |
| POST | CreateFromWizard | `Module.Improvement.Create` |
| GET | Roadmap | `Module.Improvement.View` |
| GET | ComparisonData | `Module.Improvement.View` |
| POST | AddMeasurement | `Module.Improvement.Edit` |
| POST | UpdateMeasurement | `Module.Improvement.Edit` |
| POST | DeleteMeasurement | `Module.Improvement.Edit` |
| POST | AddTeamMember | `Module.Improvement.Edit` |
| POST | RemoveTeamMember | `Module.Improvement.Edit` |
| POST | LinkRisk | `Module.Improvement.Edit` |
| POST | UnlinkRisk | `Module.Improvement.Edit` |
| POST | UpdateImprovementRisk | `Module.Improvement.Edit` |
| GET | GetAvailableRisks | `Module.Improvement.View` |
| GET | GetLinkedRisks | `Module.Improvement.View` |

### Incidents

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.Incident.View` |
| GET | Details | `Module.Incident.View` |
| GET | Create | `Module.Incident.Create` |
| POST | Create | `Module.Incident.Create` |
| GET | Edit | `Module.Incident.Edit` |
| POST | Edit | `Module.Incident.Edit` |
| POST | Delete | `Module.Incident.Delete` |
| POST | Resolve | `Module.Incident.Edit` |
| POST | Close | `Module.Incident.Edit` |

### KpiLibrary

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.Improvement.Edit` |
| GET | Create | `Module.Improvement.Edit` |
| POST | Create | `Module.Improvement.Edit` |
| GET | Edit | `Module.Improvement.Edit` |
| POST | Edit | `Module.Improvement.Edit` |
| POST | Retire | `Module.Improvement.Edit` |
| POST | Restore | `Module.Improvement.Edit` |
| GET | Search | `Module.Improvement.Edit` |

### Maintenance

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | RedirectToAction | `Module.Asset.View` |
| GET | Schedules | `Module.Asset.View` |
| GET | Records | `Module.Asset.View` |
| GET | CreateSchedule | `Module.Asset.Create` |
| POST | CreateSchedule | `Module.Asset.Create` |
| GET | EditSchedule | `Module.Asset.Edit` |
| POST | EditSchedule | `Module.Asset.Edit` |
| GET | CreateRecord | `Module.Asset.Create` |
| POST | CreateRecord | `Module.Asset.Create` |
| POST | DeleteSchedule | `Module.Asset.Delete` |

### MySpace

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | View | `(unprotected)` |
| GET | List | `(unprotected)` |
| POST | Upload | `(unprotected)` |
| POST | UploadMultiple | `(unprotected)` |
| DELETE | Delete | `(unprotected)` |
| GET | Update | `(unprotected)` |
| GET | Download | `(unprotected)` |

### OrganizationUnits

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.OrganizationUnit.View` |
| GET | Details | `Module.OrganizationUnit.View` |
| GET | Create | `Module.OrganizationUnit.Create` |
| POST | Create | `Module.OrganizationUnit.Create` |
| GET | Edit | `Module.OrganizationUnit.Edit` |
| POST | Edit | `Module.OrganizationUnit.Edit` |
| POST | Delete | `CanAdmin` |

### Problems

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.Problem.View` |
| GET | Details | `Module.Problem.View` |
| GET | Create | `Module.Problem.Create` |
| POST | Create | `Module.Problem.Create` |
| GET | Edit | `Module.Problem.Edit` |
| POST | Edit | `Module.Problem.Edit` |
| POST | Delete | `Module.Problem.Delete` |
| GET | CreateFromIncident | `Module.Problem.Create` |
| POST | CreateFromIncident | `Module.Problem.Create` |

### ProcessGroups

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.Process.View` |
| GET | Details | `Module.Process.View` |
| GET | Create | `Module.Process.Create` |
| GET | NextCode | `Module.Process.Create` |
| POST | Create | `Module.Process.Create` |
| GET | Edit | `Module.Process.Edit` |
| POST | Edit | `Module.Process.Edit` |
| POST | Delete | `CanAdmin` |

### ProcessHierarchy

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.OrganizationUnit.View` |
| GET | GetUnitDetails | `Module.OrganizationUnit.View` |

### Processes

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Dashboard | `Module.Process.View` |
| GET | Index | `Module.Process.View` |
| GET | CheckBpmn | `Module.Process.View` |
| GET | Details | `Module.Process.View` |
| GET | Raci | `Module.Process.View` |
| POST | AssignRaci | `Module.Process.Edit` |
| POST | UpdateBpmnDiagram | `Module.Process.Edit` |
| GET | GetBpmnVersions | `Module.Process.View` |
| GET | GetBpmnVersion | `Module.Process.View` |
| POST | RestoreBpmnVersion | `Module.Process.Edit` |
| GET | Create | `Module.Process.Create` |
| POST | Create | `Module.Process.Create` |
| GET | Edit | `Module.Process.Edit` |
| POST | Edit | `Module.Process.Edit` |
| POST | LinkService | `Module.Process.Edit` |
| POST | UnlinkService | `Module.Process.Edit` |
| POST | UpdateProcessService | `Module.Process.Edit` |
| GET | GetAvailableServices | `Module.Process.View` |
| GET | Setup | `Module.Process.Edit` |
| POST | SetupSave | `Module.Process.Edit` |
| GET | NextCode | `Module.Process.Create` |

### RoleGroups

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `CanAdmin` |
| POST | Save | `CanAdmin` |
| POST | Delete | `CanAdmin` |
| POST | Duplicate | `CanAdmin` |

### Roles

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `CanAdmin` |
| GET | Details | `CanAdmin` |
| GET | Create | `CanAdmin` |
| POST | Create | `CanAdmin` |
| GET | Edit | `CanAdmin` |
| POST | Edit | `CanAdmin` |
| POST | Delete | `CanAdmin` |

### SLA

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.Service.View` |
| GET | Details | `Module.Service.View` |
| GET | Create | `Module.Service.Create` |
| POST | Create | `Module.Service.Create` |
| GET | Edit | `Module.Service.Edit` |
| POST | Edit | `Module.Service.Edit` |
| POST | Delete | `Module.Service.Delete` |
| GET | Breaches | `Module.Service.View` |
| GET | Dashboard | `Module.Service.View` |

### Services

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Dashboard | `Module.Service.View` |
| GET | Index | `Module.Service.View` |
| GET | Details | `Module.Service.View` |
| GET | Create | `Module.Service.Create` |
| POST | Create | `Module.Service.Create` |
| GET | Edit | `Module.Service.Edit` |
| POST | Edit | `Module.Service.Edit` |
| POST | Delete | `CanAdmin` |
| POST | LinkAsset | `Module.Service.Edit` |
| POST | UnlinkAsset | `Module.Service.Edit` |
| POST | UpdateServiceAsset | `Module.Service.Edit` |
| GET | GetAvailableAssets | `Module.Service.View` |
| POST | LinkRisk | `Module.Service.Edit` |
| POST | UnlinkRisk | `Module.Service.Edit` |
| POST | UpdateServiceRisk | `Module.Service.Edit` |
| GET | GetAvailableRisks | `Module.Service.View` |
| POST | LinkProcess | `Module.Service.Edit` |
| POST | UnlinkProcess | `Module.Service.Edit` |
| POST | UpdateServiceProcess | `Module.Service.Edit` |
| GET | GetAvailableProcesses | `Module.Service.View` |

### SettingsHub

| Verb | Action | Policy required |
|------|--------|-----------------|
| POST | TestSmtp | `CanAdmin` |
| GET | Index | `CanAdmin` |
| POST | SaveRule | `CanAdmin` |
| POST | DeleteRule | `CanAdmin` |
| POST | SaveBulkSettings | `CanAdmin` |
| POST | UpsertSetting | `CanAdmin` |
| POST | UpdateSettingValue | `CanAdmin` |
| POST | DeleteSetting | `CanAdmin` |
| POST | TriggerRelearn | `CanAdmin` |
| POST | TestIntegrationConnection | `CanAdmin` |
| POST | ImportUpload | `CanAdmin` |

### StrategicObjectives

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `Module.Improvement.Edit` |
| GET | Create | `Module.Improvement.Edit` |
| POST | Create | `Module.Improvement.Edit` |
| GET | Edit | `Module.Improvement.Edit` |
| POST | Edit | `Module.Improvement.Edit` |
| POST | Retire | `Module.Improvement.Edit` |
| POST | Restore | `Module.Improvement.Edit` |

### Tasks

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | RedirectToAction | `Module.WorkflowTask.View` |
| GET | Create | `Module.WorkflowTask.Create` |
| POST | Create | `Module.WorkflowTask.Create` |
| GET | Edit | `Module.WorkflowTask.Edit` |
| POST | Edit | `Module.WorkflowTask.Edit` |
| POST | Delete | `Module.WorkflowTask.Delete` |

### Users

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Index | `CanAdmin` |
| GET | Details | `CanAdmin` |
| GET | Create | `CanAdmin` |
| POST | Create | `CanAdmin` |
| GET | Edit | `CanAdmin` |
| POST | Edit | `CanAdmin` |
| POST | Delete | `CanAdmin` |

### Workflow

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | RedirectToAction | `Module.Workflow.View` |
| GET | PendingApprovals | `Module.Workflow.View` |
| POST | ProcessAction | `Module.Workflow.Approve` |
| GET | Details | `Module.Workflow.View` |
| GET | ActiveUsers | `Module.Workflow.View` |
| POST | Delegate | `Module.Workflow.Approve` |

### WorkloadAnalysis

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Dashboard | `Module.Workload.View` |
| GET | Index | `Module.Workload.View` |
| GET | Details | `Module.Workload.View` |
| GET | Create | `Module.Workload.Create` |
| POST | Create | `Module.Workload.Create` |
| GET | Edit | `Module.Workload.Edit` |
| POST | Edit | `Module.Workload.Edit` |
| POST | Delete | `Module.Workload.Delete` |
| GET | Config | `Module.Workload.Edit` |
| POST | SaveConfig | `Module.Workload.Edit` |
| POST | AddLineItem | `Module.Workload.Edit` |
| POST | UpdateLineItem | `Module.Workload.Edit` |
| POST | RemoveLineItem | `Module.Workload.Edit` |
| GET | GetProcessData | `Module.Workload.View` |
| GET | GetServiceData | `Module.Workload.View` |
| POST | Calculate | `Module.Workload.View` |
| POST | Clone | `Module.Workload.Create` |
| GET | ExportScenario | `Module.Workload.View` |
| GET | ExportScenarioPdf | `Module.Workload.View` |
| GET | Compare | `Module.Workload.View` |
| POST | BulkReassignConfig | `Module.Workload.Edit` |

### Export

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | ExportProcessesToExcel | `(unprotected)` |
| GET | ExportProcessesToPdf | `(unprotected)` |
| GET | ExportServicesToExcel | `(unprotected)` |
| GET | ExportServicesToPdf | `(unprotected)` |
| GET | ExportRisksToExcel | `(unprotected)` |
| GET | ExportRisksToPdf | `(unprotected)` |
| GET | ExportIncidentsToExcel | `(unprotected)` |
| GET | ExportImprovementsToExcel | `(unprotected)` |

### Notifications

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | GetNotifications | `(unprotected)` |
| GET | GetUnreadCount | `(unprotected)` |
| POST | MarkAsRead | `(unprotected)` |
| POST | MarkAllAsRead | `(unprotected)` |

### Search

| Verb | Action | Policy required |
|------|--------|-----------------|
| GET | Search | `(unprotected)` |
