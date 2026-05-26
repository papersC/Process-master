using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ESEMS.Web.Services.Notifications;
using ESEMS.Web.Services.Workflow;

namespace ESEMS.Web.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IWorkflowService _workflowService;

    public NotificationsController(INotificationService notificationService, IWorkflowService workflowService)
    {
        _notificationService = notificationService;
        _workflowService = workflowService;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] bool unreadOnly = false, [FromQuery] int take = 20)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var notifications = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly, take);

        // Decorate each notification with the workflow id it can be approved against
        // (if any), so the bell dropdown can show an inline Approve button without a
        // round-trip per item. Resolved against the user's own pending queue —
        // approval is silently absent for items the user can't act on.
        var pending = await _workflowService.GetPendingApprovalsAsync(userId);
        var pendingByEntity = pending
            .GroupBy(w => $"{w.EntityType}:{w.EntityId}")
            .ToDictionary(g => g.Key, g => g.First().Id);

        var payload = notifications.Select(n => new
        {
            n.Id,
            n.UserId,
            n.TitleEn,
            n.TitleAr,
            n.MessageEn,
            n.MessageAr,
            n.Type,
            n.IsRead,
            n.ReadAt,
            n.RelatedEntityId,
            n.RelatedEntityType,
            n.ActionUrl,
            n.CreatedAt,
            ApprovableWorkflowId = (!string.IsNullOrEmpty(n.RelatedEntityType) && !string.IsNullOrEmpty(n.RelatedEntityId)
                && pendingByEntity.TryGetValue($"{n.RelatedEntityType}:{n.RelatedEntityId}", out var wfId))
                ? wfId : null
        });

        return Ok(payload);
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApproveFromNotification(string id)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var notifications = await _notificationService.GetUserNotificationsAsync(userId, false, 200);
        var notif = notifications.FirstOrDefault(n => n.Id == id);
        if (notif == null) return NotFound();
        if (string.IsNullOrEmpty(notif.RelatedEntityId) || string.IsNullOrEmpty(notif.RelatedEntityType))
            return BadRequest(new { error = "Notification has no related entity." });

        var pending = await _workflowService.GetPendingApprovalsAsync(userId);
        var workflow = pending.FirstOrDefault(w =>
            string.Equals(w.EntityType, notif.RelatedEntityType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(w.EntityId, notif.RelatedEntityId, StringComparison.OrdinalIgnoreCase));
        if (workflow == null) return NotFound(new { error = "No pending workflow for this notification." });

        var userName = User.Identity?.Name ?? "Unknown";
        try
        {
            // ProcessActionAsync now enforces governance guards (FLOW-001 self-approval,
            // FLOW-003 dual-level same-person) which throw on violation. Catch them so the
            // inbox gets a clean error instead of a 500.
            await _workflowService.ProcessActionAsync(workflow.Id, userId, userName, "Approved", null);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        await _notificationService.MarkAsReadAsync(id);
        return Ok(new { workflowId = workflow.Id });
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(new { count });
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        // SEC-010: verify the notification belongs to the caller before marking
        // it read. Without this, any authenticated user could mark another
        // user's notifications read by guessing/iterating ids (IDOR). We scope
        // the lookup to the caller's own notifications and 404 if absent — a
        // 404 (rather than 403) avoids confirming the id exists for someone else.
        var notifications = await _notificationService.GetUserNotificationsAsync(userId, false, 500);
        var owns = notifications.Any(n => n.Id == id && n.UserId == userId);
        if (!owns) return NotFound();

        await _notificationService.MarkAsReadAsync(id);
        return Ok();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        await _notificationService.MarkAllAsReadAsync(userId);
        return Ok();
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }
}
