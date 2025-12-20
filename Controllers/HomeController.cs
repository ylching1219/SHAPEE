using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SHAPEE.Models;
using System.Globalization;
using System.Linq;
using System.Security.Claims;

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

    // NEW: Find which semester the leave falls into
    var affectedSemester = await db.Semesters
        .FirstOrDefaultAsync(s => vm.StartDate >= s.StartDate && vm.StartDate <= s.EndDate);

    if (affectedSemester == null)
    {
        TempData["ErrorMessage"] = "The selected start date does not fall within any active semester period. Please select a valid date.";
        var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };
        ViewBag.LeaveTypes = leaveTypes;
        return View("LeaveApplication", vm);
    }

    // NEW: Check total leave days in this semester (excluding rejected/cancelled)
    var existingLeavesInSemester = await db.LeaveApplications
        .Where(la => la.UserId == userId
                  && la.Status != "Rejected"
                  && la.Status != "Cancelled"
                  && la.StartDate >= affectedSemester.StartDate
                  && la.StartDate <= affectedSemester.EndDate)
        .ToListAsync();

    // Calculate total existing leave days in this semester
    var totalExistingDays = existingLeavesInSemester.Sum(la => 
        (la.EndDate.DayNumber - la.StartDate.DayNumber + 1));

    // Check if adding this leave would exceed 7 days limit
    if (totalExistingDays + duration > 7)
    {
        var remainingDays = 7 - totalExistingDays;
        TempData["ErrorMessage"] = $"Leave limit exceeded! You can only apply for a maximum of 7 days of leave per semester. " +
                                  $"You have already applied for {totalExistingDays} day{(totalExistingDays != 1 ? "s" : "")} " +
                                  $"in semester {affectedSemester.Id}. " +
                                  $"You have {remainingDays} day{(remainingDays != 1 ? "s" : "")} remaining.";
        var leaveTypes = new List<SelectListItem>
        {
            new SelectListItem { Value = "Normal", Text = "Normal" },
            new SelectListItem { Value = "Medical", Text = "Medical" },
            new SelectListItem { Value = "Emergency", Text = "Emergency" }
        };
        ViewBag.LeaveTypes = leaveTypes;
        return View("LeaveApplication", vm);
    }

    // Check for overlapping leave applications
    var hasOverlappingLeave = await db.LeaveApplications
        .Where(la => la.UserId == userId
                  && la.Status != "Rejected"
                  && la.Status != "Cancelled")
        .AnyAsync(la =>
            (vm.StartDate <= la.EndDate && vm.EndDate >= la.StartDate)
        );

    if (hasOverlappingLeave)
    {
        TempData["ErrorMessage"] = "You already have a leave application for the selected dates. Please check your leave history and choose different dates.";
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

            //notify all admins when a student or staff submits a leave application
            var user = db.Users.FirstOrDefault(u => u.Id == userId);
            if (user is Student student)
            {
                var studentName = student.FullName?.Trim() ?? student.Id;
                CreateNotificationForAllAdmins(
                    db,
                    "New Leave Application",
                    $"Student {studentName} ({student.Id}) has submitted a new leave application for {duration} day{(duration != 1 ? "s" : "")} ({vm.StartDate:dd MMM yyyy} - {vm.EndDate:dd MMM yyyy}).",
                    "info",
                    "/Admin/LeaveManagement"
                );
            }
            else if (user is Staff staff)
            {
                var staffName = staff.FullName?.Trim() ?? staff.Id;
                CreateNotificationForAllAdmins(
                    db,
                    "New Leave Application",
                    $"Staff {staffName} ({staff.Id}) has submitted a new leave application for {duration} day{(duration != 1 ? "s" : "")} ({vm.StartDate:dd MMM yyyy} - {vm.EndDate:dd MMM yyyy}).",
                    "info",
                    "/Admin/LeaveManagement"
                );
            }

        TempData["SuccessMessage"] = $"Leave application for {duration} day{(duration != 1 ? "s" : "")} submitted successfully. You will be notified once reviewed.";

        // Redirect to same action to prevent URL change and form resubmission
        return RedirectToAction(nameof(LeaveApplication));
    }
    catch (Exception ex)
    {
        TempData["ErrorMessage"] = $"Failed to submit leave application: {ex.Message}";
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

    // GET: Announcement Management
    [Authorize(Roles = "Staff, Admin")]
    public IActionResult AnnouncementManagement(string? search, string? sort)
    {
        ViewData["Title"] = "Announcement Management";

        var query = db.Announcements.AsQueryable();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var staff = db.Staffs
            .Include(s => s.Faculty)
            .FirstOrDefault(a => a.Id == userId);


        // Apply search filter
        search = search?.Trim() ?? "";
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(a =>
                a.Title.Contains(search) ||
                a.Content.Contains(search) ||
                a.Author.Contains(search));
        }

        // Apply sorting
        query = sort switch
        {
            "oldest" => query.OrderBy(a => a.CreatedAt),
            "title_asc" => query.OrderBy(a => a.Title),
            "title_desc" => query.OrderByDescending(a => a.Title),
            "author_asc" => query.OrderBy(a => a.Author),
            "author_desc" => query.OrderByDescending(a => a.Author),
            _ => query.OrderByDescending(a => a.CreatedAt) // newest first (default)
        };

        var announcements = query.ToList();

        var vm = new AnnouncementManageVM
        {
            Announcements = announcements,
            Staff = staff
        };

        // Check if it's an AJAX request using the same method as StudentController
        if (Request.IsAjax())
        {
            return PartialView("_AnnouncementTable", vm);
        }

        return View(vm);
    }

    // POST: Create Announcement
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Staff, Admin")]
    public async Task<IActionResult> CreateAnnouncement(string title, string content, bool useFacultyName = false)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(title))
            {
                return Json(new { success = false, message = "Title is required." });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, message = "Content is required." });
            }

            if (title.Length > 100)
            {
                return Json(new { success = false, message = "Title cannot exceed 100 characters." });
            }

            if (content.Length > 500)
            {
                return Json(new { success = false, message = "Content cannot exceed 500 characters." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string authorName;

            if (User.IsInRole("Admin"))
            {
                authorName = "Admin";
            }
            else if (User.IsInRole("Staff"))
            {
                var staff = await db.Staffs
                    .Include(s => s.Faculty)
                    .FirstOrDefaultAsync(s => s.Id == userId);

                if (staff == null)
                {
                    return Json(new { success = false, message = "Staff not found." });
                }

                // Use faculty name if checkbox is checked and faculty exists
                if (useFacultyName && staff.Faculty != null)
                {
                    authorName = staff.Faculty.Name;
                }
                else
                {
                    authorName = staff.FullName.Trim();
                }
            }
            else
            {
                return Json(new { success = false, message = "Unauthorized role." });
            }

            // Generate new announcement ID
            var lastAnnouncement = await db.Announcements
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync();

            string newId;
            if (lastAnnouncement == null || lastAnnouncement.Id.Length < 2)
            {
                newId = "A001";
            }
            else
            {
                var numPart = lastAnnouncement.Id.Substring(1);
                if (int.TryParse(numPart, out int lastNum))
                {
                    newId = $"A{(lastNum + 1):D3}";
                }
                else
                {
                    newId = "A001";
                }
            }

            var announcement = new Announcement
            {
                Id = newId,
                Title = title.Trim(),
                Content = content.Trim(),
                Author = authorName,
                CreatedAt = DateTime.Now,
                UpdatedAt = null
            };

            db.Announcements.Add(announcement);
            await db.SaveChangesAsync();

            //notify all students about the new announcement
            CreateNotificationForAllStudents(
                db,
                "New Announcement",
                $"New announcement: {title}",
                "info",
                "/Student/Announcements"
            );

            return Json(new { success = true, message = "Announcement created successfully!" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error creating announcement: {ex.Message}" });
        }
    }

    // POST: Edit Announcement
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Staff, Admin")]
    public async Task<IActionResult> EditAnnouncement(string id, string title, string content, bool useFacultyName = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return Json(new { success = false, message = "Title is required." });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, message = "Content is required." });
            }

            if (title.Length > 100)
            {
                return Json(new { success = false, message = "Title cannot exceed 100 characters." });
            }

            if (content.Length > 500)
            {
                return Json(new { success = false, message = "Content cannot exceed 500 characters." });
            }

            var announcement = await db.Announcements.FindAsync(id);
            if (announcement == null)
            {
                return Json(new { success = false, message = "Announcement not found." });
            }

            // Update only title and content, preserve the original author
            announcement.Title = title.Trim();
            announcement.Content = content.Trim();
            announcement.UpdatedAt = DateTime.Now;
            // DO NOT change announcement.Author

            db.Announcements.Update(announcement);
            await db.SaveChangesAsync();

            return Json(new { success = true, message = "Announcement updated successfully!" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error updating announcement: {ex.Message}" });
        }
    }

    [HttpGet]
    [Authorize(Roles = "Staff, Admin")]
    public IActionResult GetEditAnnouncementForm(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var staff = db.Staffs
            .Include(s => s.Faculty)
            .FirstOrDefault(a => a.Id == userId);
        var announcement = db.Announcements.FirstOrDefault(a => a.Id == id);

        if (announcement == null)
        {
            return Content("<div class='alert alert-danger m-3'>Announcement not found</div>");
        }

        var vm = new AnnouncementManageVM
        {
            EditAnnouncement = announcement,
            Staff = staff
        };

        return PartialView("_AnnouncementForm", vm);
    }

    // POST: Delete Announcement
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Staff, Admin")]
    public async Task<IActionResult> DeleteAnnouncement(string id)
    {
        try
        {
            var announcement = await db.Announcements.FindAsync(id);

            if (announcement == null)
            {
                TempData["ErrorMessage"] = "Announcement not found.";
                return RedirectToAction(nameof(AnnouncementManagement));
            }

            var announcementTitle = announcement.Title;

            db.Announcements.Remove(announcement);
            await db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Announcement '{announcementTitle}' deleted successfully!";
            return RedirectToAction(nameof(AnnouncementManagement));
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error deleting announcement: {ex.Message}";
            return RedirectToAction(nameof(AnnouncementManagement));
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

    //method to create notifications for all students in tutorial groups associated with a course
    //Uses bulk insert for better performance
    public static void CreateNotificationForCourseStudents(DB dbContext, string courseId, string title, string message, string type, string? actionUrl = null)
    {
        if (string.IsNullOrEmpty(courseId) || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(message))
            return;

        try
        {
            //get all class sections for this course that have tutorial groups
            var tutorialGroupIds = dbContext.ClassSections
                .Where(cs => cs.CourseId == courseId && cs.TutorialGroupId.HasValue)
                .Select(cs => cs.TutorialGroupId.Value)
                .Distinct()
                .ToList();

            if (!tutorialGroupIds.Any())
                return; //no tutorial groups found for this course

            //get all students in these tutorial groups
            var studentIds = dbContext.Students
                .Where(s => s.TutorialGroupId.HasValue && tutorialGroupIds.Contains(s.TutorialGroupId.Value))
                .Select(s => s.Id)
                .ToList();

            if (!studentIds.Any())
                return; //no students found in these tutorial groups

            //use transaction for data integrity
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                //create notifications for all students in bulk
                var notifications = studentIds.Select(studentId => new Notification
                {
                    UserId = studentId,
                    Title = title,
                    Message = message,
                    Type = type,
                    ActionUrl = actionUrl,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                }).ToList();

                //bulk insert - much more efficient than individual inserts
                dbContext.Notification.AddRange(notifications);
                dbContext.SaveChanges();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw; 
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating notifications: {ex.Message}");
        }
    }

    //method to create notifications for ALL students in the system
    public static void CreateNotificationForAllStudents(DB dbContext, string title, string message, string type, string? actionUrl = null)
    {
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(message))
            return;

        try
        {
            //get all active students
            var studentIds = dbContext.Students
                .Select(s => s.Id)
                .ToList();

            if (!studentIds.Any())
                return;

            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                var notifications = studentIds.Select(studentId => new Notification
                {
                    UserId = studentId,
                    Title = title,
                    Message = message,
                    Type = type,
                    ActionUrl = actionUrl,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                }).ToList();

                dbContext.Notification.AddRange(notifications);
                dbContext.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating notifications for all students: {ex.Message}");
        }
    }

    //method to create notifications for ALL admins in the system
    public static void CreateNotificationForAllAdmins(DB dbContext, string title, string message, string type, string? actionUrl = null)
    {
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(message))
            return;

        try
        {
            //get all admins
            var adminIds = dbContext.Admins
                .Select(a => a.Id)
                .ToList();

            if (!adminIds.Any())
                return;

            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                var notifications = adminIds.Select(adminId => new Notification
                {
                    UserId = adminId,
                    Title = title,
                    Message = message,
                    Type = type,
                    ActionUrl = actionUrl,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                }).ToList();

                dbContext.Notification.AddRange(notifications);
                dbContext.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating notifications for all admins: {ex.Message}");
        }
    }


}