using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Filters;
using SHAPEE.Models;
using System.Security.Claims;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Stripe.Checkout;


namespace SHAPEE.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController(DB db, Helper hp) : Controller
    {
        //action filter to prevent access to pages if student has overdue payment locked
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var action = context.RouteData.Values["action"]?.ToString() ?? "";
            var allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CurrentBilling",
                "PaymentHistory",
                "Payment",
                "Pay",
                "DownloadReceipt",
                "StripeSuccess"
            };

            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(studentId))
            {
                //apply payment lock if overdue 
                ApplyPaymentLockIfOverdue(studentId);

                if (!allow.Contains(action))
                {
                    var status = db.Students
                        .Where(s => s.Id == studentId)
                        .Select(s => s.Status)
                        .FirstOrDefault();

                    if (status == "LockedPayment")
                    {
                        TempData["PaymentLockMessage"] = "Your account is locked due to overdue fees. Please complete payment to regain access.";
                        context.Result = RedirectToAction("CurrentBilling");
                        return;
                    }
                }
            }

            base.OnActionExecuting(context);
        }
        // GET: /Student
        public async Task<IActionResult> Dashboard()
        {
            await LoadProfilePicture();
            ViewData["Title"] = "Student Dashboard";

            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // all sections the student has attendance records for
            var sectionIds = db.Attendances
                .Where(a => a.StudentId == studentId)
                .Select(a => a.ClassSectionId)
                .Distinct()
                .ToList();

            var sections = db.ClassSections
                .Include(cs => cs.Course)
                .Where(cs => sectionIds.Contains(cs.SectionId))
                .ToList();

            var attendances = db.Attendances
                .Where(a => a.StudentId == studentId && sectionIds.Contains(a.ClassSectionId))
                .ToList();

            var subjectSummaries = sections
                .GroupBy(cs => cs.Course.Id)
                .Select(g =>
                {
                    var course = g.First().Course;
                    var relatedSectionIds = g.Select(cs => cs.SectionId).ToList();

                    var subjectAtts = attendances
                        .Where(a => relatedSectionIds.Contains(a.ClassSectionId))
                        .ToList();

                    var present = subjectAtts.Count(a => a.Status == "Present");
                    var absent = subjectAtts.Count(a => a.Status == "Absent");
                    var leave = subjectAtts.Count(a => a.Status == "Leave");

                    var consideredTotal = present + absent;
                    var percentage = consideredTotal > 0
                        ? Math.Round((double)present / consideredTotal * 100, 2)
                        : 0;

                    return new SubjectAttendanceSummaryVM
                    {
                        CourseId = course.Id,
                        CourseName = course.Name,
                        Present = present,
                        Absent = absent,
                        Leave = leave,          // you don’t currently have a "Leave" status
                        Percentage = percentage
                    };
                })
                .OrderBy(s => s.CourseId)
                .ToList();

            var vm = new StudentDashboardVM
            {
                SubjectAttendances = subjectSummaries
            };

            return View(vm);
        }


        //===============KOH KAI MINN================
        // student main attendance page
        public IActionResult ViewAttendance(string? sectionId, string? token, string? subject = null, string? month = null, string? search = null, string? classType = null)
        {
            ViewData["Title"] = "View Attendance";

            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            //get student and verify tutorial group 
            var student = db.Students
                .Include(s => s.TutorialGroup)
                .FirstOrDefault(s => s.Id == studentId);

            if (student == null || !student.TutorialGroupId.HasValue)
            {
                return View(new StudentAttendanceVM
                {
                    ClassAttendances = new List<StudentAttendanceItemVM>(),
                    AvailableSubjects = new List<SubjectFilterOption>(),
                    AvailableMonths = new List<MonthFilterOption>()
                });
            }

            //get section id from attendance
            var sectionIds = db.Attendances
                .Where(a => a.StudentId == studentId)
                .Select(a => a.ClassSectionId)
                .Distinct()
                .ToList();

            var enrolledSectionsQuery = db.ClassSections
                .Include(cs => cs.Course)
                .Where(cs => sectionIds.Contains(cs.SectionId))
                .AsQueryable();

            //apply subject filter
            subject = subject?.Trim() ?? "";
            if (!string.IsNullOrEmpty(subject))
            {
                enrolledSectionsQuery = enrolledSectionsQuery
                    .Where(cs => cs.Course.Id == subject);
            }

            classType = classType?.Trim() ?? "";
            if (!string.IsNullOrEmpty(classType))
            {
                enrolledSectionsQuery = enrolledSectionsQuery
                    .Where(cs => cs.SectionType == classType);
            }

            //apply search filter
            search = search?.Trim() ?? "";
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                enrolledSectionsQuery = enrolledSectionsQuery.Where(cs =>
                    cs.SectionId.ToLower().Contains(search) ||
                    cs.Course.Name.ToLower().Contains(search));
            }

            var enrolledSections = enrolledSectionsQuery.ToList();
            var filteredSectionIds = enrolledSections.Select(cs => cs.SectionId).ToList();

            //get attendance records with month filter
            var attendancesQuery = db.Attendances
                .Where(a => a.StudentId == studentId)
                .AsQueryable();

            //apply month filter
            month = month?.Trim() ?? "";
            if (!string.IsNullOrEmpty(month))
            {
                if (DateTime.TryParse($"{month}-01", out var m))
                {
                    attendancesQuery = attendancesQuery
                        .Where(a => a.Date.Year == m.Year && a.Date.Month == m.Month);
                }
            }

            var attendances = attendancesQuery
                .Where(a => filteredSectionIds.Contains(a.ClassSectionId))
                .ToList();


            //group attendances by class
            var classAttendances = enrolledSections
                .Select(section =>
                {
                    if (section == null || section.Course == null)
                        return null;

                    var sectionAttendances = attendances
                        .Where(a => a.ClassSectionId == section.SectionId)
                        .ToList();

                    return new StudentAttendanceItemVM
                    {
                        ClassSection = section,
                        Course = section.Course,
                        PresentCount = sectionAttendances.Count(x => x.Status == "Present"),
                        AbsentCount = sectionAttendances.Count(x => x.Status == "Absent"),
                        TotalSessions = sectionAttendances.Count,
                        AttendancePercentage = sectionAttendances.Any()
                            ? Math.Round((double)sectionAttendances.Count(x => x.Status == "Present") / sectionAttendances.Count * 100, 2)
                            : 0
                    };
                })
                .Where(x => x != null)
                // Only show sections with attendance if month filter is applied
                .Where(x => string.IsNullOrEmpty(month) || x!.TotalSessions > 0)
                .OrderBy(x => x!.Course!.Id)
                .ToList();

            //get available subjects
            var availableSubjects = db.ClassSections
                 .Include(cs => cs.Course)
                 .Where(cs => sectionIds.Contains(cs.SectionId))
                 .Select(cs => new { cs.Course.Id, cs.Course.Name })
                 .Distinct()
                 .OrderBy(c => c.Id)
                 .Select(c => new SubjectFilterOption
                 {
                     CourseId = c.Id,
                     CourseName = c.Name
                 })
                 .ToList();


            //get available months
            var monthNames = new[]
            {
                "January","February","March","April","May","June",
                "July","August","September","October","November","December"
            };

            var availableMonths = db.Attendances
                .Where(a => a.StudentId == studentId)
                .Select(a => a.Date)
                .Distinct()
                .GroupBy(d => new { d.Year, d.Month })
                .OrderByDescending(g => g.Key.Year)
                .ThenByDescending(g => g.Key.Month)
                .Select(g => new MonthFilterOption
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    DisplayText = $"{monthNames[g.Key.Month - 1]} {g.Key.Year}"
                })
                .ToList();

            //get overall stats
            var overallPresent = attendances.Count(a => a.Status == "Present");
            var overallAbsent = attendances.Count(a => a.Status == "Absent");
            var overallLeave = attendances.Count(a => a.Status == "Leave");
            var overallTotal = attendances.Count;

            var consideredTotal = overallPresent + overallAbsent;
            var overallPercentage = consideredTotal > 0
                ? Math.Round((double)overallPresent / consideredTotal * 100, 2)
                : 0;

            //get recent attendances
            var recentAttendances = db.Attendances
                .Include(a => a.ClassSection)
                    .ThenInclude(cs => cs.Course)
                .Include(a => a.ClassSection)
                    .ThenInclude(cs => cs.Room)
                .Where(a => a.StudentId == studentId)
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.RegisteredAt)
                .Take(10)
                .ToList();

            //get view model
            var viewModel = new StudentAttendanceVM
            {
                ClassAttendances = classAttendances,
                OverallPresent = overallPresent,
                OverallAbsent = overallAbsent,
                OverallLeave = overallLeave,
                OverallPercentage = overallPercentage,
                RecentAttendances = recentAttendances,
                SectionId = sectionId,
                QrToken = token,
                AvailableSubjects = availableSubjects,
                AvailableMonths = availableMonths
            };

            //ajax partial return
            if (Request.IsAjax())
            {
                return PartialView("_AttendanceTable", viewModel);
            }

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult GetAttendanceDetails(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId))
                return PartialView("_AttendanceDetails", null);

            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // get student and verify tutorial group match
            var student = db.Students.FirstOrDefault(s => s.Id == studentId);
            if (student == null || !student.TutorialGroupId.HasValue)
                return PartialView("_AttendanceDetails", null);

            var classSection = db.ClassSections
                .Include(cs => cs.Course)
                .Include(cs => cs.Semester)
                .Include(cs => cs.Staff)
                .Include(cs => cs.Room)
                .FirstOrDefault(cs => cs.SectionId == sectionId
                    && cs.TutorialGroupId == student.TutorialGroupId.Value);

            if (classSection == null)
                return PartialView("_AttendanceDetails", null);

            var attendances = db.Attendances
                .Where(a => a.StudentId == studentId && a.ClassSectionId == sectionId)
                .OrderByDescending(a => a.Date)
                .ToList();

            var presentCount = attendances.Count(a => a.Status == "Present");
            var absentCount = attendances.Count(a => a.Status == "Absent");
            var totalSessions = attendances.Count;
            var attendancePercentage = totalSessions > 0
                ? Math.Round((double)presentCount / totalSessions * 100, 2)
                : 0;

            var viewModel = new StudentAttendanceDetailsVM
            {
                ClassSection = classSection,
                Course = classSection.Course,
                Semester = classSection.Semester,
                Attendances = attendances,
                PresentCount = presentCount,
                AbsentCount = absentCount,
                TotalSessions = totalSessions,
                AttendancePercentage = attendancePercentage
            };

            return PartialView("_AttendanceDetails", viewModel);
        }


        // student submits attendance (QR or manual code)
        [HttpPost]
        [Authorize(Roles = "Student")]
        public IActionResult SubmitAttendance(string sectionId, string? qrToken, string? manualCode)
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);

            ClassSection? section = null;
            if (!string.IsNullOrEmpty(qrToken) && !string.IsNullOrEmpty(sectionId))
            {
                section = db.ClassSections.FirstOrDefault(s => s.SectionId == sectionId);
                if (section == null)
                {
                    if (Request.IsAjax())
                        return Json(new { success = false, message = "Invalid section ID." });
                    return BadRequest("Invalid section ID.");
                }
            }
            else if (!string.IsNullOrEmpty(manualCode))
            {
                section = db.ClassSections.FirstOrDefault(s =>
                    s.AttendanceCode == manualCode &&
                    s.IsAttendanceSessionActive);
                if (section == null)
                {
                    if (Request.IsAjax())
                        return Json(new { success = false, message = "Invalid attendance code or no active session found for this code." });
                    return BadRequest("Invalid attendance code or no active session found for this code.");
                }
                sectionId = section.SectionId;
            }
            else
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "Either QR token or attendance code is required." });
                return BadRequest("Either QR token or attendance code is required.");
            }

            //check tutorial group match
            var student = db.Students.FirstOrDefault(s => s.Id == studentId);
            if (student == null || !student.TutorialGroupId.HasValue)
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "You are not assigned to a tutorial group." });
                return BadRequest("You are not assigned to a tutorial group.");
            }

            if (student.TutorialGroupId.Value != section.TutorialGroupId)
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "Your tutorial group does not match this class section's tutorial group." });
                return BadRequest("Your tutorial group does not match this class section's tutorial group.");
            }

            //check session is active
            if (!section.IsAttendanceSessionActive)
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "Attendance session is not active." });
                return BadRequest("Attendance session is not active.");
            }

            //check whether is Qr or manual code
            bool validQr = !string.IsNullOrEmpty(qrToken)
                && qrToken == section.CurrentQrToken
                && section.CurrentQrExpiry is not null
                && section.CurrentQrExpiry.Value >= now;

            bool validCode = !string.IsNullOrEmpty(manualCode)
                && manualCode == section.AttendanceCode;

            if (!validQr && !validCode)
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "Invalid or expired token/code." });
                return BadRequest("Invalid or expired token/code.");
            }

            var sourceLabel = validQr ? "QR" : "MANUAL";

            //prevent duplicate att
            bool already = db.Attendances.Any(a =>
                a.ClassSectionId == sectionId &&
                a.StudentId == studentId &&
                a.Date == today);
            if (already)
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "You have already marked attendance for this class today." });
                return BadRequest("You have already marked attendance for this class today.");
            }

            //sus ip checking
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = Request.Headers["User-Agent"].ToString();
            var windowStart = now.AddMinutes(-10);

            int othersFromSameIp = db.Attendances
                .Where(a => a.ClassSectionId == sectionId
                    && a.Date == today
                    && a.IpAddress == ip
                    && a.StudentId != studentId
                    && a.RegisteredAt >= windowStart)
                .Select(a => a.StudentId)
                .Distinct()
                .Count();

            bool isFlagged = othersFromSameIp >= 1;

            //create attendance record
            var att = new Attendance
            {
                Id = Guid.NewGuid().ToString("N"),
                ClassSectionId = sectionId,
                ClassSection = section,
                StudentId = studentId,
                Date = today,
                RegisteredAt = now,
                Status = "Present",
                IpAddress = ip,
                UserAgent = $"{sourceLabel}||{ua}",
                IsFlaged = isFlagged,
                FlagReason = isFlagged ? "Multiple students using same IP in short time window." : null
            };

            db.Attendances.Add(att);

            try
            {
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                if (Request.IsAjax())
                    return Json(new { success = false, message = "Error saving attendance: " + ex.Message });
                return BadRequest("Error saving attendance: " + ex.Message);
            }

            TempData["AttendanceMessage"] = isFlagged
                ? "Attendance recorded (flagged for review)."
                : "Attendance recorded successfully.";

            if (Request.IsAjax())
            {
                return Json(new {
                    success = true,
                    message = isFlagged
                        ? "Attendance recorded (flagged for review)."
                        : "Attendance recorded successfully."
                });
            }

            return RedirectToAction("ViewAttendance");
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

        //get calendar events for students
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

        //export iCal for students
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
                        e.EndTime.Date <= semester.EndDate.ToDateTime(TimeOnly.MaxValue));
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


        public IActionResult CurrentBilling()
        {
            ViewData["Title"] = "Current Billing";

            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var student = db.Students
                .Include(s => s.Program)
                    .ThenInclude(p => p.Faculty)
                .FirstOrDefault(s => s.Id == studentId);

            if (student == null) return NotFound("Student not found.");
            if (string.IsNullOrEmpty(student.ProgramId)) return BadRequest("Student has no Program assigned.");

            var activeSemester = db.Semesters
                .OrderByDescending(s => s.Id)
                .FirstOrDefault(s => s.IsActive);

            if (activeSemester == null) return BadRequest("No active semester configured.");

            ApplyPaymentLockIfOverdue(studentId);

            var pcs = db.ProgramCourses
                .Where(pc =>
                    pc.ProgramId == student.ProgramId &&
                    pc.YearLevel == student.YearLevel &&
                    pc.Semester == student.Semester)
                .Include(pc => pc.Course)
                .ToList();

            var totalCredits = pcs.Sum(x => x.Course.CreditHours);
            var rate = student.Program.Faculty.CostPerCreditHour;
            var totalAmount = totalCredits * rate;

            var inv = db.Invoices.FirstOrDefault(i =>
                i.StudentId == studentId &&
                i.SemesterId == activeSemester.Id);

            if (inv == null)
            {
                inv = new Invoice
                {
                    Id = "INV-" + DateTime.Now.ToString("yyMMdd-HHmmssfff"),
                    StudentId = studentId,
                    SemesterId = activeSemester.Id,
                    TotalAmount = totalAmount,
                    DueDate = DateTime.Today.AddDays(7),
                    IsPaid = false,
                    GenerateDate = DateTime.Now
                };

                db.Invoices.Add(inv);
                db.SaveChanges();
            }

            var outstanding = db.Invoices
                .Where(i => i.StudentId == studentId && !i.IsPaid)
                .OrderBy(i => i.DueDate)
                .ToList();

            var vm = new CurrentBillingVM
            {
                StudentStatus = student.Status,
                YearLevel = student.YearLevel,
                SemesterNo = student.Semester,
                ActiveSemesterId = activeSemester.Id,

                TotalCredits = totalCredits,
                CostPerCreditHour = rate,

                CurrentInvoiceId = inv.Id,
                CurrentInvoiceDueDate = inv.DueDate,
                CurrentInvoiceAmount = inv.TotalAmount,
                CurrentInvoiceIsPaid = inv.IsPaid,

                OutstandingInvoices = outstanding.Select(x => new InvoiceRowVM
                {
                    InvoiceId = x.Id,
                    SemesterId = x.SemesterId,
                    DueDate = x.DueDate,
                    TotalAmount = x.TotalAmount,
                    IsPaid = x.IsPaid,
                    IsOverdue = !x.IsPaid && DateTime.Today > x.DueDate.Date
                }).ToList()
            };

            vm.TotalPayable = vm.OutstandingInvoices.Sum(x => x.TotalAmount);

            return View("CurrentBilling", vm);
        }

        public IActionResult PaymentHistory(string? dateFilter = null, string? amountFilter = null)
        {
            ViewData["Title"] = "Payment History";

            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var txsQuery = db.PaymentTransactions
                .Include(t => t.Invoice)
                .Where(t => t.Invoice.StudentId == studentId)
                .AsQueryable();

            // Date filter (examples)
            dateFilter = dateFilter?.Trim() ?? "";
            if (!string.IsNullOrEmpty(dateFilter))
            {
                if (dateFilter == "last30")
                {
                    var from = DateTime.Today.AddDays(-30);
                    txsQuery = txsQuery.Where(t => t.TransactionDate >= from);
                }
                else if (int.TryParse(dateFilter, out var year))
                {
                    txsQuery = txsQuery.Where(t => t.TransactionDate.Year == year);
                }
                // you can add more options as needed
            }

            // Amount / price filter (examples)
            amountFilter = amountFilter?.Trim() ?? "";
            if (!string.IsNullOrEmpty(amountFilter))
            {
                txsQuery = amountFilter switch
                {
                    "lt500" => txsQuery.Where(t => t.AmountPaid < 500),
                    "500to1000" => txsQuery.Where(t => t.AmountPaid >= 500 && t.AmountPaid <= 1000),
                    "gt1000" => txsQuery.Where(t => t.AmountPaid > 1000),
                    _ => txsQuery
                };
            }

            var txs = txsQuery
                .OrderByDescending(t => t.TransactionDate)
                .ToList();

            var vm = new PaymentHistoryVM
            {
                Transactions = txs.Select(t => new PaymentTransactionRowVM
                {
                    ReceiptId = t.Id,
                    InvoiceId = t.InvoiceId,
                    SemesterId = t.Invoice?.SemesterId ?? "",
                    TransactionDate = t.TransactionDate,
                    PaymentMethod = t.PaymentMethod,
                    AmountPaid = t.AmountPaid
                }).ToList()
            };

            if (Request.IsAjax())
            {
                return PartialView("_PaymentHistory", vm);
            }

            return View("PaymentHistory", vm);
        }

        public IActionResult Payment(string id)
        {
            ViewData["Title"] = "Payment";

            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Load invoice with related student/program/faculty
            var invoice = db.Invoices
                .Include(i => i.Student)
                    .ThenInclude(s => s.Program)
                        .ThenInclude(p => p.Faculty)
                .FirstOrDefault(i => i.Id == id && i.StudentId == studentId);

            if (invoice == null) return NotFound("Invoice not found.");

            if (invoice.IsPaid)
            {
                TempData["SuccessMessage"] = "Invoice already paid.";
                return RedirectToAction("PaymentHistory");
            }

            // Get the planned courses for this student's current program/year/semester
            var student = invoice.Student;

            if (student == null || string.IsNullOrEmpty(student.ProgramId))
                return BadRequest("Student has no Program assigned.");

            var pcs = db.ProgramCourses
                .Where(pc =>
                    pc.ProgramId == student.ProgramId &&
                    pc.YearLevel == student.YearLevel &&
                    pc.Semester == student.Semester)
                .Include(pc => pc.Course)
                .ToList();

            var totalCredits = pcs.Sum(x => x.Course.CreditHours);
            var rate = student.Program.Faculty.CostPerCreditHour;
            var subjectCount = pcs.Count;

            var vm = new PaymentCheckoutVM
            {
                InvoiceId = invoice.Id,
                SemesterId = invoice.SemesterId,
                DueDate = invoice.DueDate,
                TotalAmount = invoice.TotalAmount,

                TotalCredits = totalCredits,
                CostPerCreditHour = rate,
                SubjectCount = subjectCount
            };

            return View("Payment", vm);
        }


        public IActionResult DownloadReceipt(string id)
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var tx = db.PaymentTransactions
                .Include(t => t.Invoice)
                .FirstOrDefault(t => t.Id == id && t.Invoice.StudentId == studentId);

            if (tx == null) return NotFound();

            var relative = (tx.ReceiptUrl ?? "").Trim().TrimStart('/');
            var physical = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                relative.Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(physical)) return NotFound("Receipt file missing.");

            // preview in browser + allow download
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{Path.GetFileName(physical)}\"";
            return PhysicalFile(physical, "application/pdf");
        }

        private string CreatePdfReceipt(
            string receiptId,
            string studentName,
            string description,
            decimal billAmount,
            decimal amountPaid,
            DateTime receiptDate)
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "receipts");
            Directory.CreateDirectory(folder);

            var fileName = $"{receiptId}.pdf";
            var physical = Path.Combine(folder, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("TUNKU ABDUL RAHMAN UNIVERSITY OF MANAGEMENT AND TECHNOLOGY")
                            .FontSize(14).SemiBold().FontColor(Colors.Blue.Darken2);

                        col.Item().PaddingTop(18).Text("RECEIPT").FontSize(16).Bold();
                    });

                    page.Content().PaddingTop(18).Element(e =>
                    {
                        e.Border(1).BorderColor(Colors.Grey.Medium).Padding(18).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);   // label
                                columns.ConstantColumn(12);  // :
                                columns.RelativeColumn(6);   // value
                            });

                            void Row(string label, string value)
                            {
                                table.Cell().PaddingVertical(6).Text(label);
                                table.Cell().PaddingVertical(6).AlignCenter().Text(":");
                                table.Cell().PaddingVertical(6).Text(value);
                            }

                            Row("Receipt Number", receiptId);
                            Row("Name", studentName);
                            Row("Description", description); // e.g. Fees 202501 Y1S1
                            Row("Bill Amount", $"RM {billAmount:N2}");
                            Row("Amount Paid", $"RM {amountPaid:N2}");
                            Row("Receipt Date", receiptDate.ToString("dd-MM-yyyy"));
                            Row("Comment", "");
                        });
                    });

                    page.Footer().PaddingTop(28).Column(col =>
                    {
                        col.Item().Text("1. This is a computer generated receipt and does not require a signature.").FontSize(9);
                        col.Item().Text("2. This official receipt is valid subject to clearance.").FontSize(9);
                    });
                });
            })
            .GeneratePdf(physical);

            return $"/uploads/receipts/{fileName}";
        }

        //stripe payment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Pay(string invoiceId)
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var invoice = db.Invoices.FirstOrDefault(i => i.Id == invoiceId && i.StudentId == studentId);
            if (invoice == null) return NotFound("Invoice not found.");
            if (invoice.IsPaid) return RedirectToAction("PaymentHistory");

            var domain = $"{Request.Scheme}://{Request.Host}";
            var student = db.Students.FirstOrDefault(s => s.Id == studentId);
            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = $"{domain}/Student/StripeSuccess?invoiceId={invoice.Id}&session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{domain}/Student/Payment?id={invoice.Id}",
                CustomerEmail = student?.Email,
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "myr",
                            UnitAmount = (long)(invoice.TotalAmount * 100m),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Fees {invoice.SemesterId}",
                                Description = $"Invoice {invoice.Id}"
                            }
                        }
                    }
                }
            };

            var service = new SessionService();
            var session = service.Create(options);

            return Redirect(session.Url);
        }

        //stripe success
        [HttpGet]
        public IActionResult StripeSuccess(string invoiceId, string session_id)
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var invoice = db.Invoices.FirstOrDefault(i => i.Id == invoiceId && i.StudentId == studentId);
            if (invoice == null) return NotFound("Invoice not found.");

            var service = new SessionService();
            var session = service.Get(session_id);

            if (session.PaymentStatus != "paid")
            {
                TempData["PaymentLockMessage"] = "Payment not completed.";
                return RedirectToAction("Payment", new { id = invoice.Id });
            }

            // prevent double payment if user refreshes success page
            if (!invoice.IsPaid)
            {
                var student = db.Students.FirstOrDefault(s => s.Id == studentId);

                var receiptId = "RCP-" + DateTime.Now.ToString("yyMMdd-HHmmssfff");
                var description = student != null
                    ? $"Fees {invoice.SemesterId} Y{student.YearLevel}S{student.Semester}"
                    : $"Fees {invoice.SemesterId}";

                var receiptUrl = CreatePdfReceipt(
                    receiptId: receiptId,
                    studentName: student?.FullName?.Trim() ?? studentId,
                    description: description,
                    billAmount: invoice.TotalAmount,
                    amountPaid: invoice.TotalAmount,
                    receiptDate: DateTime.Now
                );

                var domain = $"{Request.Scheme}://{Request.Host}";
                var receiptLink = domain + Url.Action("DownloadReceipt", "Student", new { id = receiptId });

                if (student != null && !string.IsNullOrWhiteSpace(student.Email))
                {
                    _ = hp.SendPaymentReceiptEmail(
                        toEmail: student.Email,
                        studentName: student.FullName.Trim(),
                        invoiceId: invoice.Id,
                        description: description,
                        amountPaid: invoice.TotalAmount,
                        receiptId: receiptId,
                        receiptLink: receiptLink
                    );
                }

                db.PaymentTransactions.Add(new PaymentTransaction
                {
                    Id = receiptId,
                    InvoiceId = invoice.Id,
                    AmountPaid = invoice.TotalAmount,
                    PaymentMethod = "Stripe",
                    TransactionDate = DateTime.Now,
                    ReceiptUrl = receiptUrl
                });

                invoice.IsPaid = true;

                if (student != null && student.Status == "LockedPayment")
                    student.Status = "Active";

                db.SaveChanges();
            }

            TempData["SuccessMessage"] = "Payment successful (Stripe).";
            return RedirectToAction("PaymentHistory");
        }


        //helper methods

        private void ApplyPaymentLockIfOverdue(string studentId)
        {
            var today = DateTime.Today;

            var overdue = db.Invoices.Any(i =>
                i.StudentId == studentId &&
                !i.IsPaid &&
                today > i.DueDate.Date.AddDays(7));

            if (!overdue) return;

            var student = db.Students.FirstOrDefault(s => s.Id == studentId);
            if (student != null && student.Status != "LockedPayment")
            {
                student.Status = "LockedPayment";
                db.SaveChanges();
            }
        }


        //get student profile
        public async Task<IActionResult> Profile()
        {
            await LoadProfilePicture();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var student = await db.Users
                .OfType<Student>()
                .Include(s => s.Program)
                .Include(s => s.TutorialGroup)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (student == null)
                return NotFound();

            return View("~/Views/Shared/Profile.cshtml", student);
        }

        //Update Student Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string firstName, string lastName,
            string email, string contact, string gender, IFormFile profilePicture)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var student = await db.Students.FindAsync(userId);

                if (student == null)
                    return NotFound();

                // Validate email uniqueness (exclude current user)
                var emailExists = await db.Users
                    .AnyAsync(u => u.Email == email.Trim().ToLower() && u.Id != userId);

                if (emailExists)
                {
                    TempData["ErrorMessage"] = "Email already exists.";
                    return RedirectToAction("Profile");
                }

                // Update student fields
                student.FirstName = firstName?.Trim();
                student.LastName = lastName?.Trim();
                student.Email = email?.Trim().ToLower();
                student.Contact = contact?.Trim();
                student.Gender = gender?.Trim();

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
                    if (!string.IsNullOrEmpty(student.ProfilePictureUrl))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", student.ProfilePictureUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    student.ProfilePictureUrl = $"/uploads/profiles/{uniqueFileName}";
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
                var student = await db.Students.FindAsync(userId);

                if (student == null)
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
                if (!string.IsNullOrEmpty(student.ProfilePictureUrl))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", student.ProfilePictureUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                student.ProfilePictureUrl = $"/uploads/profiles/{uniqueFileName}";
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
        // STUDENT CLASSWORK PAGE
        // =======================
        public IActionResult StudentClasswork(string courseId)
        {
            if (string.IsNullOrEmpty(courseId))
                return BadRequest("Course ID is required.");

            var materials = db.Materials
                .Where(m => m.CourseId == courseId)
                .OrderBy(m => m.Title)
                .ThenBy(m => m.Id)
                .ToList();

            ViewBag.CourseId = courseId.ToUpper();

            return View("~/Views/Student/Classwork.cshtml", materials);
        }

        // =======================
        // STUDENT CLASSROOM UI
        // =======================
        public IActionResult ClassroomUI()
        {
            var courses = db.Courses
                .OrderBy(c => c.Id)
                .ToList();

            return View("~/Views/Student/ClassroomUI.cshtml", courses);
        }

        // =======================
        // GET MATERIALS (AJAX)
        // =======================
        [HttpGet]
        public IActionResult GetMaterials(string courseId)
        {
            var materials = db.Materials
                .Where(m => m.CourseId == courseId)
                .OrderByDescending(m => m.Id)
                .Select(m => new { m.Id, m.Title, m.FileUrl })
                .ToList();

            return Json(materials);
        }
        // faq
        public IActionResult FAQ()
        {
            //no db
            return View("~/Views/Student/FAQ.cshtml");
        }
        // =======================
        // STUDENT SUPPORT / CREATE TICKET
        // =======================
        [HttpGet]
        public IActionResult CreateTicket()
        {
            return View("~/Views/Student/StudentTicketCreate.cshtml");
        }

        [HttpPost]
        public IActionResult CreateTicket(SupportTicket ticket)
        {
            //if (!ModelState.IsValid)
            //{
            //    // Debug: what errors exist?
            //    var errors = ModelState.Values.SelectMany(v => v.Errors)
            //                                  .Select(e => e.ErrorMessage)
            //                                  .ToList();
            //    return Content("ModelState invalid: " + string.Join(", ", errors));
            //}

            //check last id
            var lastTicketId = db.SupportTickets
                .OrderByDescending(t => t.Id)
                .Select(t => t.Id)
                .FirstOrDefault();

            //generate new id 
            string newTicketId;

            if (string.IsNullOrEmpty(lastTicketId))
            {
                newTicketId = "ST0001";
            }
            else
            {
                var number = int.Parse(lastTicketId.Substring(2));
                newTicketId = "ST" + (number + 1).ToString("D4");
            }

            ticket.Id = newTicketId;
            ticket.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ticket.Status = "Open";
            ticket.Priority = ticket.Priority ?? "Normal";
            ticket.CreatedAt = DateTime.Now;
            ticket.ResolvedAt = null;

            try
            {
                db.SupportTickets.Add(ticket);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                return Content("Error saving ticket: " + ex.Message);
            }

            TempData["Message"] = "Ticket submitted successfully!";
            return RedirectToAction("TicketList");
        }

        // =======================
        // STUDENT TICKET LIST
        // =======================
        public IActionResult TicketList()
        {
            //logged in user id
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var tickets = db.SupportTickets
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToList();

            return View("~/Views/Student/StudentTicketList.cshtml", tickets);
        }
        [Authorize]
        public IActionResult TicketDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var ticket = db.SupportTickets
                .Include(t => t.TicketMessages)   // replies
                .FirstOrDefault(t => t.Id == id && t.UserId == userId);

            if (ticket == null)
                return NotFound();

            // sorting by sent at
            ticket.TicketMessages = ticket.TicketMessages
                .OrderBy(m => m.SentAt)
                .ToList();

            return View("~/Views/Student/StudentTicketDetails.cshtml", ticket);
        }
        [HttpGet]
        public IActionResult GetTickets(string search = "", string status = "")
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tickets = db.SupportTickets
                            .Where(t => t.UserId == userId);

            if (!string.IsNullOrEmpty(search))
                tickets = tickets.Where(t => t.Id.Contains(search) || t.Subject.Contains(search));

            if (!string.IsNullOrEmpty(status))
                tickets = tickets.Where(t => t.Status == status);

            tickets = tickets.OrderByDescending(t => t.CreatedAt);

            return PartialView("_TicketTable", tickets.ToList());
        }
        //reply ticket
        [HttpPost]
        [ValidateAntiForgeryToken]
        //public IActionResult Reply(string ticketId, string message)
        //{
        //    //validation
        //    if (string.IsNullOrEmpty(ticketId) || string.IsNullOrWhiteSpace(message))
        //    {
        //        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        //            return Json(new { success = false, message = "Please enter a message." });

        //        TempData["Message"] = "Please enter a message.";
        //        return RedirectToAction("TicketDetails", new { id = ticketId });
        //    }

        //    var ticket = db.SupportTickets.FirstOrDefault(t => t.Id == ticketId);
        //    if (ticket == null) return NotFound();

        //    // ticket logic
        //    var reply = new TicketMessage
        //    {
        //        Id = Guid.NewGuid().ToString(),
        //        SupportTicketId = ticketId,
        //        SenderId = User.FindFirstValue(ClaimTypes.NameIdentifier),
        //        Content = message,
        //        SentAt = DateTime.Now
        //    };

        //    db.TicketMessages.Add(reply);
        //    db.SaveChanges();

        //    // check ajax
        //    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        //    {
        //        return Json(new
        //        {
        //            success = true,
        //            sender = reply.SenderId,
        //            time = reply.SentAt.ToString("dd/MM/yyyy HH:mm"),
        //            content = reply.Content
        //        });
        //    }

        //    TempData["Message"] = "Reply sent successfully!";
        //    return RedirectToAction("TicketDetails", new { id = ticketId });
        //}
        [HttpGet]
        public IActionResult GetRepliesPartial(string id)
        {
            var ticket = db.SupportTickets
                           .Include(t => t.TicketMessages)
                           .FirstOrDefault(t => t.Id == id);

            if (ticket == null) return NotFound();

            var messages = ticket.TicketMessages.OrderBy(m => m.SentAt).ToList();

            // NOTE: Ensure this matches the Partial View filename in your Student folder
            return PartialView("_TicketMessage", messages);
        }










        //// List tickets for current student
        //public IActionResult TicketList()
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //    var tickets = db.SupportTickets
        //                    .Where(t => t.UserId == userId)
        //                    .OrderByDescending(t => t.CreatedAt)
        //                    .ToList();

        //    return View("~/Views/Student/StudentTicketList.cshtml", tickets);
        //}

        // Ticket details
        //public IActionResult TicketDetails(string id)
        //{
        //    if (string.IsNullOrEmpty(id))
        //        return NotFound();

        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //    var ticket = db.SupportTickets
        //                   .Include(t => t.TicketMessages)
        //                   .FirstOrDefault(t => t.Id == id && t.UserId == userId);

        //    if (ticket == null) return NotFound();

        //    ticket.TicketMessages = ticket.TicketMessages
        //                                 .OrderBy(m => m.SentAt)
        //                                 .ToList();

        //    return View("~/Views/Student/StudentTicketDetails.cshtml", ticket);
        //}

        // AJAX: Reply to ticket
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reply(string ticketId, string message)
        {
            if (string.IsNullOrEmpty(ticketId) || string.IsNullOrWhiteSpace(message))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = "Please enter a message." });

                TempData["Message"] = "Please enter a message.";
                return RedirectToAction("TicketDetails", new { id = ticketId });
            }

            var ticket = db.SupportTickets.FirstOrDefault(t => t.Id == ticketId && t.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (ticket == null) return NotFound();

            // Students cannot close tickets, so no status check required here

            string msgId = $"TM{(db.TicketMessages.Count() + 1):D4}";

            var ticketMessage = new TicketMessage
            {
                Id = msgId,
                SupportTicketId = ticketId,
                SenderId = User.FindFirstValue(ClaimTypes.NameIdentifier),
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
            return RedirectToAction("TicketDetails", new { id = ticketId });
        }

        //// AJAX: Get latest replies for a ticket
        //[HttpGet]
        //public IActionResult GetRepliesPartial(string id)
        //{
        //    var ticket = db.SupportTickets
        //                   .Include(t => t.TicketMessages)
        //                   .FirstOrDefault(t => t.Id == id && t.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier));

        //    if (ticket == null) return NotFound();

        //    var messages = ticket.TicketMessages.OrderBy(m => m.SentAt).ToList();
        //    return PartialView("
        //    ", messages); // make sure your partial matches the Student view
        //}
        //task section
        public IActionResult TaskList()
        {
            var userId = User.Identity.Name;

            var tasks = db.CourseTasks
                .Include(x => x.Submissions)
                .Where(x => x.ClassSectionId == "SEC001") // adjust
                .OrderBy(x => x.DueDate)
                .ToList();

            return View(tasks);
        }

        public IActionResult TaskSubmit(string id)
        {
            var task = db.CourseTasks.FirstOrDefault(x => x.Id == id);

            var userId = User.Identity.Name;

            var submission = db.Submissions
                .FirstOrDefault(x => x.StudentId == userId && x.CourseTaskId == id);

            ViewBag.Submitted = submission;

            ViewBag.IsLate = (DateTime.Now > task.DueDate);

            return View(task);
        }

        [HttpPost]
        public IActionResult Upload(string id, IFormFile file)
        {
            var task = db.CourseTasks.Find(id);

            var userId = User.Identity.Name;

            if (task.DueDate < DateTime.Now)
            {
                TempData["error"] = "Due date passed. Cannot submit.";
                return RedirectToAction("TaskSubmit", new { id });
            }

            string newId = "S" + (db.Submissions.Count() + 1).ToString("D4");

            string fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + file.FileName;
            string path = Path.Combine("wwwroot/Submissions", fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            Submission sub = new Submission
            {
                Id = newId,
                CourseTaskId = id,
                StudentId = userId,
                FileUrl = "/Submissions/" + fileName,
                SubmittedAt = DateTime.Now
            };

            db.Submissions.Add(sub);
            db.SaveChanges();

            TempData["msg"] = "Uploaded!";
            return RedirectToAction("TaskSubmit", new { id });
        }

        public async Task<IActionResult> ViewProgramCourse()
        {
            await LoadProfilePicture();
            ViewData["Title"] = "Program Courses";

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var student = await db.Students
                .Include(s => s.Program)
                    .ThenInclude(p => p.ProgramCourses)
                        .ThenInclude(pc => pc.Course)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == userId);

            if (student == null || student.Program == null)
            {
                TempData["ErrorMessage"] = "Student program not found.";
                return RedirectToAction("Dashboard");
            }

            return View(student);
        }

        // PAGE VIEW
        public IActionResult Index(string classId)
        {
            if (string.IsNullOrEmpty(classId))
                return Content("No class selected.");

            var userId = User.Identity.Name;

            var messages = db.ChatMessages;
                //.Where(x => x.ClassSectionId == classId)
                //.OrderBy(x => x.SentAt)
                //.ToList();

            ViewBag.ClassId = classId;
            ViewBag.UserId = userId;

            return View(messages);
        }

        // SEND MESSAGE
        [HttpPost]
        public IActionResult Send(string classId, string content)
        {
            var userId = User.Identity.Name;

            var msg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = userId,
                ReceiverId = null,
                Content = content,
                SentAt = DateTime.Now,
                IsRead = false
            };

            db.ChatMessages.Add(msg);
            db.SaveChanges();

            return RedirectToAction("Index", new { classId });
        }
        ////test
        public IActionResult DebugStudent()
        {
            string studentId = "2500001";

            // 1. Get the student without Includes to avoid SQL crashes
            var student = db.Students.FirstOrDefault(s => s.Id == studentId);

            if (student == null) return Content("Student not found.");

            var report = new System.Text.StringBuilder();
            report.AppendLine("====== RAW STUDENT DATA (PROPERTY DUMP) ======");

            // 2. Automatically loop through every property in the class
            var properties = student.GetType().GetProperties();
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(student, null) ?? "NULL";
                    report.AppendLine($"[{prop.Name}]: {value}");
                }
                catch (Exception ex)
                {
                    report.AppendLine($"[{prop.Name}]: ERROR READING ({ex.Message})");
                }
            }

            report.AppendLine("\n==============================================");
            report.AppendLine("Note: If a property is NULL, it means the column is empty ");
            report.AppendLine("or you need a separate query to fetch that related table.");

            return Content(report.ToString());
        }

    }
}