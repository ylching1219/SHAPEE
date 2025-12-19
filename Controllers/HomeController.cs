using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SHAPEE.Models;
using System.Security.Claims;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace SHAPEE.Controllers;

public class HomeController(DB db) : Controller
{
    // GET: Home/Index
    public IActionResult Index()
    {
        var items = db.Students;
        return View(items);
    }

    // JS
    // Leave Application Form
    [Authorize(Roles = "Student, Staff")]
    public IActionResult LeaveApplication()
    {
        ViewData["Title"] = "Leave Application";

        var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };

        ViewBag.LeaveTypes = leaveTypes;

        return View();
    }

    // POST: Submit Leave Application
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Student, Staff")]
    public async Task<IActionResult> SubmitLeaveApplication(LeaveApplicationVM vm, IFormFile? supportDocument)
    {
        // Remove model state for file upload since it's optional (but we'll validate it separately)
        ModelState.Remove(nameof(supportDocument));

        // Server-side validation
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please fill in all required fields correctly.";

            // Repopulate dropdown
            var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };
            ViewBag.LeaveTypes = leaveTypes;

            return View("LeaveApplication", vm);
        }

        // Validate start date is not in the past
        if (vm.StartDate < DateOnly.FromDateTime(DateTime.Today))
        {
            TempData["ErrorMessage"] = "Start date cannot be in the past.";

            // Repopulate dropdown
            var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };
            ViewBag.LeaveTypes = leaveTypes;

            return View("LeaveApplication", vm);
        }

        // Validate end date is not before start date
        if (vm.EndDate < vm.StartDate)
        {
            TempData["ErrorMessage"] = "End date cannot be before start date.";

            // Repopulate dropdown
            var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };
            ViewBag.LeaveTypes = leaveTypes;

            return View("LeaveApplication", vm);
        }

        // Validate date range (e.g., max 30 days)
        var duration = vm.EndDate.DayNumber - vm.StartDate.DayNumber + 1;
        if (duration > 30)
        {
            TempData["ErrorMessage"] = "Leave duration cannot exceed 30 days.";

            // Repopulate dropdown
            var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };
            ViewBag.LeaveTypes = leaveTypes;

            return View("LeaveApplication", vm);
        }

        // Get current user ID
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            TempData["ErrorMessage"] = "User not authenticated. Please log in.";
            return RedirectToAction("Login", "Account");
        }

        // NEW: Check for overlapping leave applications
        var hasOverlappingLeave = await db.LeaveApplications
            .Where(la => la.UserId == userId
                      && la.Status != "Rejected"
                      && la.Status != "Cancelled")
            .AnyAsync(la =>
                // Check if the new leave period overlaps with existing leave
                (vm.StartDate <= la.EndDate && vm.EndDate >= la.StartDate)
            );

        if (hasOverlappingLeave)
        {
            TempData["ErrorMessage"] = "You already have a leave application for the selected dates. Please check your leave history and choose different dates.";

            // Repopulate dropdown
            var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };
            ViewBag.LeaveTypes = leaveTypes;

            return View("LeaveApplication", vm);
        }

        // CRITICAL: Since database requires document, we must validate that a file is uploaded
        if (supportDocument == null || supportDocument.Length == 0)
        {
            TempData["ErrorMessage"] = "Supporting document is required for all leave applications.";

            // Repopulate dropdown
            var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };
            ViewBag.LeaveTypes = leaveTypes;

            return View("LeaveApplication", vm);
        }

        // Handle file upload (now required)
        string documentUrl;

        // Validate file type
        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
        var fileExtension = Path.GetExtension(supportDocument.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(fileExtension))
        {
            TempData["ErrorMessage"] = "Invalid file type. Only PDF, images (JPG, PNG), and Word documents are allowed.";

            // Repopulate dropdown
            var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };
            ViewBag.LeaveTypes = leaveTypes;

            return View("LeaveApplication", vm);
        }

        // Validate file size (5MB limit)
        const int maxFileSize = 5 * 1024 * 1024; // 5MB
        if (supportDocument.Length > maxFileSize)
        {
            TempData["ErrorMessage"] = "File size exceeds 5MB limit.";

            // Repopulate dropdown
            var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };
            ViewBag.LeaveTypes = leaveTypes;

            return View("LeaveApplication", vm);
        }

        try
        {
            // Create uploads directory if it doesn't exist
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "leave-documents");
            Directory.CreateDirectory(uploadsFolder);

            // Generate unique filename
            var uniqueFileName = $"{userId}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Save file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await supportDocument.CopyToAsync(fileStream);
            }

            documentUrl = $"/uploads/leave-documents/{uniqueFileName}";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to upload document: {ex.Message}";

            // Repopulate dropdown
            var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };
            ViewBag.LeaveTypes = leaveTypes;

            return View("LeaveApplication", vm);
        }

        // Create leave application entity
        var leaveApplication = new LeaveApplication
        {
            UserId = userId,
            StartDate = vm.StartDate,
            EndDate = vm.EndDate,
            LeaveType = vm.LeaveType!,
            Reason = vm.Reason!.Trim(),
            SupportDocumentUrl = documentUrl,
            ApplicationDate = DateOnly.FromDateTime(DateTime.Today),
            Status = "Pending",
            RejectReason = null
        };

        try
        {
            db.LeaveApplications.Add(leaveApplication);
            await db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Leave application for {duration} day{(duration != 1 ? "s" : "")} submitted successfully. You will be notified once reviewed.";

            // Redirect to same action to prevent URL change and form resubmission
            return RedirectToAction(nameof(LeaveApplication));
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to submit leave application: {ex.Message}";

            // Repopulate dropdown
            var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };
            ViewBag.LeaveTypes = leaveTypes;

            return View("LeaveApplication", vm);
        }
    }

    // GET: View Leave Applications History
    public IActionResult LeaveHistory()
    {
        ViewData["Title"] = "Leave Application History";

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            TempData["ErrorMessage"] = "User not authenticated.";
            return RedirectToAction("Login", "Account");
        }

        // Get all leave applications for current user, ordered by most recent
        var leaveApplications = db.LeaveApplications
            .Where(la => la.UserId == userId)
            .OrderByDescending(la => la.ApplicationDate)
            .ToList();

        if (Request.IsAjax())
        {
            return PartialView("_Leave", leaveApplications);
        }

        return View(leaveApplications);
    }

    // POST: Cancel Leave Application (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Student, Staff")]
    public async Task<IActionResult> CancelLeaveApplication(int id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                // Check if it's an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "User not authenticated." });
                }
                TempData["ErrorMessage"] = "User not authenticated.";
                return RedirectToAction("Login", "Account");
            }

            var leaveApplication = db.LeaveApplications
                .FirstOrDefault(la => la.Id == id && la.UserId == userId);

            if (leaveApplication == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Leave application not found." });
                }
                TempData["ErrorMessage"] = "Leave application not found.";
                return RedirectToAction(nameof(LeaveHistory));
            }

            // Only allow cancellation of pending applications
            if (leaveApplication.Status != "Pending")
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = $"Cannot cancel a leave application that is already {leaveApplication.Status.ToLower()}." });
                }
                TempData["ErrorMessage"] = $"Cannot cancel a leave application that is already {leaveApplication.Status.ToLower()}.";
                return RedirectToAction(nameof(LeaveHistory));
            }

            // Update status to Cancelled
            leaveApplication.Status = "Cancelled";
            leaveApplication.RejectReason = "Cancelled by user";

            await db.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "Leave application cancelled successfully." });
            }

            TempData["SuccessMessage"] = "Leave application cancelled successfully.";
            return RedirectToAction(nameof(LeaveHistory));
        }
        catch (Exception ex)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message = $"Error cancelling application: {ex.Message}" });
            }
            TempData["ErrorMessage"] = $"Failed to cancel leave application: {ex.Message}";
            return RedirectToAction(nameof(LeaveHistory));
        }
    }

    //KM 
        // ========================
    // NOTIFICATION MODULE
    // ========================

    // GET: Home/Notifications
    [Authorize]
    public IActionResult Notifications(string filter = "all", string? type = null, string? search = null, string? sort = null)
    {
        ViewData["Title"] = "Notifications";
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Account");

        var query = db.Notification
            .Where(n => n.UserId == userId)
            .AsQueryable();

        // Status filter
        if (filter == "unread")
            query = query.Where(n => !n.IsRead);
        else if (filter == "read")
            query = query.Where(n => n.IsRead);

        // Type filter
        if (!string.IsNullOrEmpty(type))
            query = query.Where(n => n.Type == type);

        // Search filter
        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(n => 
                n.Title.ToLower().Contains(searchLower) || 
                n.Message.ToLower().Contains(searchLower));
        }

        // Sort
        if (sort == "oldest")
            query = query.OrderBy(n => n.CreatedAt);
        else
            query = query.OrderByDescending(n => n.CreatedAt);

        var notifications = query.ToList();
        var unreadCount = db.Notification.Count(n => n.UserId == userId && !n.IsRead);

        ViewBag.Filter = filter;
        ViewBag.Type = type;
        ViewBag.Search = search;
        ViewBag.Sort = sort ?? "newest";
        ViewBag.UnreadCount = unreadCount;
        ViewBag.TotalCount = notifications.Count;

        return View("~/Views/Shared/Notifications.cshtml", notifications);
    }

    // GET: Home/GetUnreadCount (AJAX)
    [HttpGet]
    [Authorize]
    public IActionResult GetUnreadCount()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Json(new { count = 0 });

        var count = db.Notification.Count(n => n.UserId == userId && !n.IsRead);
        return Json(new { count });
    }

    // GET: Home/GetLatestNotifications (AJAX)
    [HttpGet]
    [Authorize]
    public IActionResult GetLatestNotifications(int count = 5, string filter = "all")
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Json(new { notifications = new List<object>(), totalCount = 0 });

        var query = db.Notification
            .Where(n => n.UserId == userId)
            .AsQueryable();

        if (filter == "unread")
            query = query.Where(n => !n.IsRead);

        // Get total count before limiting
        var totalCount = query.Count();

        var notifications = query
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .ToList();

        var result = notifications.Select(n => new
        {
            notificationId = n.NotificationId,
            title = n.Title,
            message = n.Message,
            type = n.Type,
            isRead = n.IsRead,
            actionUrl = n.ActionUrl,
            createdAt = n.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss")
        }).ToList();

        return Json(new { notifications = result, totalCount = totalCount });
    }

    // POST: Home/MarkAsRead (AJAX)
    [HttpPost]
    [Authorize]
    public IActionResult MarkAsRead([FromBody] MarkAsReadRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, message = "Unauthorized" });

        var notification = db.Notification
            .FirstOrDefault(n => n.NotificationId == request.NotificationId && n.UserId == userId);

        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            db.SaveChanges();
        }

        return Json(new { success = true });
    }

    // POST: Home/MarkAllAsRead (AJAX)
    [HttpPost]
    [Authorize]
    public IActionResult MarkAllAsRead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, message = "Unauthorized" });

        var unreadNotifications = db.Notification
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToList();

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
        }

        db.SaveChanges();
        return Json(new { success = true });
    }

    // GET: Home/GetFilteredNotifications (AJAX)
    [HttpGet]
    [Authorize]
    public IActionResult GetFilteredNotifications(string filter = "all", string? type = null, string? search = null, string? sort = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Json(new { success = false, message = "Unauthorized" });

        var query = db.Notification
            .Where(n => n.UserId == userId)
            .AsQueryable();

        // Status filter
        if (filter == "unread")
            query = query.Where(n => !n.IsRead);
        else if (filter == "read")
            query = query.Where(n => n.IsRead);

        // Type filter
        if (!string.IsNullOrEmpty(type))
            query = query.Where(n => n.Type == type);

        // Search filter
        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(n =>
                n.Title.ToLower().Contains(searchLower) ||
                n.Message.ToLower().Contains(searchLower));
        }

        // Sort
        if (sort == "oldest")
            query = query.OrderBy(n => n.CreatedAt);
        else
            query = query.OrderByDescending(n => n.CreatedAt);

        var notifications = query.ToList();
        var unreadCount = db.Notification.Count(n => n.UserId == userId && !n.IsRead);

        var result = notifications.Select(n => new
        {
            notificationId = n.NotificationId,
            title = n.Title,
            message = n.Message,
            type = n.Type,
            isRead = n.IsRead,
            actionUrl = n.ActionUrl ?? "#",
            createdAt = n.CreatedAt,
            timeAgo = GetRelativeTime(n.CreatedAt)
        }).ToList();

        return Json(new
        {
            success = true,
            notifications = result,
            totalCount = notifications.Count,
            unreadCount = unreadCount
        });
    }

    private string GetRelativeTime(DateTime dateTime)
    {
        var now = DateTime.Now;
        var diff = now - dateTime;
        var diffSec = (int)diff.TotalSeconds;
        var diffMin = (int)diff.TotalMinutes;
        var diffHour = (int)diff.TotalHours;
        var diffDay = (int)diff.TotalDays;

        if (diffSec < 60) return "just now";
        if (diffMin < 60) return $"{diffMin}m ago";
        if (diffHour < 24) return $"{diffHour}h ago";
        if (diffDay < 7) return $"{diffDay}d ago";
        return dateTime.ToString("MMM dd, yyyy");
    }

    //model for marking a notification as read
    public class MarkAsReadRequest
    {
        public int NotificationId { get; set; }
    }

    //method to create notification for a user (can be called from anywhere)
    public static void CreateNotificationForUser(DB dbContext, string userId, string title, string message, string type, string? actionUrl = null)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(message))
            return;

        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.Now
        };

        dbContext.Notification.Add(notification);
        dbContext.SaveChanges();
    }


}