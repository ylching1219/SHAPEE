using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHAPEE.Models;
using System.Security.Claims;

namespace SHAPEE.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController(DB db) : Controller
    {
        // GET: /Staff
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Staff Dashboard";
            return View();    
        }


        //===============KOH KAI MINN================

        public IActionResult ClassSelection(string mode = "take", string? search = null, string? day = null, string? sectionType = null, string? semester = null, string? sort = null)
        {
            ViewData["Title"] = mode == "view"
                ? "Select Class to View Attendance"
                : "Select Class to Take Attendance";
            ViewData["Mode"] = mode;

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var now = DateTime.Now;
            var todayName = now.DayOfWeek.ToString();  
            var todayAbbrev = todayName[..3];                  
            var currentTime = TimeOnly.FromDateTime(now);

            var isView = (mode ?? "take").Equals("view", StringComparison.OrdinalIgnoreCase);

            //base query all sections for this staff
            var sectionsQuery = db.ClassSections
                .Include(c => c.Course)
                .Include(c => c.Room)
                .Include(c => c.Semester)
                .Where(c => c.StaffId == staffId)
                .AsQueryable();

            //only filter by today in TAKE mode, not in VIEW mode
            if (!isView)
            {
                // mode = take (show only today class)
                //convert to lowercase for comparison
                var todayNameLower = todayName.ToLower();
                var todayAbbrevLower = todayAbbrev.ToLower();
                sectionsQuery = sectionsQuery.Where(s =>
                    s.Day.ToLower() == todayNameLower ||
                    s.Day.ToLower().StartsWith(todayAbbrevLower));
            }

            // search filter (only section id, course id ,room id)
            search = search?.Trim() ?? "";
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                sectionsQuery = sectionsQuery.Where(s =>
                    s.SectionId.ToLower().Contains(searchLower) ||
                    (s.Course != null && s.Course.Id.ToLower().Contains(searchLower)) ||
                    (s.Room != null && s.Room.Id.ToLower().Contains(searchLower)));
            }

            // other filters (works for both modes)
            day = day?.Trim() ?? "";
            sectionType = sectionType?.Trim() ?? "";
            semester = semester?.Trim() ?? "";

            // day filter (map full day names to match db data)
            // only apply day filter in VIEW mode (disabled in TAKE mode)
            if (!string.IsNullOrEmpty(day) && isView)
            {
                //map full day names to match db data
                var dayMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Monday", "Mon" },
                    { "Tuesday", "Tues" },
                    { "Wednesday", "Wed" },
                    { "Thursday", "Thurs" },
                    { "Friday", "Fri" },
                    { "Saturday", "Sat" },
                    { "Sunday", "Sun" }
                };

                //get the db data for the selected day
                var dayAbbrev = dayMapping.ContainsKey(day) ? dayMapping[day] : day;
                var dayAbbrevLower = dayAbbrev.ToLower();
                
                var dayLower = day.ToLower();
                
                sectionsQuery = sectionsQuery.Where(s =>
                    s.Day.ToLower() == dayAbbrevLower ||
                    s.Day.ToLower() == dayLower ||
                    s.Day.ToLower().StartsWith(dayAbbrevLower));
            }

            if (!string.IsNullOrEmpty(sectionType))
            {
                sectionsQuery = sectionsQuery.Where(s => s.SectionType == sectionType);
            }

            if (!string.IsNullOrEmpty(semester))
            {
                sectionsQuery = sectionsQuery.Where(s => s.SemesterId == semester);
            }

            // sorting
            sectionsQuery = sort switch
            {
                "1" => sectionsQuery.OrderBy(s => s.SectionId),
                "2" => sectionsQuery.OrderByDescending(s => s.SectionId),
                "3" => sectionsQuery.OrderBy(s => s.Day),
                "4" => sectionsQuery.OrderByDescending(s => s.Day),
                "5" => sectionsQuery.OrderBy(s => s.StartTime),
                "6" => sectionsQuery.OrderByDescending(s => s.StartTime),
                _ => sectionsQuery.OrderBy(s => s.Day).ThenBy(s => s.StartTime)
            };

            var sections = sectionsQuery.ToList();

            List<ClassSelectionItemVM> vm;

            if (isView)
            {
                // mode = view (show all class) no time chedking (no need to check if time has been reached or has passed)
                vm = sections.Select(s => new ClassSelectionItemVM
                {
                    Section = s,
                    CanStart = true,
                    DisabledReason = null
                }).ToList();
            }
            else
            {
                // mode = take (already filtered to today's classes above)
                // show all today classe, but disable button if time not yet reach or passed
                vm = sections.Select(s =>
                {
                    //allow starting if current time is between start time and end time
                    var canStart = currentTime >= s.StartTime && currentTime <= s.EndTime;
                    string? reason = null;
                    
                    if (currentTime < s.StartTime) 
                    {
                        reason = $"Class starts at {s.StartTime:HH:mm}. Please wait until the class time.";
                    }
                    else if (currentTime > s.EndTime) 
                    {
                        reason = $"Class ended at {s.EndTime:HH:mm}.";
                    }

                    return new ClassSelectionItemVM
                    {
                        Section = s,
                        CanStart = canStart,
                        DisabledReason = reason
                    };
                }).ToList();
            }

            if (Request.IsAjax())
            {
                return PartialView("_ClassSelection", vm);
            }

            return View(vm);
        }

        public IActionResult MarkAttendance(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
                return RedirectToAction("ClassSelection");

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var section = db.ClassSections
                .Include(c => c.Course)
                .Include(c => c.Room)
                .Include(c => c.Semester)
                .FirstOrDefault(c => c.SectionId == sectionId && c.StaffId == staffId);

            if (section == null)
                return RedirectToAction("ClassSelection");

            return View(section);
        }

        // generate a simple code for the attendance code (can be used for join code also)
        private static string GenerateSimpleCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rnd = new Random();
            return new string(Enumerable.Range(0, 5)
                .Select(_ => chars[rnd.Next(chars.Length)])
                .ToArray());
        }

        [HttpPost]
        [Authorize(Roles = "Staff")]
        public IActionResult StartAttendanceSession(string sectionId)
        {
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId && s.StaffId == staffId);
            if (section == null) return NotFound();

            section.CurrentQrToken = Guid.NewGuid().ToString("N");
            section.CurrentQrExpiry = DateTime.Now.AddSeconds(120);
            section.IsAttendanceSessionActive = true;

            if (string.IsNullOrEmpty(section.AttendanceCode))
                section.AttendanceCode = GenerateSimpleCode();

            db.SaveChanges();

            return Json(new
            {
                success = true,
                token = section.CurrentQrToken,
                manualCode = section.AttendanceCode
            });
        }

        [HttpPost]
        [Authorize(Roles = "Staff")]
        public IActionResult RefreshQrToken(string sectionId)
        {
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId && s.StaffId == staffId);
            if (section == null || !section.IsAttendanceSessionActive) return BadRequest();

            section.CurrentQrToken = Guid.NewGuid().ToString("N");
            section.CurrentQrExpiry = DateTime.Now.AddSeconds(120);
            db.SaveChanges();

            return Json(new { success = true, token = section.CurrentQrToken });
        }

        [HttpPost]
        [Authorize(Roles = "Staff")]
        public IActionResult RegenerateManualCode(string sectionId)
        {
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId && s.StaffId == staffId);
            if (section == null || !section.IsAttendanceSessionActive) return BadRequest();

            section.AttendanceCode = GenerateSimpleCode();
            db.SaveChanges();

            return Json(new { success = true, manualCode = section.AttendanceCode });
        }

        [HttpPost]
        [Authorize(Roles = "Staff")]
        public IActionResult StopAttendanceSession(string sectionId)
        {
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId && s.StaffId == staffId);
            if (section == null)
                return Json(new { success = false, message = "Section not found" });

            section.IsAttendanceSessionActive = false;
            section.CurrentQrToken = null;
            section.CurrentQrExpiry = null;
            db.SaveChanges();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, redirectUrl = Url.Action("SessionSummary", new { sectionId }) });
            }

            return RedirectToAction("SessionSummary", new { sectionId });
        }

        public IActionResult SessionSummary(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
                return RedirectToAction("ClassSelection", new { mode = "take" });

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateOnly.FromDateTime(DateTime.Now);

            var section = db.ClassSections
                .Include(c => c.Course)
                .Include(c => c.Room)
                .Include(c => c.Semester)
                .FirstOrDefault(c => c.SectionId == sectionId && c.StaffId == staffId);

            if (section == null)
                return RedirectToAction("ClassSelection", new { mode = "take" });

            // Get all enrolled students for this section
            var enrolledStudents = db.Enrollments
                .Include(e => e.Student)
                .Where(e => e.ClassSectionId == sectionId 
                 && e.Type == "Enrolled" 
                 && e.Status == "Approved")
                .Select(e => e.Student)
                .ToList();

            // Get attendance records for today
            var todayAttendances = db.Attendances
                .Include(a => a.Student)
                .Where(a => a.ClassSectionId == sectionId && a.Date == today)
                .ToList();

            var presentStudents = todayAttendances
                .Where(a => a.Status == "Present")
                .Select(a => a.Student)
                .ToList();

            var flaggedAttendances = todayAttendances
                .Where(a => a.IsFlaged)
                .ToList();

            var absentStudents = enrolledStudents
                .Where(s => !todayAttendances.Any(a => a.StudentId == s.Id))
                .ToList();

            var totalEnrolled = enrolledStudents.Count;
            var presentCount = presentStudents.Count;
            var absentCount = absentStudents.Count;
            var flaggedCount = flaggedAttendances.Count;
            var attendancePercentage = totalEnrolled > 0 
                ? Math.Round((double)presentCount / totalEnrolled * 100, 1) 
                : 0;

            var viewModel = new SessionSummaryVM
            {
                Section = section,
                TotalEnrolled = totalEnrolled,
                PresentCount = presentCount,
                AbsentCount = absentCount,
                FlaggedCount = flaggedCount,
                AttendancePercentage = attendancePercentage,
                PresentStudents = presentStudents,
                AbsentStudents = absentStudents,
                FlaggedAttendances = flaggedAttendances,
                Today = today
            };

            ViewData["Title"] = "Session Summary";
            return View(viewModel);
        }

        [HttpGet]
        [Authorize(Roles = "Staff")]
        public IActionResult LiveAttendanceRecord(string sectionId)
        {
            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var today = DateOnly.FromDateTime(DateTime.Now);

            var section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId && s.StaffId == staffId);
            if (section == null) return Unauthorized();

            var items = db.Attendances
                .Include(a => a.Student)
                .Where(a => a.ClassSectionId == sectionId && a.Date == today)
                .OrderByDescending(a => a.RegisteredAt)
                .Take(50) // latest 50 records
                .Select(a => new
                {
                    time = a.RegisteredAt.ToString("HH:mm:ss"),
                    studentName = a.Student.FirstName + " " + a.Student.LastName,
                    source = (a.UserAgent ?? "").StartsWith("QR||") ? "QR Scan"
                        : (a.UserAgent ?? "").StartsWith("MANUAL||") ? "Manual Code"
                        : "Unknown",
                    status = a.Status
                })
                .ToList();

            return Json(new { success = true, items });
        }

        [HttpGet]
        public IActionResult GetAttendanceStatistics(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
            {
                return Json(new { success = false, message = "Section ID is required." });
            }

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var section = db.ClassSections
                .Include(c => c.Course)
                .Include(c => c.Room)
                .Include(c => c.Semester)
                .FirstOrDefault(c => c.SectionId == sectionId && c.StaffId == staffId);

            if (section == null)
            {
                return Json(new { success = false, message = "Section not found." });
            }

            //get all enrolled student for this class section
            var enrolledStudents = db.Enrollments
                .Include(e => e.Student)
                .Where(e => e.ClassSectionId == sectionId 
                 && e.Type == "Enrolled" 
                 && e.Status == "Approved")
                .Select(e => e.Student)
                .ToList();

            var totalStudents = enrolledStudents.Count;

            //get all attendance records for this class section
            var allAttendances = db.Attendances
                .Include(a => a.Student)
                .Where(a => a.ClassSectionId == sectionId)
                .ToList();

            //calculate overall attendance percentage
            var uniqueDates = allAttendances.Select(a => a.Date).Distinct().ToList();
            var totalPossibleAttendances = totalStudents * uniqueDates.Count;
            var totalPresentCount = allAttendances.Count(a => a.Status == "Present");
            var overallAttendancePercentage = totalPossibleAttendances > 0
                ? Math.Round((double)totalPresentCount / totalPossibleAttendances * 100, 1)
                : 0;

            //calculate flaged count
            var flaggedCount = allAttendances.Count(a => a.IsFlaged);
            
            //count total absences this semester
            var semesterStart = section.Semester?.Id != null 
                ? (section.Semester.Id.StartsWith("2025") ? new DateOnly(2025, 1, 1) : DateOnly.FromDateTime(DateTime.Now))
                : DateOnly.FromDateTime(DateTime.Now);
            
            var semesterAttendances = allAttendances.Where(a => a.Date >= semesterStart).ToList();
            var totalAbsences = totalStudents * uniqueDates.Count - semesterAttendances.Count(a => a.Status == "Present");

            return Json(new { 
                success = true,
                totalStudents = totalStudents,
                overallAttendance = overallAttendancePercentage,
                totalAbsences = totalAbsences,
                flaggedCount = flaggedCount
            });
        }


        public IActionResult AttendanceManagement(string sectionId, string? search = null, string? date = null, string? status = null, string? sort = null)
        {
            if (string.IsNullOrEmpty(sectionId))
                return RedirectToAction("ClassSelection", new { mode = "view" });

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var section = db.ClassSections
                .Include(c => c.Course)
                .Include(c => c.Room)
                .Include(c => c.Semester)
                .FirstOrDefault(c => c.SectionId == sectionId && c.StaffId == staffId);

            if (section == null)
                return RedirectToAction("ClassSelection", new { mode = "view" });

            //get all enrolled students for this class section
            var enrolledStudents = db.Enrollments
                .Include(e => e.Student)
                .Where(e => e.ClassSectionId == sectionId 
                 && e.Type == "Enrolled" 
                 && e.Status == "Approved")
                .Select(e => e.Student)
                .ToList();

            var totalStudents = enrolledStudents.Count;

            //get all attendance records for this class section with filters
            var attendancesQuery = db.Attendances
                .Include(a => a.Student)
                .Where(a => a.ClassSectionId == sectionId)
                .AsQueryable();

            //apply search filter
            search = search?.Trim() ?? "";
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                attendancesQuery = attendancesQuery.Where(a =>
                    (a.Student != null && (a.Student.Id.ToLower().Contains(searchLower) ||
                     (a.Student.FirstName != null && a.Student.FirstName.ToLower().Contains(searchLower)) ||
                     (a.Student.LastName != null && a.Student.LastName.ToLower().Contains(searchLower)))));
            }

            //apply date filter
            date = date?.Trim() ?? "";
            if (!string.IsNullOrEmpty(date) && DateOnly.TryParse(date, out var filterDate))
            {
                attendancesQuery = attendancesQuery.Where(a => a.Date == filterDate);
            }

            //apply status filter
            status = status?.Trim() ?? "";
            if (!string.IsNullOrEmpty(status))
            {
                if (status == "Flagged")
                {
                    attendancesQuery = attendancesQuery.Where(a => a.IsFlaged);
                }
                else
                {
                    attendancesQuery = attendancesQuery.Where(a => a.Status == status);
                }
            }

            //apply sorting
            attendancesQuery = sort switch
            {
                "1" => attendancesQuery.OrderBy(a => a.Date),
                "2" => attendancesQuery.OrderByDescending(a => a.Date),
                "3" => attendancesQuery.OrderBy(a => a.Student != null ? a.Student.Id : ""),
                "4" => attendancesQuery.OrderByDescending(a => a.Student != null ? a.Student.Id : ""),
                "5" => attendancesQuery.OrderBy(a => a.RegisteredAt),
                "6" => attendancesQuery.OrderByDescending(a => a.RegisteredAt),
                _ => attendancesQuery.OrderByDescending(a => a.Date).ThenByDescending(a => a.RegisteredAt)
            };

            var filteredAttendances = attendancesQuery.ToList();

            //get all attendances for statistics (before filterd)
            var allAttendances = db.Attendances
                .Include(a => a.Student)
                .Where(a => a.ClassSectionId == sectionId)
                .ToList();

            //calculate overall attendance percentage
            var uniqueDates = allAttendances.Select(a => a.Date).Distinct().ToList();
            var totalPossibleAttendances = totalStudents * uniqueDates.Count;
            var totalPresentCount = allAttendances.Count(a => a.Status == "Present");
            var overallAttendancePercentage = totalPossibleAttendances > 0
                ? Math.Round((double)totalPresentCount / totalPossibleAttendances * 100, 1)
                : 0;

            //calculate flaged count
            var flaggedCount = allAttendances.Count(a => a.IsFlaged);
            
            //count total absences this semester
            var semesterStart = section.Semester?.Id != null 
                ? (section.Semester.Id.StartsWith("2025") ? new DateOnly(2025, 1, 1) : DateOnly.FromDateTime(DateTime.Now))
                : DateOnly.FromDateTime(DateTime.Now);
            
            var semesterAttendances = allAttendances.Where(a => a.Date >= semesterStart).ToList();
            var totalAbsences = totalStudents * uniqueDates.Count - semesterAttendances.Count(a => a.Status == "Present");

            //create view model
            var viewModel = new AttendanceManagementVM
            {
                Section = section,
                TotalStudents = totalStudents,
                AveragePresent = overallAttendancePercentage, //stores overall attendance percentage
                AveragePresentPercentage = (int)overallAttendancePercentage,
                TotalAbsences = totalAbsences,
                FlaggedCount = flaggedCount,
                Attendances = filteredAttendances,
                EnrolledStudents = enrolledStudents
            };

            ViewData["Title"] = "Attendance Management";
            
            if (Request.IsAjax())
            {
                return PartialView("_Attendance", viewModel);
            }
            
            return View(viewModel); 
        }


        public IActionResult AttendanceReport()
        {
            ViewData["Title"] = "Attendance Analytics";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateAttendanceStatus([FromBody] UpdateAttendanceStatusRequest request)
        {
            if (request == null || request.AttendanceIds == null || !request.AttendanceIds.Any())
            {
                return Json(new { success = false, message = "No attendance records selected." });
            }

            if (string.IsNullOrEmpty(request.Status))
            {
                return Json(new { success = false, message = "Status is required." });
            }

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            //verify all attendances belong to class sections owned by this staff
            var attendances = db.Attendances
                .Include(a => a.ClassSection)
                .Where(a => request.AttendanceIds.Contains(a.Id) && a.ClassSection.StaffId == staffId)
                .ToList();

            if (attendances.Count != request.AttendanceIds.Count)
            {
                return Json(new { success = false, message = "Some attendance records were not found or you don't have permission to update them." });
            }

            //update status for all selected attendances
            foreach (var attendance in attendances)
            {
                attendance.Status = request.Status;
            }

            try
            {
                db.SaveChanges();
                return Json(new { 
                    success = true, 
                    message = $"Successfully updated {attendances.Count} attendance record(s) to {request.Status}." 
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error updating attendance: {ex.Message}" });
            }
        }

        //request model for update attendance status
        public class UpdateAttendanceStatusRequest
        {
            public List<string> AttendanceIds { get; set; } = new();
            public string Status { get; set; } = "";
        }
    }
}