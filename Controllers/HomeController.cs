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
    //apply approved leave to attendance
    private void ApplyApprovedLeaveToAttendance(string studentId, DateOnly startDate, DateOnly endDate)
    {
        var student = db.Students.FirstOrDefault(s => s.Id == studentId);
        if (student == null || !student.TutorialGroupId.HasValue)
            return;

        var tutorialGroupId = student.TutorialGroupId.Value;

        //all sections this tutorial group attend
        var sections = db.ClassSections
            .Include(cs => cs.Semester)
            .Where(cs => cs.TutorialGroupId == tutorialGroupId)
            .ToList();

        if (!sections.Any())
            return;

        //day mapping for section
        var dayMapping = new Dictionary<DayOfWeek, string>
        {
            { DayOfWeek.Monday,    "Mon"  },
            { DayOfWeek.Tuesday,   "Tues" },
            { DayOfWeek.Wednesday, "Wed"  },
            { DayOfWeek.Thursday,  "Thurs"},
            { DayOfWeek.Friday,    "Fri"  },
            { DayOfWeek.Saturday,  "Sat"  },
            { DayOfWeek.Sunday,    "Sun"  }
        };

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var dateTime = date.ToDateTime(TimeOnly.MinValue);
            var todayName = dateTime.DayOfWeek.ToString(); // e.g. "Monday"
            var todayNameLower = todayName.ToLower();
            var todayAbbrevLower = todayNameLower.Substring(0, 3); // "mon"

            var sectionsForDay = sections.Where(cs =>
            {
                if (cs.Semester == null || string.IsNullOrEmpty(cs.Day))
                    return false;

                // semester window (DateOnly -> DateOnly comparison)
                var semStart = cs.Semester.StartDate;
                var semEnd   = cs.Semester.EndDate;
                if (date < semStart || date > semEnd)
                    return false;

                var dayLower = cs.Day.ToLower();

                // match full name or abbreviation, same as ClassSelection
                return dayLower == todayNameLower || dayLower.StartsWith(todayAbbrevLower);
            }).ToList();

            if (!sectionsForDay.Any())
                continue;

            foreach (var section in sectionsForDay)
            {
                var sectionId = section.SectionId;

                var att = db.Attendances
                    .FirstOrDefault(a =>
                        a.ClassSectionId == sectionId &&
                        a.StudentId == studentId &&
                        a.Date == date);

                if (att == null)
                {
                    db.Attendances.Add(new Attendance
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        ClassSectionId = sectionId,
                        StudentId = studentId,
                        Date = date,
                        RegisteredAt = DateTime.Now,
                        Status = "Leave",
                        IpAddress = null,
                        UserAgent = "LEAVE||SYSTEM",
                        IsFlaged = false,
                        FlagReason = "Approved leave"
                    });
                }
                else
                {
                    // if you want to convert both Absent and Present to Leave:
                    if (att.Status != "Leave")
                    {
                        att.Status = "Leave";
                        att.FlagReason = "Approved leave (updated from " + att.Status + ")";
                    }
                }
            }
        }

        db.SaveChanges();
    }
}