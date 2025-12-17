using Microsoft.EntityFrameworkCore;
using SHAPEE.Models;

namespace SHAPEE.Services
{
    public class LeaveService
    {
        private readonly DB _db;

        public LeaveService(DB db)
        {
            _db = db;
        }

        public void ApplyApprovedLeaveToAttendance(string studentId, DateOnly startDate, DateOnly endDate)
        {
            var student = _db.Students.FirstOrDefault(s => s.Id == studentId);
            if (student == null || !student.TutorialGroupId.HasValue)
                return;

            var tutorialGroupId = student.TutorialGroupId.Value;

            // All sections this tutorial group attend
            var sections = _db.ClassSections
                .Include(cs => cs.Semester)
                .Where(cs => cs.TutorialGroupId == tutorialGroupId)
                .ToList();

            if (!sections.Any())
                return;

            // Day mapping for section
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

                    // Semester window (DateOnly -> DateOnly comparison)
                    var semStart = cs.Semester.StartDate;
                    var semEnd = cs.Semester.EndDate;
                    if (date < semStart || date > semEnd)
                        return false;

                    var dayLower = cs.Day.ToLower();

                    // Match full name or abbreviation
                    return dayLower == todayNameLower || dayLower.StartsWith(todayAbbrevLower);
                }).ToList();

                if (!sectionsForDay.Any())
                    continue;

                foreach (var section in sectionsForDay)
                {
                    var sectionId = section.SectionId;

                    var att = _db.Attendances
                        .FirstOrDefault(a =>
                            a.ClassSectionId == sectionId &&
                            a.StudentId == studentId &&
                            a.Date == date);

                    if (att == null)
                    {
                        _db.Attendances.Add(new Attendance
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
                        // If you want to convert both Absent and Present to Leave:
                        if (att.Status != "Leave")
                        {
                            att.Status = "Leave";
                            att.FlagReason = "Approved leave (updated from " + att.Status + ")";
                        }
                    }
                }
            }

            _db.SaveChanges();
        }
    }
}