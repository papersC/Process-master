namespace ESEMS.Web.Data;

/// <summary>
/// BPMN 2.0 diagram templates with full visual layout for bpmn-js rendering
/// </summary>
public static class BpmnDiagramTemplates
{
    /// <summary>
    /// Housing Application Submission Process - Customer journey through digital application
    /// Includes BPMN Pools, Lanes, and Stakeholders
    /// </summary>
    public static string ApplicationSubmission => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" xmlns:di=""http://www.omg.org/spec/DD/20100524/DI"" id=""Definitions_AppSubmission"" targetNamespace=""http://mbrhe.gov.ae/bpmn"">
  <bpmn:collaboration id=""Collaboration_AppSubmission"">
    <bpmn:participant id=""Participant_Customer"" name=""Customer"" processRef=""Process_Customer""/>
    <bpmn:participant id=""Participant_MBRHE"" name=""MBRHE"" processRef=""Process_MBRHE""/>
    <bpmn:participant id=""Participant_UAEPass"" name=""UAE Pass System""/>
    <bpmn:messageFlow id=""Flow_Msg1"" sourceRef=""Task_Login"" targetRef=""Participant_UAEPass""/>
    <bpmn:messageFlow id=""Flow_Msg2"" sourceRef=""Participant_UAEPass"" targetRef=""Task_Login""/>
    <bpmn:messageFlow id=""Flow_Msg3"" sourceRef=""Task_Review"" targetRef=""Task_ReceiveApp""/>
    <bpmn:messageFlow id=""Flow_Msg4"" sourceRef=""Task_SendConfirm"" targetRef=""Participant_Customer""/>
  </bpmn:collaboration>
  <bpmn:process id=""Process_Customer"" name=""Customer Process"" isExecutable=""false"">
    <bpmn:laneSet id=""LaneSet_Customer"">
      <bpmn:lane id=""Lane_Customer"" name=""Customer"">
        <bpmn:flowNodeRef>Start_1</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Login</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_SelectService</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_FillForm</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_UploadDocs</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_FixErrors</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Review</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_Customer</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:startEvent id=""Start_1"" name=""Customer Initiates Application"">
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:userTask id=""Task_Login"" name=""Login via UAE Pass"">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:userTask id=""Task_SelectService"" name=""Select Housing Service Type"">
      <bpmn:incoming>Flow_2</bpmn:incoming>
      <bpmn:outgoing>Flow_3</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:userTask id=""Task_FillForm"" name=""Complete Application Form"">
      <bpmn:incoming>Flow_3</bpmn:incoming>
      <bpmn:incoming>Flow_10</bpmn:incoming>
      <bpmn:outgoing>Flow_4</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:userTask id=""Task_UploadDocs"" name=""Upload Required Documents"">
      <bpmn:incoming>Flow_4</bpmn:incoming>
      <bpmn:outgoing>Flow_5</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:userTask id=""Task_FixErrors"" name=""Fix Validation Errors"">
      <bpmn:incoming>Flow_8</bpmn:incoming>
      <bpmn:outgoing>Flow_10</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:userTask id=""Task_Review"" name=""Review &amp; Submit"">
      <bpmn:incoming>Flow_7</bpmn:incoming>
      <bpmn:outgoing>Flow_9</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:endEvent id=""End_Customer"" name=""Confirmation Received"">
      <bpmn:incoming>Flow_9</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""Start_1"" targetRef=""Task_Login""/>
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_Login"" targetRef=""Task_SelectService""/>
    <bpmn:sequenceFlow id=""Flow_3"" sourceRef=""Task_SelectService"" targetRef=""Task_FillForm""/>
    <bpmn:sequenceFlow id=""Flow_4"" sourceRef=""Task_FillForm"" targetRef=""Task_UploadDocs""/>
    <bpmn:sequenceFlow id=""Flow_5"" sourceRef=""Task_UploadDocs"" targetRef=""Task_Review""/>
    <bpmn:sequenceFlow id=""Flow_7"" sourceRef=""Task_Review"" targetRef=""Task_Review""/>
    <bpmn:sequenceFlow id=""Flow_8"" name=""Validation Failed"" sourceRef=""Gateway_Valid"" targetRef=""Task_FixErrors""/>
    <bpmn:sequenceFlow id=""Flow_9"" sourceRef=""Task_Review"" targetRef=""End_Customer""/>
    <bpmn:sequenceFlow id=""Flow_10"" sourceRef=""Task_FixErrors"" targetRef=""Task_FillForm""/>
  </bpmn:process>
  <bpmn:process id=""Process_MBRHE"" name=""MBRHE Process"" isExecutable=""false"">
    <bpmn:laneSet id=""LaneSet_MBRHE"">
      <bpmn:lane id=""Lane_RequestProcessing"" name=""Customer Happiness - Request Processing Section (CHP-RP)"">
        <bpmn:flowNodeRef>Task_ReceiveApp</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Validate</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Valid</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_GenerateRef</bpmn:flowNodeRef>
      </bpmn:lane>
      <bpmn:lane id=""Lane_DigitalTransformation"" name=""Digital Transformation - Smart Systems Section (DIG-SD)"">
        <bpmn:flowNodeRef>Task_SendConfirm</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_1</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:startEvent id=""Task_ReceiveApp"" name=""Application Received"">
      <bpmn:outgoing>Flow_11</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:serviceTask id=""Task_Validate"" name=""Validate Application Data"">
      <bpmn:incoming>Flow_11</bpmn:incoming>
      <bpmn:outgoing>Flow_6</bpmn:outgoing>
    </bpmn:serviceTask>
    <bpmn:exclusiveGateway id=""Gateway_Valid"" name=""Data Valid?"">
      <bpmn:incoming>Flow_6</bpmn:incoming>
      <bpmn:outgoing>Flow_12</bpmn:outgoing>
      <bpmn:outgoing>Flow_8</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    <bpmn:serviceTask id=""Task_GenerateRef"" name=""Generate Reference Number"">
      <bpmn:incoming>Flow_12</bpmn:incoming>
      <bpmn:outgoing>Flow_13</bpmn:outgoing>
    </bpmn:serviceTask>
    <bpmn:sendTask id=""Task_SendConfirm"" name=""Send Confirmation SMS/Email"">
      <bpmn:incoming>Flow_13</bpmn:incoming>
      <bpmn:outgoing>Flow_14</bpmn:outgoing>
    </bpmn:sendTask>
    <bpmn:endEvent id=""End_1"" name=""Application Registered"">
      <bpmn:incoming>Flow_14</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_6"" sourceRef=""Task_Validate"" targetRef=""Gateway_Valid""/>
    <bpmn:sequenceFlow id=""Flow_11"" sourceRef=""Task_ReceiveApp"" targetRef=""Task_Validate""/>
    <bpmn:sequenceFlow id=""Flow_12"" name=""Yes"" sourceRef=""Gateway_Valid"" targetRef=""Task_GenerateRef""/>
    <bpmn:sequenceFlow id=""Flow_13"" sourceRef=""Task_GenerateRef"" targetRef=""Task_SendConfirm""/>
    <bpmn:sequenceFlow id=""Flow_14"" sourceRef=""Task_SendConfirm"" targetRef=""End_1""/>
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Collaboration_AppSubmission"">
      <bpmndi:BPMNShape id=""Participant_Customer_di"" bpmnElement=""Participant_Customer"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""80"" width=""1400"" height=""280""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_Customer_di"" bpmnElement=""Lane_Customer"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""80"" width=""1370"" height=""280""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Participant_MBRHE_di"" bpmnElement=""Participant_MBRHE"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""400"" width=""1400"" height=""320""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_RequestProcessing_di"" bpmnElement=""Lane_RequestProcessing"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""400"" width=""1370"" height=""160""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_DigitalTransformation_di"" bpmnElement=""Lane_DigitalTransformation"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""560"" width=""1370"" height=""160""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Participant_UAEPass_di"" bpmnElement=""Participant_UAEPass"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""760"" width=""1400"" height=""80""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Start_1_di"" bpmnElement=""Start_1""><dc:Bounds x=""200"" y=""202"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Login_di"" bpmnElement=""Task_Login""><dc:Bounds x=""290"" y=""180"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_SelectService_di"" bpmnElement=""Task_SelectService""><dc:Bounds x=""460"" y=""180"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_FillForm_di"" bpmnElement=""Task_FillForm""><dc:Bounds x=""630"" y=""180"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_UploadDocs_di"" bpmnElement=""Task_UploadDocs""><dc:Bounds x=""800"" y=""180"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_FixErrors_di"" bpmnElement=""Task_FixErrors""><dc:Bounds x=""630"" y=""270"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Review_di"" bpmnElement=""Task_Review""><dc:Bounds x=""970"" y=""180"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_Customer_di"" bpmnElement=""End_Customer""><dc:Bounds x=""1142"" y=""202"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_ReceiveApp_di"" bpmnElement=""Task_ReceiveApp""><dc:Bounds x=""200"" y=""462"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Validate_di"" bpmnElement=""Task_Validate""><dc:Bounds x=""290"" y=""440"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Valid_di"" bpmnElement=""Gateway_Valid"" isMarkerVisible=""true""><dc:Bounds x=""465"" y=""455"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_GenerateRef_di"" bpmnElement=""Task_GenerateRef""><dc:Bounds x=""570"" y=""440"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_SendConfirm_di"" bpmnElement=""Task_SendConfirm""><dc:Bounds x=""570"" y=""600"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_1_di"" bpmnElement=""End_1""><dc:Bounds x=""742"" y=""622"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1""><di:waypoint x=""236"" y=""220""/><di:waypoint x=""290"" y=""220""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2""><di:waypoint x=""410"" y=""220""/><di:waypoint x=""460"" y=""220""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_3_di"" bpmnElement=""Flow_3""><di:waypoint x=""580"" y=""220""/><di:waypoint x=""630"" y=""220""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_4_di"" bpmnElement=""Flow_4""><di:waypoint x=""750"" y=""220""/><di:waypoint x=""800"" y=""220""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_5_di"" bpmnElement=""Flow_5""><di:waypoint x=""920"" y=""220""/><di:waypoint x=""970"" y=""220""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_9_di"" bpmnElement=""Flow_9""><di:waypoint x=""1090"" y=""220""/><di:waypoint x=""1142"" y=""220""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_10_di"" bpmnElement=""Flow_10""><di:waypoint x=""690"" y=""270""/><di:waypoint x=""690"" y=""260""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_6_di"" bpmnElement=""Flow_6""><di:waypoint x=""410"" y=""480""/><di:waypoint x=""465"" y=""480""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_11_di"" bpmnElement=""Flow_11""><di:waypoint x=""236"" y=""480""/><di:waypoint x=""290"" y=""480""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_12_di"" bpmnElement=""Flow_12""><di:waypoint x=""515"" y=""480""/><di:waypoint x=""570"" y=""480""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_8_di"" bpmnElement=""Flow_8""><di:waypoint x=""490"" y=""505""/><di:waypoint x=""490"" y=""310""/><di:waypoint x=""690"" y=""310""/><di:waypoint x=""690"" y=""270""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_13_di"" bpmnElement=""Flow_13""><di:waypoint x=""630"" y=""520""/><di:waypoint x=""630"" y=""600""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_14_di"" bpmnElement=""Flow_14""><di:waypoint x=""690"" y=""640""/><di:waypoint x=""742"" y=""640""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg1_di"" bpmnElement=""Flow_Msg1""><di:waypoint x=""350"" y=""260""/><di:waypoint x=""350"" y=""760""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg2_di"" bpmnElement=""Flow_Msg2""><di:waypoint x=""370"" y=""760""/><di:waypoint x=""370"" y=""260""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg3_di"" bpmnElement=""Flow_Msg3""><di:waypoint x=""1030"" y=""260""/><di:waypoint x=""1030"" y=""370""/><di:waypoint x=""218"" y=""370""/><di:waypoint x=""218"" y=""462""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg4_di"" bpmnElement=""Flow_Msg4""><di:waypoint x=""630"" y=""600""/><di:waypoint x=""630"" y=""370""/><di:waypoint x=""1160"" y=""370""/><di:waypoint x=""1160"" y=""238""/></bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";

    /// <summary>
    /// Eligibility Verification Process - Automated eligibility checking with government integration
    /// Includes BPMN Pools, Lanes, and Stakeholders
    /// </summary>
    public static string EligibilityVerification => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" xmlns:di=""http://www.omg.org/spec/DD/20100524/DI"" id=""Definitions_Eligibility"" targetNamespace=""http://mbrhe.gov.ae/bpmn"">
  <bpmn:collaboration id=""Collaboration_Eligibility"">
    <bpmn:participant id=""Participant_MBRHE"" name=""MBRHE"" processRef=""Process_Eligibility""/>
    <bpmn:participant id=""Participant_MOF"" name=""Ministry of Finance""/>
    <bpmn:participant id=""Participant_LandDept"" name=""Dubai Land Department""/>
    <bpmn:participant id=""Participant_ICA"" name=""Federal Authority for Identity and Citizenship""/>
    <bpmn:messageFlow id=""Flow_Msg1"" sourceRef=""Task_IncomeCheck"" targetRef=""Participant_MOF""/>
    <bpmn:messageFlow id=""Flow_Msg2"" sourceRef=""Participant_MOF"" targetRef=""Task_IncomeCheck""/>
    <bpmn:messageFlow id=""Flow_Msg3"" sourceRef=""Task_PropertyCheck"" targetRef=""Participant_LandDept""/>
    <bpmn:messageFlow id=""Flow_Msg4"" sourceRef=""Participant_LandDept"" targetRef=""Task_PropertyCheck""/>
    <bpmn:messageFlow id=""Flow_Msg5"" sourceRef=""Task_FamilyCheck"" targetRef=""Participant_ICA""/>
    <bpmn:messageFlow id=""Flow_Msg6"" sourceRef=""Participant_ICA"" targetRef=""Task_FamilyCheck""/>
  </bpmn:collaboration>
  <bpmn:process id=""Process_Eligibility"" name=""Eligibility Verification"" isExecutable=""false"">
    <bpmn:laneSet id=""LaneSet_Eligibility"">
      <bpmn:lane id=""Lane_ServiceExcellence"" name=""Customer Happiness - Service Excellence Section (CHP-SE)"">
        <bpmn:flowNodeRef>Start_1</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_ExtractData</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Split</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_IncomeCheck</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_PropertyCheck</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_FamilyCheck</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Join</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_CalcScore</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Decision</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_Approved</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_Rejected</bpmn:flowNodeRef>
      </bpmn:lane>
      <bpmn:lane id=""Lane_PlanningBudget"" name=""Support Services - Planning &amp; Budget Section (SSV-PB)"">
        <bpmn:flowNodeRef>Task_ManualReview</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_Manual</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:startEvent id=""Start_1"" name=""Application Received"">
      <bpmn:outgoing>Flow_1</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:serviceTask id=""Task_ExtractData"" name=""Extract Applicant Data from UAE Pass"">
      <bpmn:incoming>Flow_1</bpmn:incoming>
      <bpmn:outgoing>Flow_2</bpmn:outgoing>
    </bpmn:serviceTask>
    <bpmn:parallelGateway id=""Gateway_Split"" name=""Parallel Checks"">
      <bpmn:incoming>Flow_2</bpmn:incoming>
      <bpmn:outgoing>Flow_3</bpmn:outgoing>
      <bpmn:outgoing>Flow_4</bpmn:outgoing>
      <bpmn:outgoing>Flow_5</bpmn:outgoing>
    </bpmn:parallelGateway>
    <bpmn:serviceTask id=""Task_IncomeCheck"" name=""Verify Income (Ministry of Finance)"">
      <bpmn:incoming>Flow_3</bpmn:incoming>
      <bpmn:outgoing>Flow_6</bpmn:outgoing>
    </bpmn:serviceTask>
    <bpmn:serviceTask id=""Task_PropertyCheck"" name=""Check Property Ownership (Land Dept)"">
      <bpmn:incoming>Flow_4</bpmn:incoming>
      <bpmn:outgoing>Flow_7</bpmn:outgoing>
    </bpmn:serviceTask>
    <bpmn:serviceTask id=""Task_FamilyCheck"" name=""Verify Family Status (ICA)"">
      <bpmn:incoming>Flow_5</bpmn:incoming>
      <bpmn:outgoing>Flow_8</bpmn:outgoing>
    </bpmn:serviceTask>
    <bpmn:parallelGateway id=""Gateway_Join"" name=""Merge Results"">
      <bpmn:incoming>Flow_6</bpmn:incoming>
      <bpmn:incoming>Flow_7</bpmn:incoming>
      <bpmn:incoming>Flow_8</bpmn:incoming>
      <bpmn:outgoing>Flow_9</bpmn:outgoing>
    </bpmn:parallelGateway>
    <bpmn:businessRuleTask id=""Task_CalcScore"" name=""Calculate Eligibility Score"">
      <bpmn:incoming>Flow_9</bpmn:incoming>
      <bpmn:outgoing>Flow_10</bpmn:outgoing>
    </bpmn:businessRuleTask>
    <bpmn:exclusiveGateway id=""Gateway_Decision"" name=""Eligible?"">
      <bpmn:incoming>Flow_10</bpmn:incoming>
      <bpmn:outgoing>Flow_11</bpmn:outgoing>
      <bpmn:outgoing>Flow_12</bpmn:outgoing>
      <bpmn:outgoing>Flow_13</bpmn:outgoing>
    </bpmn:exclusiveGateway>
    <bpmn:endEvent id=""End_Approved"" name=""Approved - Proceed to Review"">
      <bpmn:incoming>Flow_11</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:endEvent id=""End_Rejected"" name=""Rejected - Notify Applicant"">
      <bpmn:incoming>Flow_12</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:userTask id=""Task_ManualReview"" name=""Manual Review Required"">
      <bpmn:incoming>Flow_13</bpmn:incoming>
      <bpmn:outgoing>Flow_14</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:endEvent id=""End_Manual"" name=""Pending Manual Decision"">
      <bpmn:incoming>Flow_14</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""Start_1"" targetRef=""Task_ExtractData""/>
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_ExtractData"" targetRef=""Gateway_Split""/>
    <bpmn:sequenceFlow id=""Flow_3"" sourceRef=""Gateway_Split"" targetRef=""Task_IncomeCheck""/>
    <bpmn:sequenceFlow id=""Flow_4"" sourceRef=""Gateway_Split"" targetRef=""Task_PropertyCheck""/>
    <bpmn:sequenceFlow id=""Flow_5"" sourceRef=""Gateway_Split"" targetRef=""Task_FamilyCheck""/>
    <bpmn:sequenceFlow id=""Flow_6"" sourceRef=""Task_IncomeCheck"" targetRef=""Gateway_Join""/>
    <bpmn:sequenceFlow id=""Flow_7"" sourceRef=""Task_PropertyCheck"" targetRef=""Gateway_Join""/>
    <bpmn:sequenceFlow id=""Flow_8"" sourceRef=""Task_FamilyCheck"" targetRef=""Gateway_Join""/>
    <bpmn:sequenceFlow id=""Flow_9"" sourceRef=""Gateway_Join"" targetRef=""Task_CalcScore""/>
    <bpmn:sequenceFlow id=""Flow_10"" sourceRef=""Task_CalcScore"" targetRef=""Gateway_Decision""/>
    <bpmn:sequenceFlow id=""Flow_11"" name=""Score ≥ 70"" sourceRef=""Gateway_Decision"" targetRef=""End_Approved""/>
    <bpmn:sequenceFlow id=""Flow_12"" name=""Score &lt; 40"" sourceRef=""Gateway_Decision"" targetRef=""End_Rejected""/>
    <bpmn:sequenceFlow id=""Flow_13"" name=""40-69"" sourceRef=""Gateway_Decision"" targetRef=""Task_ManualReview""/>
    <bpmn:sequenceFlow id=""Flow_14"" sourceRef=""Task_ManualReview"" targetRef=""End_Manual""/>
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Collaboration_Eligibility"">
      <bpmndi:BPMNShape id=""Participant_MBRHE_di"" bpmnElement=""Participant_MBRHE"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""80"" width=""1600"" height=""450""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_ServiceExcellence_di"" bpmnElement=""Lane_ServiceExcellence"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""80"" width=""1570"" height=""280""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_PlanningBudget_di"" bpmnElement=""Lane_PlanningBudget"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""360"" width=""1570"" height=""170""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Participant_MOF_di"" bpmnElement=""Participant_MOF"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""600"" width=""480"" height=""80""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Participant_LandDept_di"" bpmnElement=""Participant_LandDept"" isHorizontal=""true"">
        <dc:Bounds x=""640"" y=""600"" width=""480"" height=""80""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Participant_ICA_di"" bpmnElement=""Participant_ICA"" isHorizontal=""true"">
        <dc:Bounds x=""1160"" y=""600"" width=""560"" height=""80""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Start_1_di"" bpmnElement=""Start_1""><dc:Bounds x=""200"" y=""202"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_ExtractData_di"" bpmnElement=""Task_ExtractData""><dc:Bounds x=""290"" y=""180"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Split_di"" bpmnElement=""Gateway_Split""><dc:Bounds x=""465"" y=""195"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_IncomeCheck_di"" bpmnElement=""Task_IncomeCheck""><dc:Bounds x=""580"" y=""110"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_PropertyCheck_di"" bpmnElement=""Task_PropertyCheck""><dc:Bounds x=""580"" y=""210"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_FamilyCheck_di"" bpmnElement=""Task_FamilyCheck""><dc:Bounds x=""760"" y=""110"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Join_di"" bpmnElement=""Gateway_Join""><dc:Bounds x=""945"" y=""195"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_CalcScore_di"" bpmnElement=""Task_CalcScore""><dc:Bounds x=""1060"" y=""180"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Decision_di"" bpmnElement=""Gateway_Decision"" isMarkerVisible=""true""><dc:Bounds x=""1245"" y=""195"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_Approved_di"" bpmnElement=""End_Approved""><dc:Bounds x=""1382"" y=""122"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_Rejected_di"" bpmnElement=""End_Rejected""><dc:Bounds x=""1382"" y=""282"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_ManualReview_di"" bpmnElement=""Task_ManualReview""><dc:Bounds x=""1210"" y=""400"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_Manual_di"" bpmnElement=""End_Manual""><dc:Bounds x=""1402"" y=""422"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1""><di:waypoint x=""236"" y=""220""/><di:waypoint x=""290"" y=""220""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2""><di:waypoint x=""410"" y=""220""/><di:waypoint x=""465"" y=""220""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_3_di"" bpmnElement=""Flow_3""><di:waypoint x=""490"" y=""195""/><di:waypoint x=""490"" y=""150""/><di:waypoint x=""580"" y=""150""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_4_di"" bpmnElement=""Flow_4""><di:waypoint x=""490"" y=""245""/><di:waypoint x=""490"" y=""250""/><di:waypoint x=""580"" y=""250""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_5_di"" bpmnElement=""Flow_5""><di:waypoint x=""515"" y=""220""/><di:waypoint x=""540"" y=""220""/><di:waypoint x=""540"" y=""150""/><di:waypoint x=""760"" y=""150""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_6_di"" bpmnElement=""Flow_6""><di:waypoint x=""700"" y=""150""/><di:waypoint x=""970"" y=""150""/><di:waypoint x=""970"" y=""195""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_7_di"" bpmnElement=""Flow_7""><di:waypoint x=""700"" y=""250""/><di:waypoint x=""970"" y=""250""/><di:waypoint x=""970"" y=""245""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_8_di"" bpmnElement=""Flow_8""><di:waypoint x=""880"" y=""150""/><di:waypoint x=""970"" y=""150""/><di:waypoint x=""970"" y=""195""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_9_di"" bpmnElement=""Flow_9""><di:waypoint x=""995"" y=""220""/><di:waypoint x=""1060"" y=""220""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_10_di"" bpmnElement=""Flow_10""><di:waypoint x=""1180"" y=""220""/><di:waypoint x=""1245"" y=""220""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_11_di"" bpmnElement=""Flow_11""><di:waypoint x=""1270"" y=""195""/><di:waypoint x=""1270"" y=""140""/><di:waypoint x=""1382"" y=""140""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_12_di"" bpmnElement=""Flow_12""><di:waypoint x=""1270"" y=""245""/><di:waypoint x=""1270"" y=""300""/><di:waypoint x=""1382"" y=""300""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_13_di"" bpmnElement=""Flow_13""><di:waypoint x=""1270"" y=""245""/><di:waypoint x=""1270"" y=""400""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_14_di"" bpmnElement=""Flow_14""><di:waypoint x=""1330"" y=""440""/><di:waypoint x=""1402"" y=""440""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg1_di"" bpmnElement=""Flow_Msg1""><di:waypoint x=""640"" y=""190""/><di:waypoint x=""640"" y=""395""/><di:waypoint x=""360"" y=""395""/><di:waypoint x=""360"" y=""600""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg2_di"" bpmnElement=""Flow_Msg2""><di:waypoint x=""380"" y=""600""/><di:waypoint x=""380"" y=""395""/><di:waypoint x=""660"" y=""395""/><di:waypoint x=""660"" y=""190""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg3_di"" bpmnElement=""Flow_Msg3""><di:waypoint x=""640"" y=""290""/><di:waypoint x=""640"" y=""445""/><di:waypoint x=""880"" y=""445""/><di:waypoint x=""880"" y=""600""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg4_di"" bpmnElement=""Flow_Msg4""><di:waypoint x=""900"" y=""600""/><di:waypoint x=""900"" y=""445""/><di:waypoint x=""660"" y=""445""/><di:waypoint x=""660"" y=""290""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg5_di"" bpmnElement=""Flow_Msg5""><di:waypoint x=""820"" y=""190""/><di:waypoint x=""820"" y=""395""/><di:waypoint x=""1400"" y=""395""/><di:waypoint x=""1400"" y=""600""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg6_di"" bpmnElement=""Flow_Msg6""><di:waypoint x=""1420"" y=""600""/><di:waypoint x=""1420"" y=""395""/><di:waypoint x=""840"" y=""395""/><di:waypoint x=""840"" y=""190""/></bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";

    /// <summary>
    /// Document Review Process - Multi-stage document verification workflow
    /// Includes BPMN Pools, Lanes, and Stakeholders
    /// </summary>
    public static string DocumentReview => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" xmlns:di=""http://www.omg.org/spec/DD/20100524/DI"" id=""Definitions_DocReview"" targetNamespace=""http://mbrhe.gov.ae/bpmn"">
  <bpmn:collaboration id=""Collaboration_DocReview"">
    <bpmn:participant id=""Participant_MBRHE"" name=""MBRHE"" processRef=""Process_DocReview""/>
    <bpmn:participant id=""Participant_Customer"" name=""Customer""/>
    <bpmn:messageFlow id=""Flow_Msg1"" sourceRef=""Task_RequestDocs"" targetRef=""Participant_Customer""/>
    <bpmn:messageFlow id=""Flow_Msg2"" sourceRef=""Participant_Customer"" targetRef=""Event_Wait""/>
  </bpmn:collaboration>
  <bpmn:process id=""Process_DocReview"" name=""Document Review Process"" isExecutable=""false"">
    <bpmn:laneSet id=""LaneSet_DocReview"">
      <bpmn:lane id=""Lane_PlanningDesign"" name=""Engineering Projects - Planning &amp; Design Section (ENG-PD)"">
        <bpmn:flowNodeRef>Start_1</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_OCR</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Classify</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Complete</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Verify</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_CrossRef</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Valid</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_Approved</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_Rejected</bpmn:flowNodeRef>
      </bpmn:lane>
      <bpmn:lane id=""Lane_RequestProcessing"" name=""Customer Happiness - Request Processing Section (CHP-RP)"">
        <bpmn:flowNodeRef>Task_RequestDocs</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Event_Wait</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:startEvent id=""Start_1"" name=""Documents Received""><bpmn:outgoing>Flow_1</bpmn:outgoing></bpmn:startEvent>
    <bpmn:serviceTask id=""Task_OCR"" name=""OCR Document Scanning""><bpmn:incoming>Flow_1</bpmn:incoming><bpmn:outgoing>Flow_2</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:serviceTask id=""Task_Classify"" name=""AI Document Classification""><bpmn:incoming>Flow_2</bpmn:incoming><bpmn:outgoing>Flow_3</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:exclusiveGateway id=""Gateway_Complete"" name=""All Docs Present?""><bpmn:incoming>Flow_3</bpmn:incoming><bpmn:outgoing>Flow_4</bpmn:outgoing><bpmn:outgoing>Flow_5</bpmn:outgoing></bpmn:exclusiveGateway>
    <bpmn:sendTask id=""Task_RequestDocs"" name=""Request Missing Documents""><bpmn:incoming>Flow_5</bpmn:incoming><bpmn:outgoing>Flow_6</bpmn:outgoing></bpmn:sendTask>
    <bpmn:intermediateCatchEvent id=""Event_Wait"" name=""Wait for Documents""><bpmn:incoming>Flow_6</bpmn:incoming><bpmn:outgoing>Flow_7</bpmn:outgoing><bpmn:timerEventDefinition/></bpmn:intermediateCatchEvent>
    <bpmn:userTask id=""Task_Verify"" name=""Verify Document Authenticity""><bpmn:incoming>Flow_4</bpmn:incoming><bpmn:incoming>Flow_7</bpmn:incoming><bpmn:outgoing>Flow_8</bpmn:outgoing></bpmn:userTask>
    <bpmn:serviceTask id=""Task_CrossRef"" name=""Cross-Reference External Sources""><bpmn:incoming>Flow_8</bpmn:incoming><bpmn:outgoing>Flow_9</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:exclusiveGateway id=""Gateway_Valid"" name=""Documents Valid?""><bpmn:incoming>Flow_9</bpmn:incoming><bpmn:outgoing>Flow_10</bpmn:outgoing><bpmn:outgoing>Flow_11</bpmn:outgoing></bpmn:exclusiveGateway>
    <bpmn:endEvent id=""End_Approved"" name=""Documents Approved""><bpmn:incoming>Flow_10</bpmn:incoming></bpmn:endEvent>
    <bpmn:endEvent id=""End_Rejected"" name=""Documents Rejected""><bpmn:incoming>Flow_11</bpmn:incoming><bpmn:errorEventDefinition/></bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""Start_1"" targetRef=""Task_OCR""/>
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_OCR"" targetRef=""Task_Classify""/>
    <bpmn:sequenceFlow id=""Flow_3"" sourceRef=""Task_Classify"" targetRef=""Gateway_Complete""/>
    <bpmn:sequenceFlow id=""Flow_4"" name=""Yes"" sourceRef=""Gateway_Complete"" targetRef=""Task_Verify""/>
    <bpmn:sequenceFlow id=""Flow_5"" name=""No"" sourceRef=""Gateway_Complete"" targetRef=""Task_RequestDocs""/>
    <bpmn:sequenceFlow id=""Flow_6"" sourceRef=""Task_RequestDocs"" targetRef=""Event_Wait""/>
    <bpmn:sequenceFlow id=""Flow_7"" sourceRef=""Event_Wait"" targetRef=""Task_Verify""/>
    <bpmn:sequenceFlow id=""Flow_8"" sourceRef=""Task_Verify"" targetRef=""Task_CrossRef""/>
    <bpmn:sequenceFlow id=""Flow_9"" sourceRef=""Task_CrossRef"" targetRef=""Gateway_Valid""/>
    <bpmn:sequenceFlow id=""Flow_10"" name=""Valid"" sourceRef=""Gateway_Valid"" targetRef=""End_Approved""/>
    <bpmn:sequenceFlow id=""Flow_11"" name=""Invalid"" sourceRef=""Gateway_Valid"" targetRef=""End_Rejected""/>
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Collaboration_DocReview"">
      <bpmndi:BPMNShape id=""Participant_MBRHE_di"" bpmnElement=""Participant_MBRHE"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""80"" width=""1500"" height=""400""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_PlanningDesign_di"" bpmnElement=""Lane_PlanningDesign"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""80"" width=""1470"" height=""240""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_RequestProcessing_di"" bpmnElement=""Lane_RequestProcessing"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""320"" width=""1470"" height=""160""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Participant_Customer_di"" bpmnElement=""Participant_Customer"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""520"" width=""1500"" height=""80""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Start_1_di"" bpmnElement=""Start_1""><dc:Bounds x=""200"" y=""182"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_OCR_di"" bpmnElement=""Task_OCR""><dc:Bounds x=""290"" y=""160"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Classify_di"" bpmnElement=""Task_Classify""><dc:Bounds x=""460"" y=""160"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Complete_di"" bpmnElement=""Gateway_Complete"" isMarkerVisible=""true""><dc:Bounds x=""635"" y=""175"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Verify_di"" bpmnElement=""Task_Verify""><dc:Bounds x=""880"" y=""160"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_CrossRef_di"" bpmnElement=""Task_CrossRef""><dc:Bounds x=""1050"" y=""160"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Valid_di"" bpmnElement=""Gateway_Valid"" isMarkerVisible=""true""><dc:Bounds x=""1225"" y=""175"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_Approved_di"" bpmnElement=""End_Approved""><dc:Bounds x=""1362"" y=""122"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_Rejected_di"" bpmnElement=""End_Rejected""><dc:Bounds x=""1362"" y=""252"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_RequestDocs_di"" bpmnElement=""Task_RequestDocs""><dc:Bounds x=""600"" y=""360"" width=""120"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Event_Wait_di"" bpmnElement=""Event_Wait""><dc:Bounds x=""782"" y=""382"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1""><di:waypoint x=""236"" y=""200""/><di:waypoint x=""290"" y=""200""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2""><di:waypoint x=""410"" y=""200""/><di:waypoint x=""460"" y=""200""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_3_di"" bpmnElement=""Flow_3""><di:waypoint x=""580"" y=""200""/><di:waypoint x=""635"" y=""200""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_4_di"" bpmnElement=""Flow_4""><di:waypoint x=""685"" y=""200""/><di:waypoint x=""880"" y=""200""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_5_di"" bpmnElement=""Flow_5""><di:waypoint x=""660"" y=""225""/><di:waypoint x=""660"" y=""360""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_6_di"" bpmnElement=""Flow_6""><di:waypoint x=""720"" y=""400""/><di:waypoint x=""782"" y=""400""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_7_di"" bpmnElement=""Flow_7""><di:waypoint x=""818"" y=""400""/><di:waypoint x=""940"" y=""400""/><di:waypoint x=""940"" y=""240""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_8_di"" bpmnElement=""Flow_8""><di:waypoint x=""1000"" y=""200""/><di:waypoint x=""1050"" y=""200""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_9_di"" bpmnElement=""Flow_9""><di:waypoint x=""1170"" y=""200""/><di:waypoint x=""1225"" y=""200""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_10_di"" bpmnElement=""Flow_10""><di:waypoint x=""1250"" y=""175""/><di:waypoint x=""1250"" y=""140""/><di:waypoint x=""1362"" y=""140""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_11_di"" bpmnElement=""Flow_11""><di:waypoint x=""1250"" y=""225""/><di:waypoint x=""1250"" y=""270""/><di:waypoint x=""1362"" y=""270""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg1_di"" bpmnElement=""Flow_Msg1""><di:waypoint x=""660"" y=""440""/><di:waypoint x=""660"" y=""520""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg2_di"" bpmnElement=""Flow_Msg2""><di:waypoint x=""800"" y=""520""/><di:waypoint x=""800"" y=""418""/></bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";

    /// <summary>
    /// Approval and Contract Process - Multi-level approval with contract generation
    /// Includes BPMN Pools, Lanes, and Stakeholders
    /// </summary>
    public static string ApprovalContract => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" xmlns:di=""http://www.omg.org/spec/DD/20100524/DI"" id=""Definitions_Approval"" targetNamespace=""http://mbrhe.gov.ae/bpmn"">
  <bpmn:collaboration id=""Collaboration_Approval"">
    <bpmn:participant id=""Participant_MBRHE"" name=""MBRHE"" processRef=""Process_Approval""/>
    <bpmn:participant id=""Participant_Customer"" name=""Customer""/>
    <bpmn:messageFlow id=""Flow_Msg1"" sourceRef=""Task_SignContract"" targetRef=""Participant_Customer""/>
    <bpmn:messageFlow id=""Flow_Msg2"" sourceRef=""Participant_Customer"" targetRef=""Task_SignContract""/>
    <bpmn:messageFlow id=""Flow_Msg3"" sourceRef=""Task_Notify"" targetRef=""Participant_Customer""/>
    <bpmn:messageFlow id=""Flow_Msg4"" sourceRef=""Task_RejectNotify"" targetRef=""Participant_Customer""/>
  </bpmn:collaboration>
  <bpmn:process id=""Process_Approval"" name=""Approval and Contract Process"" isExecutable=""false"">
    <bpmn:laneSet id=""LaneSet_Approval"">
      <bpmn:lane id=""Lane_PlanningBudget"" name=""Support Services - Planning &amp; Budget Section (SSV-PB)"">
        <bpmn:flowNodeRef>Start_1</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_TechReview</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Tech</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_FinReview</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Fin</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_DGApproval</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_GenContract</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Archive</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_Approved</bpmn:flowNodeRef>
      </bpmn:lane>
      <bpmn:lane id=""Lane_RequestProcessing"" name=""Customer Happiness - Request Processing Section (CHP-RP)"">
        <bpmn:flowNodeRef>Task_SignContract</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Notify</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_RejectNotify</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_Rejected</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:startEvent id=""Start_1"" name=""Application Ready for Approval""><bpmn:outgoing>Flow_1</bpmn:outgoing></bpmn:startEvent>
    <bpmn:userTask id=""Task_TechReview"" name=""Technical Committee Review""><bpmn:incoming>Flow_1</bpmn:incoming><bpmn:outgoing>Flow_2</bpmn:outgoing></bpmn:userTask>
    <bpmn:exclusiveGateway id=""Gateway_Tech"" name=""Technical Approved?""><bpmn:incoming>Flow_2</bpmn:incoming><bpmn:outgoing>Flow_3</bpmn:outgoing><bpmn:outgoing>Flow_4</bpmn:outgoing></bpmn:exclusiveGateway>
    <bpmn:userTask id=""Task_FinReview"" name=""Financial Committee Review""><bpmn:incoming>Flow_3</bpmn:incoming><bpmn:outgoing>Flow_5</bpmn:outgoing></bpmn:userTask>
    <bpmn:exclusiveGateway id=""Gateway_Fin"" name=""Financial Approved?""><bpmn:incoming>Flow_5</bpmn:incoming><bpmn:outgoing>Flow_6</bpmn:outgoing><bpmn:outgoing>Flow_7</bpmn:outgoing></bpmn:exclusiveGateway>
    <bpmn:userTask id=""Task_DGApproval"" name=""Director General Approval""><bpmn:incoming>Flow_6</bpmn:incoming><bpmn:outgoing>Flow_8</bpmn:outgoing></bpmn:userTask>
    <bpmn:serviceTask id=""Task_GenContract"" name=""Generate Contract Document""><bpmn:incoming>Flow_8</bpmn:incoming><bpmn:outgoing>Flow_9</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:userTask id=""Task_SignContract"" name=""Customer Signs Contract""><bpmn:incoming>Flow_9</bpmn:incoming><bpmn:outgoing>Flow_10</bpmn:outgoing></bpmn:userTask>
    <bpmn:serviceTask id=""Task_Archive"" name=""Archive in DMS""><bpmn:incoming>Flow_10</bpmn:incoming><bpmn:outgoing>Flow_11</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:sendTask id=""Task_Notify"" name=""Send Approval Notification""><bpmn:incoming>Flow_11</bpmn:incoming><bpmn:outgoing>Flow_12</bpmn:outgoing></bpmn:sendTask>
    <bpmn:endEvent id=""End_Approved"" name=""Contract Finalized""><bpmn:incoming>Flow_12</bpmn:incoming></bpmn:endEvent>
    <bpmn:sendTask id=""Task_RejectNotify"" name=""Send Rejection Notice""><bpmn:incoming>Flow_4</bpmn:incoming><bpmn:incoming>Flow_7</bpmn:incoming><bpmn:outgoing>Flow_13</bpmn:outgoing></bpmn:sendTask>
    <bpmn:endEvent id=""End_Rejected"" name=""Application Rejected""><bpmn:incoming>Flow_13</bpmn:incoming></bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""Start_1"" targetRef=""Task_TechReview""/>
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_TechReview"" targetRef=""Gateway_Tech""/>
    <bpmn:sequenceFlow id=""Flow_3"" name=""Approved"" sourceRef=""Gateway_Tech"" targetRef=""Task_FinReview""/>
    <bpmn:sequenceFlow id=""Flow_4"" name=""Rejected"" sourceRef=""Gateway_Tech"" targetRef=""Task_RejectNotify""/>
    <bpmn:sequenceFlow id=""Flow_5"" sourceRef=""Task_FinReview"" targetRef=""Gateway_Fin""/>
    <bpmn:sequenceFlow id=""Flow_6"" name=""Approved"" sourceRef=""Gateway_Fin"" targetRef=""Task_DGApproval""/>
    <bpmn:sequenceFlow id=""Flow_7"" name=""Rejected"" sourceRef=""Gateway_Fin"" targetRef=""Task_RejectNotify""/>
    <bpmn:sequenceFlow id=""Flow_8"" sourceRef=""Task_DGApproval"" targetRef=""Task_GenContract""/>
    <bpmn:sequenceFlow id=""Flow_9"" sourceRef=""Task_GenContract"" targetRef=""Task_SignContract""/>
    <bpmn:sequenceFlow id=""Flow_10"" sourceRef=""Task_SignContract"" targetRef=""Task_Archive""/>
    <bpmn:sequenceFlow id=""Flow_11"" sourceRef=""Task_Archive"" targetRef=""Task_Notify""/>
    <bpmn:sequenceFlow id=""Flow_12"" sourceRef=""Task_Notify"" targetRef=""End_Approved""/>
    <bpmn:sequenceFlow id=""Flow_13"" sourceRef=""Task_RejectNotify"" targetRef=""End_Rejected""/>
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Collaboration_Approval"">
      <bpmndi:BPMNShape id=""Participant_MBRHE_di"" bpmnElement=""Participant_MBRHE"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""80"" width=""1500"" height=""350""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_PlanningBudget_di"" bpmnElement=""Lane_PlanningBudget"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""80"" width=""1470"" height=""220""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_RequestProcessing_di"" bpmnElement=""Lane_RequestProcessing"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""300"" width=""1470"" height=""130""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Participant_Customer_di"" bpmnElement=""Participant_Customer"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""470"" width=""1500"" height=""60""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Start_1_di"" bpmnElement=""Start_1""><dc:Bounds x=""200"" y=""152"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_TechReview_di"" bpmnElement=""Task_TechReview""><dc:Bounds x=""280"" y=""130"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Tech_di"" bpmnElement=""Gateway_Tech"" isMarkerVisible=""true""><dc:Bounds x=""425"" y=""145"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_FinReview_di"" bpmnElement=""Task_FinReview""><dc:Bounds x=""520"" y=""130"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Fin_di"" bpmnElement=""Gateway_Fin"" isMarkerVisible=""true""><dc:Bounds x=""665"" y=""145"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_DGApproval_di"" bpmnElement=""Task_DGApproval""><dc:Bounds x=""760"" y=""130"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_GenContract_di"" bpmnElement=""Task_GenContract""><dc:Bounds x=""900"" y=""130"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Archive_di"" bpmnElement=""Task_Archive""><dc:Bounds x=""1180"" y=""130"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_Approved_di"" bpmnElement=""End_Approved""><dc:Bounds x=""1472"" y=""152"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_SignContract_di"" bpmnElement=""Task_SignContract""><dc:Bounds x=""1040"" y=""330"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Notify_di"" bpmnElement=""Task_Notify""><dc:Bounds x=""1320"" y=""330"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_RejectNotify_di"" bpmnElement=""Task_RejectNotify""><dc:Bounds x=""520"" y=""330"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_Rejected_di"" bpmnElement=""End_Rejected""><dc:Bounds x=""672"" y=""352"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1""><di:waypoint x=""236"" y=""170""/><di:waypoint x=""280"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2""><di:waypoint x=""380"" y=""170""/><di:waypoint x=""425"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_3_di"" bpmnElement=""Flow_3""><di:waypoint x=""475"" y=""170""/><di:waypoint x=""520"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_4_di"" bpmnElement=""Flow_4""><di:waypoint x=""450"" y=""195""/><di:waypoint x=""450"" y=""370""/><di:waypoint x=""520"" y=""370""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_5_di"" bpmnElement=""Flow_5""><di:waypoint x=""620"" y=""170""/><di:waypoint x=""665"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_6_di"" bpmnElement=""Flow_6""><di:waypoint x=""715"" y=""170""/><di:waypoint x=""760"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_7_di"" bpmnElement=""Flow_7""><di:waypoint x=""690"" y=""195""/><di:waypoint x=""690"" y=""370""/><di:waypoint x=""620"" y=""370""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_8_di"" bpmnElement=""Flow_8""><di:waypoint x=""860"" y=""170""/><di:waypoint x=""900"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_9_di"" bpmnElement=""Flow_9""><di:waypoint x=""1000"" y=""170""/><di:waypoint x=""1020"" y=""170""/><di:waypoint x=""1020"" y=""370""/><di:waypoint x=""1040"" y=""370""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_10_di"" bpmnElement=""Flow_10""><di:waypoint x=""1140"" y=""370""/><di:waypoint x=""1160"" y=""370""/><di:waypoint x=""1160"" y=""170""/><di:waypoint x=""1180"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_11_di"" bpmnElement=""Flow_11""><di:waypoint x=""1280"" y=""170""/><di:waypoint x=""1300"" y=""170""/><di:waypoint x=""1300"" y=""370""/><di:waypoint x=""1320"" y=""370""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_12_di"" bpmnElement=""Flow_12""><di:waypoint x=""1420"" y=""370""/><di:waypoint x=""1446"" y=""370""/><di:waypoint x=""1446"" y=""170""/><di:waypoint x=""1472"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_13_di"" bpmnElement=""Flow_13""><di:waypoint x=""620"" y=""370""/><di:waypoint x=""672"" y=""370""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg1_di"" bpmnElement=""Flow_Msg1""><di:waypoint x=""1090"" y=""410""/><di:waypoint x=""1090"" y=""470""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg2_di"" bpmnElement=""Flow_Msg2""><di:waypoint x=""1110"" y=""470""/><di:waypoint x=""1110"" y=""410""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg3_di"" bpmnElement=""Flow_Msg3""><di:waypoint x=""1370"" y=""410""/><di:waypoint x=""1370"" y=""470""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg4_di"" bpmnElement=""Flow_Msg4""><di:waypoint x=""570"" y=""410""/><di:waypoint x=""570"" y=""470""/></bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";

    /// <summary>
    /// Customer Inquiry Process - Omnichannel customer service workflow
    /// Includes BPMN Pools, Lanes, and Stakeholders
    /// </summary>
    public static string CustomerInquiry => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" xmlns:di=""http://www.omg.org/spec/DD/20100524/DI"" id=""Definitions_Inquiry"" targetNamespace=""http://mbrhe.gov.ae/bpmn"">
  <bpmn:collaboration id=""Collaboration_Inquiry"">
    <bpmn:participant id=""Participant_MBRHE"" name=""MBRHE"" processRef=""Process_Inquiry""/>
    <bpmn:participant id=""Participant_Customer"" name=""Customer""/>
    <bpmn:messageFlow id=""Flow_Msg1"" sourceRef=""Participant_Customer"" targetRef=""Start_1""/>
    <bpmn:messageFlow id=""Flow_Msg2"" sourceRef=""Task_Survey"" targetRef=""Participant_Customer""/>
  </bpmn:collaboration>
  <bpmn:process id=""Process_Inquiry"" name=""Customer Inquiry Process"" isExecutable=""false"">
    <bpmn:laneSet id=""LaneSet_Inquiry"">
      <bpmn:lane id=""Lane_CustomerCare"" name=""Customer Happiness - Customer Care Section (CHP-CC)"">
        <bpmn:flowNodeRef>Start_1</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Channel</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Phone</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Chatbot</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Email</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Merge</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_LogCRM</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Resolved</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Escalate</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Survey</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_1</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:startEvent id=""Start_1"" name=""Customer Contacts MBRHE""><bpmn:outgoing>Flow_1</bpmn:outgoing></bpmn:startEvent>
    <bpmn:exclusiveGateway id=""Gateway_Channel"" name=""Contact Channel?""><bpmn:incoming>Flow_1</bpmn:incoming><bpmn:outgoing>Flow_2</bpmn:outgoing><bpmn:outgoing>Flow_3</bpmn:outgoing><bpmn:outgoing>Flow_4</bpmn:outgoing></bpmn:exclusiveGateway>
    <bpmn:userTask id=""Task_Phone"" name=""Phone Call Handling""><bpmn:incoming>Flow_2</bpmn:incoming><bpmn:outgoing>Flow_5</bpmn:outgoing></bpmn:userTask>
    <bpmn:serviceTask id=""Task_Chatbot"" name=""AI Chatbot Response""><bpmn:incoming>Flow_3</bpmn:incoming><bpmn:outgoing>Flow_6</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:userTask id=""Task_Email"" name=""Email Response""><bpmn:incoming>Flow_4</bpmn:incoming><bpmn:outgoing>Flow_7</bpmn:outgoing></bpmn:userTask>
    <bpmn:exclusiveGateway id=""Gateway_Merge"" name=""Merge""><bpmn:incoming>Flow_5</bpmn:incoming><bpmn:incoming>Flow_6</bpmn:incoming><bpmn:incoming>Flow_7</bpmn:incoming><bpmn:outgoing>Flow_8</bpmn:outgoing></bpmn:exclusiveGateway>
    <bpmn:serviceTask id=""Task_LogCRM"" name=""Log in CRM System""><bpmn:incoming>Flow_8</bpmn:incoming><bpmn:outgoing>Flow_9</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:exclusiveGateway id=""Gateway_Resolved"" name=""Resolved?""><bpmn:incoming>Flow_9</bpmn:incoming><bpmn:outgoing>Flow_10</bpmn:outgoing><bpmn:outgoing>Flow_11</bpmn:outgoing></bpmn:exclusiveGateway>
    <bpmn:userTask id=""Task_Escalate"" name=""Escalate to Specialist""><bpmn:incoming>Flow_11</bpmn:incoming><bpmn:outgoing>Flow_12</bpmn:outgoing></bpmn:userTask>
    <bpmn:sendTask id=""Task_Survey"" name=""Send Satisfaction Survey""><bpmn:incoming>Flow_10</bpmn:incoming><bpmn:incoming>Flow_12</bpmn:incoming><bpmn:outgoing>Flow_13</bpmn:outgoing></bpmn:sendTask>
    <bpmn:endEvent id=""End_1"" name=""Inquiry Closed""><bpmn:incoming>Flow_13</bpmn:incoming></bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""Start_1"" targetRef=""Gateway_Channel""/>
    <bpmn:sequenceFlow id=""Flow_2"" name=""Phone"" sourceRef=""Gateway_Channel"" targetRef=""Task_Phone""/>
    <bpmn:sequenceFlow id=""Flow_3"" name=""Chat"" sourceRef=""Gateway_Channel"" targetRef=""Task_Chatbot""/>
    <bpmn:sequenceFlow id=""Flow_4"" name=""Email"" sourceRef=""Gateway_Channel"" targetRef=""Task_Email""/>
    <bpmn:sequenceFlow id=""Flow_5"" sourceRef=""Task_Phone"" targetRef=""Gateway_Merge""/>
    <bpmn:sequenceFlow id=""Flow_6"" sourceRef=""Task_Chatbot"" targetRef=""Gateway_Merge""/>
    <bpmn:sequenceFlow id=""Flow_7"" sourceRef=""Task_Email"" targetRef=""Gateway_Merge""/>
    <bpmn:sequenceFlow id=""Flow_8"" sourceRef=""Gateway_Merge"" targetRef=""Task_LogCRM""/>
    <bpmn:sequenceFlow id=""Flow_9"" sourceRef=""Task_LogCRM"" targetRef=""Gateway_Resolved""/>
    <bpmn:sequenceFlow id=""Flow_10"" name=""Yes"" sourceRef=""Gateway_Resolved"" targetRef=""Task_Survey""/>
    <bpmn:sequenceFlow id=""Flow_11"" name=""No"" sourceRef=""Gateway_Resolved"" targetRef=""Task_Escalate""/>
    <bpmn:sequenceFlow id=""Flow_12"" sourceRef=""Task_Escalate"" targetRef=""Task_Survey""/>
    <bpmn:sequenceFlow id=""Flow_13"" sourceRef=""Task_Survey"" targetRef=""End_1""/>
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Collaboration_Inquiry"">
      <bpmndi:BPMNShape id=""Participant_MBRHE_di"" bpmnElement=""Participant_MBRHE"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""80"" width=""1200"" height=""350""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_CustomerCare_di"" bpmnElement=""Lane_CustomerCare"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""80"" width=""1170"" height=""350""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Participant_Customer_di"" bpmnElement=""Participant_Customer"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""470"" width=""1200"" height=""60""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Start_1_di"" bpmnElement=""Start_1""><dc:Bounds x=""200"" y=""222"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Channel_di"" bpmnElement=""Gateway_Channel"" isMarkerVisible=""true""><dc:Bounds x=""285"" y=""215"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Phone_di"" bpmnElement=""Task_Phone""><dc:Bounds x=""380"" y=""100"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Chatbot_di"" bpmnElement=""Task_Chatbot""><dc:Bounds x=""380"" y=""200"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Email_di"" bpmnElement=""Task_Email""><dc:Bounds x=""380"" y=""300"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Merge_di"" bpmnElement=""Gateway_Merge"" isMarkerVisible=""true""><dc:Bounds x=""525"" y=""215"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_LogCRM_di"" bpmnElement=""Task_LogCRM""><dc:Bounds x=""620"" y=""200"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Resolved_di"" bpmnElement=""Gateway_Resolved"" isMarkerVisible=""true""><dc:Bounds x=""765"" y=""215"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Escalate_di"" bpmnElement=""Task_Escalate""><dc:Bounds x=""860"" y=""300"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Survey_di"" bpmnElement=""Task_Survey""><dc:Bounds x=""1000"" y=""200"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_1_di"" bpmnElement=""End_1""><dc:Bounds x=""1152"" y=""222"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1""><di:waypoint x=""236"" y=""240""/><di:waypoint x=""285"" y=""240""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2""><di:waypoint x=""310"" y=""215""/><di:waypoint x=""310"" y=""140""/><di:waypoint x=""380"" y=""140""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_3_di"" bpmnElement=""Flow_3""><di:waypoint x=""335"" y=""240""/><di:waypoint x=""380"" y=""240""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_4_di"" bpmnElement=""Flow_4""><di:waypoint x=""310"" y=""265""/><di:waypoint x=""310"" y=""340""/><di:waypoint x=""380"" y=""340""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_5_di"" bpmnElement=""Flow_5""><di:waypoint x=""480"" y=""140""/><di:waypoint x=""550"" y=""140""/><di:waypoint x=""550"" y=""215""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_6_di"" bpmnElement=""Flow_6""><di:waypoint x=""480"" y=""240""/><di:waypoint x=""525"" y=""240""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_7_di"" bpmnElement=""Flow_7""><di:waypoint x=""480"" y=""340""/><di:waypoint x=""550"" y=""340""/><di:waypoint x=""550"" y=""265""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_8_di"" bpmnElement=""Flow_8""><di:waypoint x=""575"" y=""240""/><di:waypoint x=""620"" y=""240""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_9_di"" bpmnElement=""Flow_9""><di:waypoint x=""720"" y=""240""/><di:waypoint x=""765"" y=""240""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_10_di"" bpmnElement=""Flow_10""><di:waypoint x=""815"" y=""240""/><di:waypoint x=""1000"" y=""240""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_11_di"" bpmnElement=""Flow_11""><di:waypoint x=""790"" y=""265""/><di:waypoint x=""790"" y=""340""/><di:waypoint x=""860"" y=""340""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_12_di"" bpmnElement=""Flow_12""><di:waypoint x=""960"" y=""340""/><di:waypoint x=""1050"" y=""340""/><di:waypoint x=""1050"" y=""280""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_13_di"" bpmnElement=""Flow_13""><di:waypoint x=""1100"" y=""240""/><di:waypoint x=""1152"" y=""240""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg1_di"" bpmnElement=""Flow_Msg1""><di:waypoint x=""218"" y=""470""/><di:waypoint x=""218"" y=""258""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg2_di"" bpmnElement=""Flow_Msg2""><di:waypoint x=""1050"" y=""280""/><di:waypoint x=""1050"" y=""470""/></bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";

    /// <summary>
    /// Complaint Resolution Process - SLA-driven complaint handling
    /// Includes BPMN Pools, Lanes, and Stakeholders
    /// </summary>
    public static string ComplaintResolution => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" xmlns:di=""http://www.omg.org/spec/DD/20100524/DI"" id=""Definitions_Complaint"" targetNamespace=""http://mbrhe.gov.ae/bpmn"">
  <bpmn:collaboration id=""Collaboration_Complaint"">
    <bpmn:participant id=""Participant_MBRHE"" name=""MBRHE"" processRef=""Process_Complaint""/>
    <bpmn:participant id=""Participant_Customer"" name=""Customer""/>
    <bpmn:messageFlow id=""Flow_Msg1"" sourceRef=""Participant_Customer"" targetRef=""Start_1""/>
    <bpmn:messageFlow id=""Flow_Msg2"" sourceRef=""Task_Ack"" targetRef=""Participant_Customer""/>
    <bpmn:messageFlow id=""Flow_Msg3"" sourceRef=""Task_Communicate"" targetRef=""Participant_Customer""/>
    <bpmn:messageFlow id=""Flow_Msg4"" sourceRef=""Participant_Customer"" targetRef=""Gateway_Satisfied""/>
  </bpmn:collaboration>
  <bpmn:process id=""Process_Complaint"" name=""Complaint Resolution Process"" isExecutable=""false"">
    <bpmn:laneSet id=""LaneSet_Complaint"">
      <bpmn:lane id=""Lane_CustomerCare"" name=""Customer Happiness - Customer Care Section (CHP-CC)"">
        <bpmn:flowNodeRef>Start_1</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Register</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Classify</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Ack</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Assign</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Communicate</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Satisfied</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Close</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_1</bpmn:flowNodeRef>
      </bpmn:lane>
      <bpmn:lane id=""Lane_ServiceExcellence"" name=""Customer Happiness - Service Excellence Section (CHP-SE)"">
        <bpmn:flowNodeRef>Task_Investigate</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Event_SLA</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Escalate</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Resolve</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:startEvent id=""Start_1"" name=""Complaint Received""><bpmn:outgoing>Flow_1</bpmn:outgoing></bpmn:startEvent>
    <bpmn:serviceTask id=""Task_Register"" name=""Register in CRM""><bpmn:incoming>Flow_1</bpmn:incoming><bpmn:outgoing>Flow_2</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:serviceTask id=""Task_Classify"" name=""AI Classification &amp; Priority""><bpmn:incoming>Flow_2</bpmn:incoming><bpmn:outgoing>Flow_3</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:sendTask id=""Task_Ack"" name=""Send Acknowledgment""><bpmn:incoming>Flow_3</bpmn:incoming><bpmn:outgoing>Flow_4</bpmn:outgoing></bpmn:sendTask>
    <bpmn:userTask id=""Task_Assign"" name=""Assign to Department""><bpmn:incoming>Flow_4</bpmn:incoming><bpmn:outgoing>Flow_5</bpmn:outgoing></bpmn:userTask>
    <bpmn:userTask id=""Task_Investigate"" name=""Investigate Issue""><bpmn:incoming>Flow_5</bpmn:incoming><bpmn:outgoing>Flow_6</bpmn:outgoing></bpmn:userTask>
    <bpmn:boundaryEvent id=""Event_SLA"" name=""SLA Breach"" attachedToRef=""Task_Investigate""><bpmn:outgoing>Flow_Escalate</bpmn:outgoing><bpmn:timerEventDefinition/></bpmn:boundaryEvent>
    <bpmn:userTask id=""Task_Escalate"" name=""Escalate to Manager""><bpmn:incoming>Flow_Escalate</bpmn:incoming><bpmn:outgoing>Flow_7</bpmn:outgoing></bpmn:userTask>
    <bpmn:userTask id=""Task_Resolve"" name=""Propose Resolution""><bpmn:incoming>Flow_6</bpmn:incoming><bpmn:incoming>Flow_7</bpmn:incoming><bpmn:outgoing>Flow_8</bpmn:outgoing></bpmn:userTask>
    <bpmn:sendTask id=""Task_Communicate"" name=""Communicate Resolution""><bpmn:incoming>Flow_8</bpmn:incoming><bpmn:outgoing>Flow_9</bpmn:outgoing></bpmn:sendTask>
    <bpmn:exclusiveGateway id=""Gateway_Satisfied"" name=""Customer Satisfied?""><bpmn:incoming>Flow_9</bpmn:incoming><bpmn:outgoing>Flow_10</bpmn:outgoing><bpmn:outgoing>Flow_11</bpmn:outgoing></bpmn:exclusiveGateway>
    <bpmn:serviceTask id=""Task_Close"" name=""Close Complaint""><bpmn:incoming>Flow_10</bpmn:incoming><bpmn:outgoing>Flow_12</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:endEvent id=""End_1"" name=""Complaint Resolved""><bpmn:incoming>Flow_12</bpmn:incoming></bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""Start_1"" targetRef=""Task_Register""/>
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_Register"" targetRef=""Task_Classify""/>
    <bpmn:sequenceFlow id=""Flow_3"" sourceRef=""Task_Classify"" targetRef=""Task_Ack""/>
    <bpmn:sequenceFlow id=""Flow_4"" sourceRef=""Task_Ack"" targetRef=""Task_Assign""/>
    <bpmn:sequenceFlow id=""Flow_5"" sourceRef=""Task_Assign"" targetRef=""Task_Investigate""/>
    <bpmn:sequenceFlow id=""Flow_6"" sourceRef=""Task_Investigate"" targetRef=""Task_Resolve""/>
    <bpmn:sequenceFlow id=""Flow_Escalate"" sourceRef=""Event_SLA"" targetRef=""Task_Escalate""/>
    <bpmn:sequenceFlow id=""Flow_7"" sourceRef=""Task_Escalate"" targetRef=""Task_Resolve""/>
    <bpmn:sequenceFlow id=""Flow_8"" sourceRef=""Task_Resolve"" targetRef=""Task_Communicate""/>
    <bpmn:sequenceFlow id=""Flow_9"" sourceRef=""Task_Communicate"" targetRef=""Gateway_Satisfied""/>
    <bpmn:sequenceFlow id=""Flow_10"" name=""Yes"" sourceRef=""Gateway_Satisfied"" targetRef=""Task_Close""/>
    <bpmn:sequenceFlow id=""Flow_11"" name=""No"" sourceRef=""Gateway_Satisfied"" targetRef=""Task_Investigate""/>
    <bpmn:sequenceFlow id=""Flow_12"" sourceRef=""Task_Close"" targetRef=""End_1""/>
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Collaboration_Complaint"">
      <bpmndi:BPMNShape id=""Participant_MBRHE_di"" bpmnElement=""Participant_MBRHE"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""80"" width=""1600"" height=""400""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_CustomerCare_di"" bpmnElement=""Lane_CustomerCare"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""80"" width=""1570"" height=""200""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_ServiceExcellence_di"" bpmnElement=""Lane_ServiceExcellence"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""280"" width=""1570"" height=""200""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Participant_Customer_di"" bpmnElement=""Participant_Customer"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""520"" width=""1600"" height=""60""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Start_1_di"" bpmnElement=""Start_1""><dc:Bounds x=""200"" y=""152"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Register_di"" bpmnElement=""Task_Register""><dc:Bounds x=""280"" y=""130"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Classify_di"" bpmnElement=""Task_Classify""><dc:Bounds x=""420"" y=""130"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Ack_di"" bpmnElement=""Task_Ack""><dc:Bounds x=""560"" y=""130"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Assign_di"" bpmnElement=""Task_Assign""><dc:Bounds x=""700"" y=""130"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Communicate_di"" bpmnElement=""Task_Communicate""><dc:Bounds x=""1140"" y=""130"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Satisfied_di"" bpmnElement=""Gateway_Satisfied"" isMarkerVisible=""true""><dc:Bounds x=""1285"" y=""145"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Close_di"" bpmnElement=""Task_Close""><dc:Bounds x=""1380"" y=""130"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_1_di"" bpmnElement=""End_1""><dc:Bounds x=""1532"" y=""152"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Investigate_di"" bpmnElement=""Task_Investigate""><dc:Bounds x=""840"" y=""320"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Event_SLA_di"" bpmnElement=""Event_SLA""><dc:Bounds x=""872"" y=""382"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Escalate_di"" bpmnElement=""Task_Escalate""><dc:Bounds x=""960"" y=""320"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Resolve_di"" bpmnElement=""Task_Resolve""><dc:Bounds x=""1000"" y=""320"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1""><di:waypoint x=""236"" y=""170""/><di:waypoint x=""280"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2""><di:waypoint x=""380"" y=""170""/><di:waypoint x=""420"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_3_di"" bpmnElement=""Flow_3""><di:waypoint x=""520"" y=""170""/><di:waypoint x=""560"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_4_di"" bpmnElement=""Flow_4""><di:waypoint x=""660"" y=""170""/><di:waypoint x=""700"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_5_di"" bpmnElement=""Flow_5""><di:waypoint x=""800"" y=""170""/><di:waypoint x=""820"" y=""170""/><di:waypoint x=""820"" y=""360""/><di:waypoint x=""840"" y=""360""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_6_di"" bpmnElement=""Flow_6""><di:waypoint x=""940"" y=""360""/><di:waypoint x=""1000"" y=""360""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Escalate_di"" bpmnElement=""Flow_Escalate""><di:waypoint x=""908"" y=""400""/><di:waypoint x=""934"" y=""400""/><di:waypoint x=""934"" y=""360""/><di:waypoint x=""960"" y=""360""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_7_di"" bpmnElement=""Flow_7""><di:waypoint x=""1010"" y=""400""/><di:waypoint x=""1050"" y=""400""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_8_di"" bpmnElement=""Flow_8""><di:waypoint x=""1100"" y=""360""/><di:waypoint x=""1120"" y=""360""/><di:waypoint x=""1120"" y=""170""/><di:waypoint x=""1140"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_9_di"" bpmnElement=""Flow_9""><di:waypoint x=""1240"" y=""170""/><di:waypoint x=""1285"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_10_di"" bpmnElement=""Flow_10""><di:waypoint x=""1335"" y=""170""/><di:waypoint x=""1380"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_11_di"" bpmnElement=""Flow_11""><di:waypoint x=""1310"" y=""195""/><di:waypoint x=""1310"" y=""440""/><di:waypoint x=""890"" y=""440""/><di:waypoint x=""890"" y=""400""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_12_di"" bpmnElement=""Flow_12""><di:waypoint x=""1480"" y=""170""/><di:waypoint x=""1532"" y=""170""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg1_di"" bpmnElement=""Flow_Msg1""><di:waypoint x=""218"" y=""520""/><di:waypoint x=""218"" y=""188""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg2_di"" bpmnElement=""Flow_Msg2""><di:waypoint x=""610"" y=""210""/><di:waypoint x=""610"" y=""520""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg3_di"" bpmnElement=""Flow_Msg3""><di:waypoint x=""1190"" y=""210""/><di:waypoint x=""1190"" y=""520""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg4_di"" bpmnElement=""Flow_Msg4""><di:waypoint x=""1310"" y=""520""/><di:waypoint x=""1310"" y=""195""/></bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";

    /// <summary>
    /// IT Incident Management Process - ITIL-aligned incident handling
    /// Includes BPMN Pools, Lanes, and Stakeholders
    /// </summary>
    public static string IncidentManagement => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" xmlns:di=""http://www.omg.org/spec/DD/20100524/DI"" id=""Definitions_Incident"" targetNamespace=""http://mbrhe.gov.ae/bpmn"">
  <bpmn:collaboration id=""Collaboration_Incident"">
    <bpmn:participant id=""Participant_MBRHE"" name=""MBRHE"" processRef=""Process_Incident""/>
    <bpmn:participant id=""Participant_User"" name=""End User""/>
    <bpmn:messageFlow id=""Flow_Msg1"" sourceRef=""Participant_User"" targetRef=""Start_1""/>
    <bpmn:messageFlow id=""Flow_Msg2"" sourceRef=""Task_Verify"" targetRef=""Participant_User""/>
    <bpmn:messageFlow id=""Flow_Msg3"" sourceRef=""Participant_User"" targetRef=""Task_Verify""/>
  </bpmn:collaboration>
  <bpmn:process id=""Process_Incident"" name=""IT Incident Management"" isExecutable=""false"">
    <bpmn:laneSet id=""LaneSet_Incident"">
      <bpmn:lane id=""Lane_TechnicalSupport"" name=""Digital Transformation - Technical Support Services Section (DIG-TS)"">
        <bpmn:flowNodeRef>Start_1</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Log</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Categorize</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_Major</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_MajorProcess</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_L1</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_L1Resolved</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_L2</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Gateway_L2Resolved</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_L3</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Resolve</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Verify</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>Task_Close</bpmn:flowNodeRef>
        <bpmn:flowNodeRef>End_1</bpmn:flowNodeRef>
      </bpmn:lane>
    </bpmn:laneSet>
    <bpmn:startEvent id=""Start_1"" name=""Incident Reported""><bpmn:outgoing>Flow_1</bpmn:outgoing></bpmn:startEvent>
    <bpmn:serviceTask id=""Task_Log"" name=""Log Incident in ITSM""><bpmn:incoming>Flow_1</bpmn:incoming><bpmn:outgoing>Flow_2</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:businessRuleTask id=""Task_Categorize"" name=""Categorize &amp; Prioritize""><bpmn:incoming>Flow_2</bpmn:incoming><bpmn:outgoing>Flow_3</bpmn:outgoing></bpmn:businessRuleTask>
    <bpmn:exclusiveGateway id=""Gateway_Major"" name=""Major Incident?""><bpmn:incoming>Flow_3</bpmn:incoming><bpmn:outgoing>Flow_4</bpmn:outgoing><bpmn:outgoing>Flow_5</bpmn:outgoing></bpmn:exclusiveGateway>
    <bpmn:callActivity id=""Task_MajorProcess"" name=""Major Incident Process""><bpmn:incoming>Flow_4</bpmn:incoming><bpmn:outgoing>Flow_6</bpmn:outgoing></bpmn:callActivity>
    <bpmn:userTask id=""Task_L1"" name=""L1 Support Investigation""><bpmn:incoming>Flow_5</bpmn:incoming><bpmn:outgoing>Flow_7</bpmn:outgoing></bpmn:userTask>
    <bpmn:exclusiveGateway id=""Gateway_L1Resolved"" name=""Resolved at L1?""><bpmn:incoming>Flow_7</bpmn:incoming><bpmn:outgoing>Flow_8</bpmn:outgoing><bpmn:outgoing>Flow_9</bpmn:outgoing></bpmn:exclusiveGateway>
    <bpmn:userTask id=""Task_L2"" name=""Escalate to L2 Support""><bpmn:incoming>Flow_9</bpmn:incoming><bpmn:outgoing>Flow_10</bpmn:outgoing></bpmn:userTask>
    <bpmn:exclusiveGateway id=""Gateway_L2Resolved"" name=""Resolved at L2?""><bpmn:incoming>Flow_10</bpmn:incoming><bpmn:outgoing>Flow_11</bpmn:outgoing><bpmn:outgoing>Flow_12</bpmn:outgoing></bpmn:exclusiveGateway>
    <bpmn:userTask id=""Task_L3"" name=""Escalate to L3/Vendor""><bpmn:incoming>Flow_12</bpmn:incoming><bpmn:outgoing>Flow_13</bpmn:outgoing></bpmn:userTask>
    <bpmn:serviceTask id=""Task_Resolve"" name=""Apply Resolution""><bpmn:incoming>Flow_6</bpmn:incoming><bpmn:incoming>Flow_8</bpmn:incoming><bpmn:incoming>Flow_11</bpmn:incoming><bpmn:incoming>Flow_13</bpmn:incoming><bpmn:outgoing>Flow_14</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:userTask id=""Task_Verify"" name=""User Verification""><bpmn:incoming>Flow_14</bpmn:incoming><bpmn:outgoing>Flow_15</bpmn:outgoing></bpmn:userTask>
    <bpmn:serviceTask id=""Task_Close"" name=""Close Incident""><bpmn:incoming>Flow_15</bpmn:incoming><bpmn:outgoing>Flow_16</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:endEvent id=""End_1"" name=""Incident Closed""><bpmn:incoming>Flow_16</bpmn:incoming></bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""Start_1"" targetRef=""Task_Log""/>
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Task_Log"" targetRef=""Task_Categorize""/>
    <bpmn:sequenceFlow id=""Flow_3"" sourceRef=""Task_Categorize"" targetRef=""Gateway_Major""/>
    <bpmn:sequenceFlow id=""Flow_4"" name=""Yes"" sourceRef=""Gateway_Major"" targetRef=""Task_MajorProcess""/>
    <bpmn:sequenceFlow id=""Flow_5"" name=""No"" sourceRef=""Gateway_Major"" targetRef=""Task_L1""/>
    <bpmn:sequenceFlow id=""Flow_6"" sourceRef=""Task_MajorProcess"" targetRef=""Task_Resolve""/>
    <bpmn:sequenceFlow id=""Flow_7"" sourceRef=""Task_L1"" targetRef=""Gateway_L1Resolved""/>
    <bpmn:sequenceFlow id=""Flow_8"" name=""Yes"" sourceRef=""Gateway_L1Resolved"" targetRef=""Task_Resolve""/>
    <bpmn:sequenceFlow id=""Flow_9"" name=""No"" sourceRef=""Gateway_L1Resolved"" targetRef=""Task_L2""/>
    <bpmn:sequenceFlow id=""Flow_10"" sourceRef=""Task_L2"" targetRef=""Gateway_L2Resolved""/>
    <bpmn:sequenceFlow id=""Flow_11"" name=""Yes"" sourceRef=""Gateway_L2Resolved"" targetRef=""Task_Resolve""/>
    <bpmn:sequenceFlow id=""Flow_12"" name=""No"" sourceRef=""Gateway_L2Resolved"" targetRef=""Task_L3""/>
    <bpmn:sequenceFlow id=""Flow_13"" sourceRef=""Task_L3"" targetRef=""Task_Resolve""/>
    <bpmn:sequenceFlow id=""Flow_14"" sourceRef=""Task_Resolve"" targetRef=""Task_Verify""/>
    <bpmn:sequenceFlow id=""Flow_15"" sourceRef=""Task_Verify"" targetRef=""Task_Close""/>
    <bpmn:sequenceFlow id=""Flow_16"" sourceRef=""Task_Close"" targetRef=""End_1""/>
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Collaboration_Incident"">
      <bpmndi:BPMNShape id=""Participant_MBRHE_di"" bpmnElement=""Participant_MBRHE"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""80"" width=""1800"" height=""450""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Lane_TechnicalSupport_di"" bpmnElement=""Lane_TechnicalSupport"" isHorizontal=""true"">
        <dc:Bounds x=""150"" y=""80"" width=""1770"" height=""450""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Participant_User_di"" bpmnElement=""Participant_User"" isHorizontal=""true"">
        <dc:Bounds x=""120"" y=""570"" width=""1800"" height=""60""/>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Start_1_di"" bpmnElement=""Start_1""><dc:Bounds x=""200"" y=""242"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Log_di"" bpmnElement=""Task_Log""><dc:Bounds x=""280"" y=""220"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Categorize_di"" bpmnElement=""Task_Categorize""><dc:Bounds x=""420"" y=""220"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Major_di"" bpmnElement=""Gateway_Major"" isMarkerVisible=""true""><dc:Bounds x=""565"" y=""235"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_MajorProcess_di"" bpmnElement=""Task_MajorProcess""><dc:Bounds x=""660"" y=""120"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_L1_di"" bpmnElement=""Task_L1""><dc:Bounds x=""660"" y=""220"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_L1Resolved_di"" bpmnElement=""Gateway_L1Resolved"" isMarkerVisible=""true""><dc:Bounds x=""805"" y=""235"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_L2_di"" bpmnElement=""Task_L2""><dc:Bounds x=""900"" y=""330"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_L2Resolved_di"" bpmnElement=""Gateway_L2Resolved"" isMarkerVisible=""true""><dc:Bounds x=""1045"" y=""345"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_L3_di"" bpmnElement=""Task_L3""><dc:Bounds x=""1140"" y=""430"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Resolve_di"" bpmnElement=""Task_Resolve""><dc:Bounds x=""1280"" y=""220"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Verify_di"" bpmnElement=""Task_Verify""><dc:Bounds x=""1420"" y=""220"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Close_di"" bpmnElement=""Task_Close""><dc:Bounds x=""1560"" y=""220"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_1_di"" bpmnElement=""End_1""><dc:Bounds x=""1702"" y=""242"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1""><di:waypoint x=""236"" y=""260""/><di:waypoint x=""280"" y=""260""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2""><di:waypoint x=""380"" y=""260""/><di:waypoint x=""420"" y=""260""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_3_di"" bpmnElement=""Flow_3""><di:waypoint x=""520"" y=""260""/><di:waypoint x=""565"" y=""260""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_4_di"" bpmnElement=""Flow_4""><di:waypoint x=""590"" y=""235""/><di:waypoint x=""590"" y=""160""/><di:waypoint x=""660"" y=""160""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_5_di"" bpmnElement=""Flow_5""><di:waypoint x=""615"" y=""260""/><di:waypoint x=""660"" y=""260""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_6_di"" bpmnElement=""Flow_6""><di:waypoint x=""760"" y=""160""/><di:waypoint x=""1330"" y=""160""/><di:waypoint x=""1330"" y=""220""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_7_di"" bpmnElement=""Flow_7""><di:waypoint x=""760"" y=""260""/><di:waypoint x=""805"" y=""260""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_8_di"" bpmnElement=""Flow_8""><di:waypoint x=""855"" y=""260""/><di:waypoint x=""1280"" y=""260""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_9_di"" bpmnElement=""Flow_9""><di:waypoint x=""830"" y=""285""/><di:waypoint x=""830"" y=""370""/><di:waypoint x=""900"" y=""370""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_10_di"" bpmnElement=""Flow_10""><di:waypoint x=""1000"" y=""370""/><di:waypoint x=""1045"" y=""370""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_11_di"" bpmnElement=""Flow_11""><di:waypoint x=""1070"" y=""345""/><di:waypoint x=""1070"" y=""260""/><di:waypoint x=""1280"" y=""260""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_12_di"" bpmnElement=""Flow_12""><di:waypoint x=""1070"" y=""395""/><di:waypoint x=""1070"" y=""470""/><di:waypoint x=""1140"" y=""470""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_13_di"" bpmnElement=""Flow_13""><di:waypoint x=""1240"" y=""470""/><di:waypoint x=""1330"" y=""470""/><di:waypoint x=""1330"" y=""300""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_14_di"" bpmnElement=""Flow_14""><di:waypoint x=""1380"" y=""260""/><di:waypoint x=""1420"" y=""260""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_15_di"" bpmnElement=""Flow_15""><di:waypoint x=""1520"" y=""260""/><di:waypoint x=""1560"" y=""260""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_16_di"" bpmnElement=""Flow_16""><di:waypoint x=""1660"" y=""260""/><di:waypoint x=""1702"" y=""260""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg1_di"" bpmnElement=""Flow_Msg1""><di:waypoint x=""218"" y=""570""/><di:waypoint x=""218"" y=""278""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg2_di"" bpmnElement=""Flow_Msg2""><di:waypoint x=""1470"" y=""300""/><di:waypoint x=""1470"" y=""570""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_Msg3_di"" bpmnElement=""Flow_Msg3""><di:waypoint x=""1490"" y=""570""/><di:waypoint x=""1490"" y=""300""/></bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";

    /// <summary>
    /// Employee Onboarding Process - HR workflow for new hires
    /// </summary>
    public static string EmployeeOnboarding => @"<?xml version=""1.0"" encoding=""UTF-8""?>
<bpmn:definitions xmlns:bpmn=""http://www.omg.org/spec/BPMN/20100524/MODEL"" xmlns:bpmndi=""http://www.omg.org/spec/BPMN/20100524/DI"" xmlns:dc=""http://www.omg.org/spec/DD/20100524/DC"" xmlns:di=""http://www.omg.org/spec/DD/20100524/DI"" id=""Definitions_Onboarding"" targetNamespace=""http://mbrhe.gov.ae/bpmn"">
  <bpmn:process id=""Process_Onboarding"" name=""Employee Onboarding"" isExecutable=""false"">
    <bpmn:startEvent id=""Start_1"" name=""Offer Accepted""><bpmn:outgoing>Flow_1</bpmn:outgoing></bpmn:startEvent>
    <bpmn:parallelGateway id=""Gateway_Split"" name=""Parallel Setup""><bpmn:incoming>Flow_1</bpmn:incoming><bpmn:outgoing>Flow_2</bpmn:outgoing><bpmn:outgoing>Flow_3</bpmn:outgoing><bpmn:outgoing>Flow_4</bpmn:outgoing></bpmn:parallelGateway>
    <bpmn:userTask id=""Task_HR"" name=""HR Documentation""><bpmn:incoming>Flow_2</bpmn:incoming><bpmn:outgoing>Flow_5</bpmn:outgoing></bpmn:userTask>
    <bpmn:serviceTask id=""Task_IT"" name=""IT Account Setup""><bpmn:incoming>Flow_3</bpmn:incoming><bpmn:outgoing>Flow_6</bpmn:outgoing></bpmn:serviceTask>
    <bpmn:userTask id=""Task_Facilities"" name=""Workspace Preparation""><bpmn:incoming>Flow_4</bpmn:incoming><bpmn:outgoing>Flow_7</bpmn:outgoing></bpmn:userTask>
    <bpmn:parallelGateway id=""Gateway_Join"" name=""Merge""><bpmn:incoming>Flow_5</bpmn:incoming><bpmn:incoming>Flow_6</bpmn:incoming><bpmn:incoming>Flow_7</bpmn:incoming><bpmn:outgoing>Flow_8</bpmn:outgoing></bpmn:parallelGateway>
    <bpmn:userTask id=""Task_Orientation"" name=""Day 1 Orientation""><bpmn:incoming>Flow_8</bpmn:incoming><bpmn:outgoing>Flow_9</bpmn:outgoing></bpmn:userTask>
    <bpmn:userTask id=""Task_Training"" name=""Department Training""><bpmn:incoming>Flow_9</bpmn:incoming><bpmn:outgoing>Flow_10</bpmn:outgoing></bpmn:userTask>
    <bpmn:userTask id=""Task_Mentor"" name=""Assign Mentor""><bpmn:incoming>Flow_10</bpmn:incoming><bpmn:outgoing>Flow_11</bpmn:outgoing></bpmn:userTask>
    <bpmn:intermediateCatchEvent id=""Event_30Days"" name=""30 Day Check""><bpmn:incoming>Flow_11</bpmn:incoming><bpmn:outgoing>Flow_12</bpmn:outgoing><bpmn:timerEventDefinition/></bpmn:intermediateCatchEvent>
    <bpmn:userTask id=""Task_Review"" name=""Probation Review""><bpmn:incoming>Flow_12</bpmn:incoming><bpmn:outgoing>Flow_13</bpmn:outgoing></bpmn:userTask>
    <bpmn:endEvent id=""End_1"" name=""Onboarding Complete""><bpmn:incoming>Flow_13</bpmn:incoming></bpmn:endEvent>
    <bpmn:sequenceFlow id=""Flow_1"" sourceRef=""Start_1"" targetRef=""Gateway_Split""/>
    <bpmn:sequenceFlow id=""Flow_2"" sourceRef=""Gateway_Split"" targetRef=""Task_HR""/>
    <bpmn:sequenceFlow id=""Flow_3"" sourceRef=""Gateway_Split"" targetRef=""Task_IT""/>
    <bpmn:sequenceFlow id=""Flow_4"" sourceRef=""Gateway_Split"" targetRef=""Task_Facilities""/>
    <bpmn:sequenceFlow id=""Flow_5"" sourceRef=""Task_HR"" targetRef=""Gateway_Join""/>
    <bpmn:sequenceFlow id=""Flow_6"" sourceRef=""Task_IT"" targetRef=""Gateway_Join""/>
    <bpmn:sequenceFlow id=""Flow_7"" sourceRef=""Task_Facilities"" targetRef=""Gateway_Join""/>
    <bpmn:sequenceFlow id=""Flow_8"" sourceRef=""Gateway_Join"" targetRef=""Task_Orientation""/>
    <bpmn:sequenceFlow id=""Flow_9"" sourceRef=""Task_Orientation"" targetRef=""Task_Training""/>
    <bpmn:sequenceFlow id=""Flow_10"" sourceRef=""Task_Training"" targetRef=""Task_Mentor""/>
    <bpmn:sequenceFlow id=""Flow_11"" sourceRef=""Task_Mentor"" targetRef=""Event_30Days""/>
    <bpmn:sequenceFlow id=""Flow_12"" sourceRef=""Event_30Days"" targetRef=""Task_Review""/>
    <bpmn:sequenceFlow id=""Flow_13"" sourceRef=""Task_Review"" targetRef=""End_1""/>
  </bpmn:process>
  <bpmndi:BPMNDiagram id=""BPMNDiagram_1"">
    <bpmndi:BPMNPlane id=""BPMNPlane_1"" bpmnElement=""Process_Onboarding"">
      <bpmndi:BPMNShape id=""Start_1_di"" bpmnElement=""Start_1""><dc:Bounds x=""152"" y=""232"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Split_di"" bpmnElement=""Gateway_Split""><dc:Bounds x=""245"" y=""225"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_HR_di"" bpmnElement=""Task_HR""><dc:Bounds x=""350"" y=""100"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_IT_di"" bpmnElement=""Task_IT""><dc:Bounds x=""350"" y=""210"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Facilities_di"" bpmnElement=""Task_Facilities""><dc:Bounds x=""350"" y=""320"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Gateway_Join_di"" bpmnElement=""Gateway_Join""><dc:Bounds x=""505"" y=""225"" width=""50"" height=""50""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Orientation_di"" bpmnElement=""Task_Orientation""><dc:Bounds x=""610"" y=""210"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Training_di"" bpmnElement=""Task_Training""><dc:Bounds x=""760"" y=""210"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Mentor_di"" bpmnElement=""Task_Mentor""><dc:Bounds x=""910"" y=""210"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Event_30Days_di"" bpmnElement=""Event_30Days""><dc:Bounds x=""1062"" y=""232"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""Task_Review_di"" bpmnElement=""Task_Review""><dc:Bounds x=""1150"" y=""210"" width=""100"" height=""80""/></bpmndi:BPMNShape>
      <bpmndi:BPMNShape id=""End_1_di"" bpmnElement=""End_1""><dc:Bounds x=""1302"" y=""232"" width=""36"" height=""36""/></bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id=""Flow_1_di"" bpmnElement=""Flow_1""><di:waypoint x=""188"" y=""250""/><di:waypoint x=""245"" y=""250""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_2_di"" bpmnElement=""Flow_2""><di:waypoint x=""270"" y=""225""/><di:waypoint x=""270"" y=""140""/><di:waypoint x=""350"" y=""140""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_3_di"" bpmnElement=""Flow_3""><di:waypoint x=""295"" y=""250""/><di:waypoint x=""350"" y=""250""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_4_di"" bpmnElement=""Flow_4""><di:waypoint x=""270"" y=""275""/><di:waypoint x=""270"" y=""360""/><di:waypoint x=""350"" y=""360""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_5_di"" bpmnElement=""Flow_5""><di:waypoint x=""450"" y=""140""/><di:waypoint x=""530"" y=""140""/><di:waypoint x=""530"" y=""225""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_6_di"" bpmnElement=""Flow_6""><di:waypoint x=""450"" y=""250""/><di:waypoint x=""505"" y=""250""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_7_di"" bpmnElement=""Flow_7""><di:waypoint x=""450"" y=""360""/><di:waypoint x=""530"" y=""360""/><di:waypoint x=""530"" y=""275""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_8_di"" bpmnElement=""Flow_8""><di:waypoint x=""555"" y=""250""/><di:waypoint x=""610"" y=""250""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_9_di"" bpmnElement=""Flow_9""><di:waypoint x=""710"" y=""250""/><di:waypoint x=""760"" y=""250""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_10_di"" bpmnElement=""Flow_10""><di:waypoint x=""860"" y=""250""/><di:waypoint x=""910"" y=""250""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_11_di"" bpmnElement=""Flow_11""><di:waypoint x=""1010"" y=""250""/><di:waypoint x=""1062"" y=""250""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_12_di"" bpmnElement=""Flow_12""><di:waypoint x=""1098"" y=""250""/><di:waypoint x=""1150"" y=""250""/></bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id=""Flow_13_di"" bpmnElement=""Flow_13""><di:waypoint x=""1250"" y=""250""/><di:waypoint x=""1302"" y=""250""/></bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>";
}

