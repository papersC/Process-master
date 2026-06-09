/*
  fix-malformed-english-names.sql
  --------------------------------
  The 2026-06-02 bulk import left many rows with the Arabic string pasted into
  the English-name field (NameEn = NameAr). Arabic UI is fine, but English UI
  and exports are degraded. This script restores proper English names.

  Safety:
    * Each UPDATE is guarded with "AND NameEn = NameAr", so it only touches rows
      that are STILL malformed — re-running is a no-op, and a row already fixed
      (by hand or with different English) is left alone.
    * Fully reversible: the Arabic is preserved in NameAr, so to revert any row
      just set NameEn = NameAr again.
    * Wrapped in a transaction with XACT_ABORT so any error rolls back.

  Scope at authoring time: 1 Category, 1 ProcessGroup, 46 Processes.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Category
UPDATE Categories SET NameEn = N'Manage Corporate Resources', UpdatedAt = GETUTCDATE()
  WHERE Code = N'7' AND NameEn = NameAr;

-- Process Group
UPDATE ProcessGroups SET NameEn = N'Manage Payments & Revenues', UpdatedAt = GETUTCDATE()
  WHERE Code = N'7.1' AND NameEn = NameAr;

-- Processes
UPDATE Processes SET NameEn = N'Assign Unified Model Projects & Sign Contracts',      UpdatedAt = GETUTCDATE() WHERE Code = N'1.1.13' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Follow up Execution of Unified Model Projects',        UpdatedAt = GETUTCDATE() WHERE Code = N'1.1.15' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Receive Unified Model Projects',                       UpdatedAt = GETUTCDATE() WHERE Code = N'1.1.16' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Approve Unified Model Consultants'' Payments',         UpdatedAt = GETUTCDATE() WHERE Code = N'1.1.17' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Perform Preventive Maintenance',                       UpdatedAt = GETUTCDATE() WHERE Code = N'2.2.3'  AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Manage Establishment Project Facilities (Maintenance & Cleaning)', UpdatedAt = GETUTCDATE() WHERE Code = N'2.2.8' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Review Requests to Sell Land Mortgaged to the Bank',   UpdatedAt = GETUTCDATE() WHERE Code = N'3.1.7'  AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Develop Investment Strategy & Policy',                 UpdatedAt = GETUTCDATE() WHERE Code = N'3.1.8'  AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Project Investment',                                   UpdatedAt = GETUTCDATE() WHERE Code = N'4.1.2'  AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Prepare Feasibility Studies for Investment Projects',  UpdatedAt = GETUTCDATE() WHERE Code = N'4.1.3'  AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Govern Investment Assets',                             UpdatedAt = GETUTCDATE() WHERE Code = N'4.1.4'  AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Partnership Management Manual',                        UpdatedAt = GETUTCDATE() WHERE Code = N'4.2.3'  AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Disburse Salaries',                                    UpdatedAt = GETUTCDATE() WHERE Code = N'5.3.4'  AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Close the Revenue Accounting Period',                  UpdatedAt = GETUTCDATE() WHERE Code = N'5.3.7'  AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Classify Suppliers',                                   UpdatedAt = GETUTCDATE() WHERE Code = N'8.1.2'  AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Archive Documents',                                    UpdatedAt = GETUTCDATE() WHERE Code = N'10.3.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Develop Digital Transformation Strategy',              UpdatedAt = GETUTCDATE() WHERE Code = N'11.1.3' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Identify Enterprise Technology Needs',                 UpdatedAt = GETUTCDATE() WHERE Code = N'11.4.4' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Measure Marketing Campaigns Impact',                   UpdatedAt = GETUTCDATE() WHERE Code = N'11.6.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Handle Requests from Social Media & Press',            UpdatedAt = GETUTCDATE() WHERE Code = N'12.1.6' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Prepare Internal & External Communication',            UpdatedAt = GETUTCDATE() WHERE Code = N'12.3.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Execute Marketing Activities',                         UpdatedAt = GETUTCDATE() WHERE Code = N'13.1.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Execute Internal & External Communication',            UpdatedAt = GETUTCDATE() WHERE Code = N'13.2.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Prepare Project-Related Reports',                      UpdatedAt = GETUTCDATE() WHERE Code = N'14.2.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Draft Recommendations Related to Organizational Units',UpdatedAt = GETUTCDATE() WHERE Code = N'14.2.3' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Plan the CEO''s Meetings Schedule',                    UpdatedAt = GETUTCDATE() WHERE Code = N'14.2.4' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Govern & Monitor Projects & Initiatives',              UpdatedAt = GETUTCDATE() WHERE Code = N'14.2.5' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Develop Internal Audit Policy',                        UpdatedAt = GETUTCDATE() WHERE Code = N'14.2.6' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Raise Awareness of Internal Audit Policies & Standards',UpdatedAt = GETUTCDATE() WHERE Code = N'14.2.7' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Prepare & Monitor the Internal Audit Plan',            UpdatedAt = GETUTCDATE() WHERE Code = N'14.2.8' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Implement Internal Audit Corrective Actions',          UpdatedAt = GETUTCDATE() WHERE Code = N'15.1.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Develop Risk Register',                                UpdatedAt = GETUTCDATE() WHERE Code = N'15.1.3' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Develop Risk Response Plan',                           UpdatedAt = GETUTCDATE() WHERE Code = N'15.1.4' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Develop Business Continuity Plan & Policy',            UpdatedAt = GETUTCDATE() WHERE Code = N'15.2.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Propose Amendments to Policies & Regulations',         UpdatedAt = GETUTCDATE() WHERE Code = N'15.2.3' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Conduct Administrative Investigations',                UpdatedAt = GETUTCDATE() WHERE Code = N'16.1.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Develop Corporate Strategy',                           UpdatedAt = GETUTCDATE() WHERE Code = N'16.1.6' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Represent the Establishment in Courts',                UpdatedAt = GETUTCDATE() WHERE Code = N'16.2.3' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Promote Corporate Performance Culture',                UpdatedAt = GETUTCDATE() WHERE Code = N'17.3.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Develop Delegation of Authority Policy',               UpdatedAt = GETUTCDATE() WHERE Code = N'17.3.3' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Monitor Corporate Quality',                            UpdatedAt = GETUTCDATE() WHERE Code = N'18.2.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Develop Quality Audit Plan',                           UpdatedAt = GETUTCDATE() WHERE Code = N'18.2.3' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Oversee Excellence Standards Implementation',          UpdatedAt = GETUTCDATE() WHERE Code = N'18.3.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Develop Open & Big Data Plan',                         UpdatedAt = GETUTCDATE() WHERE Code = N'19.1.1' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Govern Corporate Data',                                UpdatedAt = GETUTCDATE() WHERE Code = N'19.2.2' AND NameEn = NameAr;
UPDATE Processes SET NameEn = N'Govern Housing Policies (Monitor Policy Application)', UpdatedAt = GETUTCDATE() WHERE Code = N'19.3.2' AND NameEn = NameAr;

COMMIT;
PRINT 'Malformed English names fixed.';
