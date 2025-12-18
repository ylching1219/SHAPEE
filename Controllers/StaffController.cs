using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHAPEE.Models;
using System.Security.Claims;
using System.Text;

namespace SHAPEE.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController(DB db) : Controller
    {
        // GET: /Staff
        public async Task<IActionResult> Dashboard()
        {
            await LoadProfilePicture();
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

            if (!isView)
            {
                sectionsQuery = sectionsQuery.Where(s => s.Semester != null && s.Semester.IsActive);
            }

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
                    (s.Course != null && s.Course.Name.ToLower().Contains(searchLower)) ||
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
            
            // prevent taking attendance for not active semester
            if (section.Semester == null || !section.Semester.IsActive)
            {
                TempData["ErrorMessage"] = "You can only take attendance for classes in an active semester.";
                return RedirectToAction("ClassSelection", new { mode = "take" });
            }

            var now = DateTime.Now;
            var todayName = now.DayOfWeek.ToString();   // e.g. "Monday"
            var todayAbbrev = todayName[..3];           // "Mon"
            var nowTime = TimeOnly.FromDateTime(now);

            var dayLower = (section.Day ?? "").ToLower();
            var isToday = dayLower == todayName.ToLower()
                    || dayLower.StartsWith(todayAbbrev.ToLower());

            var isWithinTime = nowTime >= section.StartTime && nowTime <= section.EndTime;

            if (!isToday || !isWithinTime)
            {
                TempData["ErrorMessage"] = $"You can only start this session on {section.Day} between {section.StartTime:HH:mm} and {section.EndTime:HH:mm}.";
                return RedirectToAction("ClassSelection", new { mode = "take" });
            }
            
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
            var section = db.ClassSections
                .Include(s => s.Semester)
                .FirstOrDefault(s => s.SectionId == sectionId && s.StaffId == staffId);
            if (section == null) return NotFound();

            // prevent starting sessions for not active semester
            if (section.Semester == null || !section.Semester.IsActive)
            {
                return Json(new { success = false, message = "Cannot start attendance for a class in an inactive semester." });
            }

            var now = DateTime.Now;
            var todayName = now.DayOfWeek.ToString();
            var nowTime = TimeOnly.FromDateTime(now);

            var isToday = string.Equals(section.Day, todayName, StringComparison.OrdinalIgnoreCase);
            var isWithinTime = nowTime >= section.StartTime && nowTime <= section.EndTime;

            if (!isToday || !isWithinTime)
            {
                return Json(new { success = false, message = "Attendance session can only be started during the scheduled class time." });
            }

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

            var today = DateOnly.FromDateTime(DateTime.Now);
            AutoMarkAbsents(sectionId, today, staffId);

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

            // Get all students in the same tutorial group as this section
            var enrolledStudents = db.Students
                .Where(s => s.TutorialGroupId.HasValue
                    && s.TutorialGroupId.Value == section.TutorialGroupId)
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

            var absentStudents = todayAttendances
                .Where(a => a.Status == "Absent")
                .Select(a => a.Student)
                .ToList();

            var totalEnrolled = enrolledStudents.Count;
            var presentCount = presentStudents.Count;
            var absentCount = absentStudents.Count;
            var flaggedCount = flaggedAttendances.Count;
            var consideredTotal = presentCount + absentCount;
            var attendancePercentage = consideredTotal > 0
                ? Math.Round((double)presentCount / consideredTotal * 100, 1)
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
    
        //auto mark absent if student not present after class ends
        private void AutoMarkAbsents(string sectionId, DateOnly date, string staffId)
        {
            var section = db.ClassSections
                .FirstOrDefault(c => c.SectionId == sectionId && c.StaffId == staffId);
            if (section == null) return;

            if (section.Semester == null || !section.Semester.IsActive)
            return;

            var enrolledStudents = db.Students
                .Where(s => s.TutorialGroupId.HasValue
                    && s.TutorialGroupId.Value == section.TutorialGroupId)
                .ToList();
            if (!enrolledStudents.Any()) return;

            var existingIds = db.Attendances
                .Where(a => a.ClassSectionId == sectionId && a.Date == date)
                .Select(a => a.StudentId)
                .ToList();

            var absentStudents = enrolledStudents
                .Where(s => !existingIds.Contains(s.Id))
                .ToList();
            if (!absentStudents.Any()) return;

            var now = DateTime.Now;
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = Request.Headers["User-Agent"].ToString();

            foreach (var s in absentStudents)
            {
                db.Attendances.Add(new Attendance
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ClassSectionId = sectionId,
                    StudentId = s.Id,
                    Date = date,
                    RegisteredAt = now,
                    Status = "Absent",
                    IpAddress = ip,
                    UserAgent = $"AUTO_ABSENT||{ua}",
                    IsFlaged = false,
                    FlagReason = "Not Registered"
                });
            }

            db.SaveChanges();
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
                .Take(50)
                .Select(a => new
                {
                    time = a.RegisteredAt.ToString("HH:mm:ss"),
                    studentId = a.StudentId,   // NEW
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

            //get all students in the same tutorial group as this section
            var enrolledStudents = db.Students
                .Where(s => s.TutorialGroupId.HasValue
                    && s.TutorialGroupId.Value == section.TutorialGroupId)
                .ToList();

            var totalStudents = enrolledStudents.Count;

            //get all attendance records for this class section
            var allAttendances = db.Attendances
                .Include(a => a.Student)
                .Where(a => a.ClassSectionId == sectionId)
                .ToList();

            //calculate overall attendance percentage
            var totalPresent = allAttendances.Count(a => a.Status == "Present");
            var totalAbsent  = allAttendances.Count(a => a.Status == "Absent");
            // ignore Leave, Late, Excused in denominator as you prefer

            var consideredTotal = totalPresent + totalAbsent;
            var overallAttendancePercentage = consideredTotal > 0
                ? Math.Round((double)totalPresent / consideredTotal * 100, 1)
                : 0;

            //calculate flaged count
            var flaggedCount = allAttendances.Count(a => a.IsFlaged);

            //count total absences this semester
            var semesterStart = section.Semester?.Id != null
                ? (section.Semester.Id.StartsWith("2025") ? new DateOnly(2025, 1, 1) : DateOnly.FromDateTime(DateTime.Now))
                : DateOnly.FromDateTime(DateTime.Now);

            var totalAbsences = totalAbsent;

            return Json(new
            {
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

            var today = DateOnly.FromDateTime(DateTime.Now);
            var nowTime = TimeOnly.FromDateTime(DateTime.Now);
            var todayName = DateTime.Now.DayOfWeek.ToString();

            if (string.Equals(section.Day, todayName, StringComparison.OrdinalIgnoreCase)
                && nowTime >= section.EndTime)
            {
                AutoMarkAbsents(sectionId, today, staffId);
            }

            //get all students in the same tutorial group as this section
            var enrolledStudents = db.Students
                .Where(s => s.TutorialGroupId.HasValue
                    && s.TutorialGroupId.Value == section.TutorialGroupId)
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
            var totalPresent = allAttendances.Count(a => a.Status == "Present");
            var totalAbsent  = allAttendances.Count(a => a.Status == "Absent");
            // ignore Leave, Late, Excused in denominator as you prefer

            var consideredTotal = totalPresent + totalAbsent;
            var overallAttendancePercentage = consideredTotal > 0
                ? Math.Round((double)totalPresent / consideredTotal * 100, 1)
                : 0;


            //calculate flaged count
            var flaggedCount = allAttendances.Count(a => a.IsFlaged);

            //count total absences this semester
            var semesterStart = section.Semester?.Id != null
                ? (section.Semester.Id.StartsWith("2025") ? new DateOnly(2025, 1, 1) : DateOnly.FromDateTime(DateTime.Now))
                : DateOnly.FromDateTime(DateTime.Now);

            var semesterAttendances = allAttendances.Where(a => a.Date >= semesterStart).ToList();
            var totalAbsences = totalAbsent;

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
        

        [HttpGet]
        public IActionResult GetAttendanceChartData(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
            {
                return Json(new { success = false, message = "Section ID is required." });
            }

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var section = db.ClassSections
                .FirstOrDefault(c => c.SectionId == sectionId && c.StaffId == staffId);

            if (section == null)
            {
                return Json(new { success = false, message = "Section not found." });
            }

            //get allattendance record for this section
            var allAttendances = db.Attendances
                .Include(a => a.Student)
                .Where(a => a.ClassSectionId == sectionId)
                .OrderBy(a => a.Date)
                .ToList();

            //get all enrolled students
            var enrolledStudents = db.Students
                .Where(s => s.TutorialGroupId.HasValue
                    && s.TutorialGroupId.Value == section.TutorialGroupId)
                .ToList();

            var totalStudents = enrolledStudents.Count;

            //chart data by date
            var attendanceByDate = allAttendances
                .GroupBy(a => a.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    PresentCount = g.Count(a => a.Status == "Present"),
                    TotalCount = g.Count(),
                    AttendancePercentage = 
                    (g.Count(a => a.Status == "Present") + g.Count(a => a.Status == "Absent")) > 0
                        ? Math.Round(
                            (double)g.Count(a => a.Status == "Present") /
                            (g.Count(a => a.Status == "Present") + g.Count(a => a.Status == "Absent")) * 100, 1)
                        : 0
                })
                .OrderBy(x => x.Date)
                .ToList();

            var lineChartLabels = attendanceByDate.Select(x => x.Date.ToString("MMM dd")).ToList();
            var lineChartData = attendanceByDate.Select(x => x.AttendancePercentage).ToList();

            //chart data by status
            var statusDistribution = allAttendances
                .GroupBy(a => a.Status)
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToList();

            var pieChartLabels = statusDistribution.Select(x => x.Status).ToList();
            var pieChartData = statusDistribution.Select(x => x.Count).ToList();
            var pieChartColors = pieChartLabels.Select(status =>
                status == "Present" ? "#28a745" :
                status == "Absent" ? "#dc3545" :
                status == "Late" ? "#ffc107" :
                status == "Excused" ? "#17a2b8" :
                status == "Leave" ? "#6f42c1" : "#6c757d"
            ).ToList();

            return Json(new
            {
                success = true,
                lineChart = new
                {
                    labels = lineChartLabels,
                    data = lineChartData
                },
                pieChart = new
                {
                    labels = pieChartLabels,
                    data = pieChartData,
                    colors = pieChartColors
                }
            });
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
                    .ThenInclude(cs => cs.Semester)
                .Where(a => request.AttendanceIds.Contains(a.Id) && a.ClassSection.StaffId == staffId)
                .ToList();

            if (attendances.Count != request.AttendanceIds.Count)
            {
                return Json(new { success = false, message = "Some attendance records were not found or you don't have permission to update them." });
            }

            if (attendances.Any(a => a.ClassSection.Semester == null || !a.ClassSection.Semester.IsActive))
            {
                return Json(new { success = false, message = "You can only update attendance for classes in an active semester." });
            }

            //update status for all selected attendances
            foreach (var attendance in attendances)
            {
                attendance.Status = request.Status;
            }

            try
            {
                db.SaveChanges();
                return Json(new
                {
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

        //academic calendar - read only
        public IActionResult ViewCalendar(string? semesterId = null)
        {
            ViewData["Title"] = "Academic Calendar";

            var semesters = db.Semesters
                .OrderByDescending(s => s.Id)
                .ToList();

            var viewModel = new EventManagementVM
            {
                Events = new List<CalendarEvent>(),
                Semesters = semesters,
                SelectedSemesterId = semesterId,
                EventsWithSemester = new List<EventWithSemesterInfo>()
            };

            return View(viewModel);
        }

        //get calendar events for staff
        [HttpGet]
        public IActionResult GetCalendarEvents(string? start, string? end, string? semesterId = null)
        {
            DateTime startDate;
            DateTime endDate;

            if (!DateTime.TryParse(start, out startDate))
            {
                startDate = DateTime.Now.AddMonths(-1);
            }

            if (!DateTime.TryParse(end, out endDate))
            {
                endDate = DateTime.Now.AddMonths(1);
            }

            var eventsList = new List<object>();

            //get calendar events
            var eventsQuery = db.CalendarEvents
                .Where(e => e.StartTime >= startDate && e.EndTime <= endDate)
                .AsQueryable();

            if (!string.IsNullOrEmpty(semesterId))
            {
                var semester = db.Semesters.FirstOrDefault(s => s.Id == semesterId);
                if (semester != null)
                {
                    var semesterStart = semester.StartDate.ToDateTime(TimeOnly.MinValue);
                    var semesterEnd = semester.EndDate.ToDateTime(TimeOnly.MaxValue);

                    eventsQuery = eventsQuery.Where(e =>
                        e.StartTime.Date >= semesterStart.Date &&
                        e.StartTime.Date <= semesterEnd.Date);
                }
            }

            var calendarEvents = eventsQuery.ToList();

            //add calendar events
            eventsList.AddRange(calendarEvents.Select(e =>
            {
                var isAllDay = (e.StartTime.Hour == 0 && e.StartTime.Minute == 0) &&
                              (e.EndTime.Hour == 0 && e.EndTime.Minute == 0 ||
                               e.EndTime.Hour == 23 && e.EndTime.Minute == 59);

                return new
                {
                    id = e.Id,
                    title = e.Title,
                    description = e.Description,
                    start = isAllDay ? e.StartTime.ToString("yyyy-MM-dd") : e.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = isAllDay ? e.EndTime.ToString("yyyy-MM-dd") : e.EndTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    type = e.Type,
                    backgroundColor = GetEventColor(e.Type),
                    borderColor = GetEventColor(e.Type),
                    textColor = GetEventTextColor(e.Type),
                    allDay = isAllDay,
                    eventType = "calendar"
                };
            }));

            //get semesters and add them as events
            var allSemesters = db.Semesters.ToList();

            var semesters = allSemesters.Where(s =>
            {
                var sStart = s.StartDate.ToDateTime(TimeOnly.MinValue);
                var sEnd = s.EndDate.ToDateTime(TimeOnly.MaxValue);
                return sStart <= endDate && sEnd >= startDate;
            }).ToList();

            if (!string.IsNullOrEmpty(semesterId))
            {
                semesters = semesters.Where(s => s.Id == semesterId).ToList();
            }

            //add semester periods as events
            eventsList.AddRange(semesters.Select(s =>
            {
                var weeks = (s.EndDate.ToDateTime(TimeOnly.MaxValue) - s.StartDate.ToDateTime(TimeOnly.MinValue)).TotalDays / 7;
                var semesterType = weeks >= 13 ? "Long" : "Short";

                return new
                {
                    id = $"semester_{s.Id}",
                    title = $"{s.Id} - {semesterType}",
                    description = $"Semester Period: {s.StartDate:dd MMM yyyy} to {s.EndDate:dd MMM yyyy}",
                    start = s.StartDate.ToString("yyyy-MM-dd"),
                    end = s.EndDate.AddDays(1).ToString("yyyy-MM-dd"),
                    type = "Semester Period",
                    backgroundColor = semesterType == "Long" ? "#6f42c1" : "#e83e8c",
                    borderColor = semesterType == "Long" ? "#5a32a3" : "#d91a72",
                    textColor = "#ffffff",
                    allDay = true,
                    eventType = "semester",
                    classNames = new[] { "semester-period" },
                    extendedProps = new
                    {
                        eventType = "semester",
                        semesterId = s.Id
                    }
                };
            }));

            return Json(eventsList);
        }

        //helper methods for event colors
        private string GetEventColor(string eventType)
        {
            return eventType switch
            {
                "Public Holiday" => "#dc3545",
                "Exam Period" => "#fd7e14",
                "Academic Event" => "#28a745",
                "Semester Break" => "#17a2b8",
                _ => "#495057"
            };
        }

        private string GetEventTextColor(string eventType)
        {
            return "#ffffff";
        }

        //export iCal for staff
        public IActionResult ExportICal(string? semesterId = null)
        {
            var eventsQuery = db.CalendarEvents.AsQueryable();

            if (!string.IsNullOrEmpty(semesterId))
            {
                var semester = db.Semesters.FirstOrDefault(s => s.Id == semesterId);
                if (semester != null)
                {
                    eventsQuery = eventsQuery.Where(e =>
                        e.StartTime.Date >= semester.StartDate.ToDateTime(TimeOnly.MinValue) &&
                        e.StartTime.Date <= semester.EndDate.ToDateTime(TimeOnly.MaxValue));
                }
            }

            var events = eventsQuery.ToList();

            var ical = new StringBuilder();
            ical.AppendLine("BEGIN:VCALENDAR");
            ical.AppendLine("VERSION:2.0");
            ical.AppendLine("PRODID:-//TARUMT//Academic Calendar//EN");
            ical.AppendLine("CALSCALE:GREGORIAN");
            ical.AppendLine("METHOD:PUBLISH");

            foreach (var evt in events)
            {
                ical.AppendLine("BEGIN:VEVENT");
                ical.AppendLine($"UID:{evt.Id}@tarumt.edu.my");
                ical.AppendLine($"DTSTART:{evt.StartTime:yyyyMMddTHHmmss}");
                ical.AppendLine($"DTEND:{evt.EndTime:yyyyMMddTHHmmss}");
                ical.AppendLine($"SUMMARY:{evt.Title}");
                ical.AppendLine($"DESCRIPTION:{evt.Description}");
                ical.AppendLine($"LOCATION:Academic Calendar");
                ical.AppendLine($"STATUS:CONFIRMED");
                ical.AppendLine("END:VEVENT");
            }

            ical.AppendLine("END:VCALENDAR");

            var bytes = Encoding.UTF8.GetBytes(ical.ToString());
            return File(bytes, "text/calendar", $"academic_calendar_{DateTime.Now:yyyyMMdd}.ics");
        }

        //get semester details for calendar navigation
        [HttpGet]
        public IActionResult GetSemesterDetails(string semesterId)
        {
            var semester = db.Semesters.FirstOrDefault(s => s.Id == semesterId);
            if (semester == null)
            {
                return Json(new { success = false, message = "Semester not found." });
            }

            return Json(new
            {
                success = true,
                id = semester.Id,
                startDate = semester.StartDate.ToString("yyyy-MM-dd"),
                endDate = semester.EndDate.ToString("yyyy-MM-dd"),
                isActive = semester.IsActive
            });
        }



        // GET: Staff Profile
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staff = await db.Users
                .OfType<Staff>()
                .Include(s => s.Faculty)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (staff == null)
                return NotFound();

            return View("~/Views/Shared/Profile.cshtml", staff);
        }

        //Update Staff Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string firstName, string lastName,
            string email, string contact, string gender, IFormFile profilePicture)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var staff = await db.Staffs.FindAsync(userId);

                if (staff == null)
                    return NotFound();

                // Validate email uniqueness (exclude current user)
                var emailExists = await db.Users
                    .AnyAsync(u => u.Email == email.Trim().ToLower() && u.Id != userId);

                if (emailExists)
                {
                    TempData["ErrorMessage"] = "Email already exists.";
                    return RedirectToAction("Profile");
                }

                // Update staff fields
                staff.FirstName = firstName?.Trim();
                staff.LastName = lastName?.Trim();
                staff.Email = email?.Trim().ToLower();
                staff.Contact = contact?.Trim();
                staff.Gender = gender?.Trim();

                // Handle profile picture upload (inline)
                if (profilePicture != null && profilePicture.Length > 0)
                {
                    // Validate file size (2MB max)
                    if (profilePicture.Length > 2 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File size must not exceed 2MB.";
                        return RedirectToAction("Profile");
                    }

                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var fileExtension = Path.GetExtension(profilePicture.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["ErrorMessage"] = "Only JPG and PNG files are allowed.";
                        return RedirectToAction("Profile");
                    }

                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/profiles");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = $"{userId}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await profilePicture.CopyToAsync(fileStream);
                    }

                    // Delete old profile picture if exists
                    if (!string.IsNullOrEmpty(staff.ProfilePictureUrl))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", staff.ProfilePictureUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    staff.ProfilePictureUrl = $"/uploads/profiles/{uniqueFileName}";
                }

                await db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Profile updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while updating profile: {ex.Message}";
            }

            return RedirectToAction("Profile");
        }

        //Upload Profile Picture
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProfilePicture(IFormFile profilePicture)
        {
            try
            {
                if (profilePicture == null || profilePicture.Length == 0)
                {
                    TempData["ErrorMessage"] = "Please select a file to upload.";
                    return RedirectToAction("Profile");
                }

                // Validate file size (2MB max)
                if (profilePicture.Length > 2 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "File size must not exceed 2MB.";
                    return RedirectToAction("Profile");
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var fileExtension = Path.GetExtension(profilePicture.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    TempData["ErrorMessage"] = "Only JPG and PNG files are allowed.";
                    return RedirectToAction("Profile");
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var staff = await db.Staffs.FindAsync(userId);

                if (staff == null)
                    return NotFound();

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/profiles");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{userId}_{DateTime.Now:yyyyMMddHHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await profilePicture.CopyToAsync(fileStream);
                }

                // Delete old profile picture if exists
                if (!string.IsNullOrEmpty(staff.ProfilePictureUrl))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", staff.ProfilePictureUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                staff.ProfilePictureUrl = $"/uploads/profiles/{uniqueFileName}";
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = "Profile picture uploaded successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while uploading: {ex.Message}";
            }

            return RedirectToAction("Profile");
        }

        private async Task LoadProfilePicture()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profilePictureUrl = "/img/undraw_profile.svg";

            if (User.IsInRole("Staff"))
            {
                var staff = await db.Staffs.FindAsync(userId);
                if (staff != null && !string.IsNullOrEmpty(staff.ProfilePictureUrl))
                {
                    profilePictureUrl = staff.ProfilePictureUrl;
                }
            }
            else if (User.IsInRole("Student"))
            {
                var student = await db.Students.FindAsync(userId);
                if (student != null && !string.IsNullOrEmpty(student.ProfilePictureUrl))
                {
                    profilePictureUrl = student.ProfilePictureUrl;
                }
            }

            ViewBag.ProfilePictureUrl = profilePictureUrl;
        }
        //lks
        // =======================
        // STAFF CLASSWORK PAGE
        // =======================
        public IActionResult Classwork(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest("Course ID is required.");

            var room = db.Courses.FirstOrDefault(c => c.Id == id);
            if (room == null)
                return NotFound();

            var materials = db.Materials
                .Where(m => m.CourseId == id)
                .OrderBy(m => m.Title)
                .ThenBy(m => m.Id)
                .ToList();

            ViewBag.CourseId = id.ToUpper();
            ViewBag.Room = room;

            return View("~/Views/Staff/Classwork.cshtml", materials);
        }

        // =======================
        // UPLOAD MULTIPLE FILES
        // =======================
        [HttpPost]
        public IActionResult UploadDoc(string courseId, string title, IFormFile[] files)
        {
            if (files == null || files.Length == 0)
                return BadRequest("No files selected.");

            if (string.IsNullOrWhiteSpace(title))
                return BadRequest("Title is required.");

            var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            var lastMaterial = db.Materials
                .OrderByDescending(m => m.Id)
                .FirstOrDefault();

            int nextNumber = 1;
            if (lastMaterial != null && lastMaterial.Id.StartsWith("M"))
            {
                if (int.TryParse(lastMaterial.Id.Substring(1), out int num))
                    nextNumber = num + 1;
            }

            var savedMaterials = new List<Material>();

            foreach (var file in files)
            {
                // remove old name
                string originalName = Path.GetFileNameWithoutExtension(file.FileName);
                string cleanName = string.Concat(originalName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");

                // rename new name
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{courseId}_{timestamp}_{cleanName}{Path.GetExtension(file.FileName)}";

                var filePath = Path.Combine(uploadFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                var material = new Material
                {
                    Id = $"M{nextNumber:D3}",
                    CourseId = courseId,
                    Title = title,
                    FileUrl = "/uploads/" + fileName,
                    MaterialType = Path.GetExtension(file.FileName).TrimStart('.')
                };

                db.Materials.Add(material);
                savedMaterials.Add(material);
                nextNumber++;
            }

            db.SaveChanges();
            return Json(savedMaterials);
        }

        // =======================
        // DELETE MATERIAL
        // =======================
        [HttpPost]
        public IActionResult DeleteMaterial(string id)
        {
            var material = db.Materials.FirstOrDefault(m => m.Id == id);
            if (material != null)
            {
                var filePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    material.FileUrl.TrimStart('/')
                );

                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                db.Materials.Remove(material);
                db.SaveChanges();
            }

            return Ok();
        }

        // =======================
        // STAFF CLASSROOM UI
        // =======================
        public IActionResult StaffClassroomUI()
        {
            var courses = db.Courses
                .OrderBy(c => c.Id)
                .ToList();

            return View("~/Views/Staff/ClassroomUI.cshtml", courses);
        }
        // =======================
        // STAFF TICKET LIST
        // =======================
        public IActionResult StaffTicketList()
        {
            var tickets = db.SupportTickets
                .OrderByDescending(t => t.CreatedAt)
                .ToList();

            return View("~/Views/Staff/StaffTicketList.cshtml", tickets);
        }

        // =======================
        // STAFF TICKET DETAILS
        // =======================
        public IActionResult StaffTicketDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var ticket = db.SupportTickets
                .Include(t => t.TicketMessages)
                .FirstOrDefault(t => t.Id == id);

            if (ticket == null) return NotFound();

            // Sort by date and convert to list for consistency
            ticket.TicketMessages = ticket.TicketMessages
                .OrderBy(m => m.SentAt)
                .ToList();

            return View("~/Views/Staff/StaffTicketDetails.cshtml", ticket);
        }

        // =======================
        // STAFF FILTER (AJAX)
        // =======================
        [HttpGet]
        public IActionResult GetStaffTickets(string search = "", string status = "")
        {
            var tickets = db.SupportTickets.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                tickets = tickets.Where(t => t.Id.Contains(search) || t.Subject.Contains(search));

            if (!string.IsNullOrWhiteSpace(status))
                tickets = tickets.Where(t => t.Status == status);

            tickets = tickets.OrderByDescending(t => t.CreatedAt);

            return PartialView("_TicketTable", tickets.OrderByDescending(t => t.CreatedAt).ToList());

        }
        // =======================
        // Ticket reply
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reply(string ticketId, string message)
        {
            //validate
            if (string.IsNullOrEmpty(ticketId) || string.IsNullOrWhiteSpace(message))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "Please enter a message." });

                TempData["Message"] = "Please enter a message.";
                return RedirectToAction("StaffTicketDetails", new { id = ticketId });
            }

            var ticket = db.SupportTickets.FirstOrDefault(t => t.Id == ticketId);
            if (ticket == null) return NotFound();

            //check status closed or no
            if (ticket.Status == "Closed")
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "This ticket is closed and cannot receive replies." });

                TempData["Message"] = "This ticket is closed.";
                return RedirectToAction("StaffTicketDetails", new { id = ticketId });
            }


            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string msgId = $"TM{(db.TicketMessages.Count() + 1):D4}";

            var ticketMessage = new TicketMessage
            {
                Id = msgId,
                SupportTicketId = ticketId,
                SenderId = userId,
                Content = message,
                SentAt = DateTime.Now
            };

            db.TicketMessages.Add(ticketMessage);
            db.SaveChanges();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = true,
                    sender = ticketMessage.SenderId,
                    time = ticketMessage.SentAt.ToString("dd/MM/yyyy HH:mm"),
                    content = ticketMessage.Content
                });
            }

            TempData["Message"] = "Reply sent successfully.";
            return RedirectToAction("StaffTicketDetails", new { id = ticketId });
        }
        // =======================
        // get instant reply
        // =======================
        [HttpGet]
        public IActionResult GetRepliesPartial(string id)
        {
            var ticket = db.SupportTickets
                           .Include(t => t.TicketMessages)
                           .FirstOrDefault(t => t.Id == id);

            if (ticket == null) return NotFound();

            // sort by date
            var messages = ticket.TicketMessages.OrderBy(m => m.SentAt).ToList();
            return PartialView("_TicketMessage", messages);
        }
        // =======================
        // Close ticket
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CloseTicket(string id)
        {
            // find db
            var ticket = db.SupportTickets.FirstOrDefault(t => t.Id == id);

            if (ticket == null) return NotFound();

            // update data
            ticket.Status = "Closed";
            ticket.ResolvedAt = DateTime.Now;

            db.SaveChanges();

            // back to list
            return RedirectToAction("StaffTicketList");
        }
        //task create

        [HttpPost]
        public async Task<IActionResult> Create(CourseTask task, string[] SelectedMaterialIds)
        {
            // generate id
            task.Id = Guid.NewGuid().ToString();

            //add task
            db.CourseTasks.Add(task);

            // create for selected
            if (SelectedMaterialIds != null)
            {
                foreach (var matId in SelectedMaterialIds)
                {
                    var tm = new TaskMaterial
                    {
                        Id = Guid.NewGuid().ToString(),
                        CourseTaskId = task.Id,
                        MaterialId = matId
                    };
                    //db.TaskMaterial.Add(tm);
                }
            }

            await db.SaveChangesAsync();
            return RedirectToAction("Index");
        }


        //task section
        public IActionResult TaskList()
        {
            var tasks = db.CourseTasks
                .Include(x => x.Submissions)
                .OrderByDescending(x => x.DueDate)
                .ToList();

            return View(tasks);
        }

        public IActionResult TaskDetails(string id)
        {
            var task = db.CourseTasks
                .Include(x => x.Submissions)
                .ThenInclude(x => x.CourseTask)
                .FirstOrDefault(x => x.Id == id);

            return View(task);
        }
        //create new task
        [HttpPost]
        public IActionResult CreateTask(CourseTask obj)
        {
            obj.Id = "T" + (db.CourseTasks.Count() + 1).ToString("D4");

            if (obj.DueDate < DateTime.Now)
            {
                TempData["msg"] = "Due date cannot be in the past.";
                return RedirectToAction("TaskList");
            }

            db.CourseTasks.Add(obj);
            db.SaveChanges();

            TempData["msg"] = "Assignment created.";
            return RedirectToAction("TaskList");
        }

        [HttpPost]
        public IActionResult Grade(string id, decimal grade, string feedback)
        {
            var s = db.Submissions.FirstOrDefault(x => x.Id == id);
            if (s == null) return NotFound();

            s.Grade = grade;
            s.Feedback = feedback;
            db.SaveChanges();

            TempData["msg"] = "Graded!";
            return RedirectToAction("TaskDetails", new { id = s.CourseTaskId });
        }
        // ======================
        // LIST ALL TASKS
        // ======================
        public IActionResult Task()
        {
            var tasks = db.CourseTasks
                .OrderByDescending(t => t.DueDate)
                .ToList();

            return View(tasks);
        }

        // ======================
        // CREATE PAGE FORM
        // ======================
        [HttpGet]
        public IActionResult TaskCreate()
        {
            ViewBag.Sections = db.ClassSections
                .Include(s => s.Course)
                .ToList();

            // get all material for pick
            ViewBag.Materials = db.Materials.ToList();

            return View();
        }


        [HttpPost]
        public async Task<IActionResult> TaskCreate(CourseTask task, string[] SelectedMaterialIds)
        {
            // generate id
            task.Id = Guid.NewGuid().ToString();

            // add to db
            db.CourseTasks.Add(task);

            // link
            if (SelectedMaterialIds != null && SelectedMaterialIds.Length > 0)
            {
                foreach (var matId in SelectedMaterialIds)
                {
                    var tm = new TaskMaterial
                    {
                        Id = Guid.NewGuid().ToString(),
                        CourseTaskId = task.Id,
                        MaterialId = matId
                    };

                    // Temporary fix
                    db.Set<TaskMaterial>().Add(tm);
                }
            }

            // save all change
            await db.SaveChangesAsync();

            // back to list
            return RedirectToAction("TaskList");
        }
        // ======================
        // CREATE POST SAVE
        // ======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CourseTask task)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Sections = db.ClassSections.ToList();
                return View(task);
            }

            // prevent duplicate ID
            if (db.CourseTasks.Any(t => t.Id == task.Id))
            {
                ModelState.AddModelError("Id", "Task ID already exists");
                ViewBag.Sections = db.ClassSections.ToList();
                return View(task);
            }

            task.Id = Guid.NewGuid().ToString();
            db.CourseTasks.Add(task);
            db.SaveChanges();

            TempData["msg"] = "Assignment created!";
            return RedirectToAction("Index");
        }
        // PAGE VIEW

        public IActionResult Chat(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return Content("No class selected.");

            ViewBag.ClassId = classId;
            ViewBag.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Initial load of messages
            var messages = db.ChatMessages
                .Where(x => x.ReceiverId == "STF0002") // Testing with STF0002
                .OrderBy(x => x.SentAt)
                .ToList();

            return View(messages);
        }

        // AJAX API: Staff/GetMessages
        [HttpGet]
        public IActionResult GetMessages(string? search)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

          
            var query = db.ChatMessages
                .Where(x => x.ReceiverId == "STF0002");

            if (!string.IsNullOrEmpty(search))
            {
                var s = search.ToLower();
                query = query.Where(m => m.Content.ToLower().Contains(s) ||
                                        m.SenderId.ToLower().Contains(s));
            }

         
            var messages = query
                .OrderBy(x => x.SentAt)
                .Select(m => new {
                    sender = m.SenderId,
                    content = m.Content,
                    time = m.SentAt.ToString("HH:mm"),
                    isMe = m.SenderId == currentUserId
                }).ToList();

            return Json(messages);
        }

        // POST: Staff/Send
        [HttpPost]
        public IActionResult Send(string classId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return RedirectToAction("Chat", new { classId });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Generate CM001 format ID
            var lastMsg = db.ChatMessages.OrderByDescending(m => m.Id).FirstOrDefault();
            int nextNum = 1;
            if (lastMsg != null && lastMsg.Id.StartsWith("CM"))
            {
                if (int.TryParse(lastMsg.Id.Substring(2), out int num))
                    nextNum = num + 1;
            }

            var msg = new ChatMessage
            {
                Id = $"CM{nextNum:D3}",
                SenderId = userId,
                ReceiverId = "STF0002", // Test Receiver
                Content = content,
                SentAt = DateTime.Now,
                IsRead = false
            };

            db.ChatMessages.Add(msg);
            db.SaveChanges();

            return RedirectToAction("Chat", new { classId });
        }
        [HttpPost]
        public async Task<IActionResult> GradeSubmission(string SubmissionId, decimal Grade, string Feedback)
        {
            var submission = await db.Submissions.FindAsync(SubmissionId);
            if (submission != null)
            {
                submission.Grade = Grade;
                submission.Feedback = Feedback;
                await db.SaveChangesAsync();
            }
            return RedirectToAction("ViewSubmissions", new { id = submission.CourseTaskId });
        }
        //[HttpPost]
        //public async Task<IActionResult> Create(CourseTask task, string[] SelectedMaterialIds)
        //{
        //    // 1. Generate ID for the Task
        //    task.Id = Guid.NewGuid().ToString();

        //    // 2. Add the Task
        //    db.CourseTasks.Add(task);

        //    // 3. Create TaskMaterial entries for each selected material
        //    if (SelectedMaterialIds != null)
        //    {
        //        foreach (var matId in SelectedMaterialIds)
        //        {
        //            var tm = new TaskMaterial
        //            {
        //                Id = Guid.NewGuid().ToString(),
        //                CourseTaskId = task.Id,
        //                MaterialId = matId
        //            };
        //            //db.TaskMaterials.Add(tm);
        //        }
        //    }

        //    await db.SaveChangesAsync();
        //    return RedirectToAction("Index");
        //}
    }
}
