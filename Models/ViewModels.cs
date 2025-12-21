using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
namespace SHAPEE.Models
#nullable disable warnings

{
    // Required by the Login action
    public class LoginVM
    {
        [Required(ErrorMessage = "User ID is required.")]
        [MaxLength(50)]
        public string Id { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }

    // Required by the ForgotPassword action
    public class ForgotPasswordVM
    {
        [Required(ErrorMessage = "User ID is required to reset password.")]
        [Display(Name = "Your User ID")]
        public string Id { get; set; }
    }

    // Required by the ResetPassword action
    public class ResetPasswordVM
    {
        public string UserId { get; set; }
        public string Token { get; set; }

        [Required(ErrorMessage = "New Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirmation is required.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }
    }

    public class FacultyVM
    {
        [Required(ErrorMessage = "Faculty Id is required.")]
        public string FacultyId { get; set; }

        [Required(ErrorMessage = "Faculty Name is required.")]
        public string FacultyName { get; set; }

        [Required(ErrorMessage = "Cost Per Credit Hour is required.")]

        public decimal CostPerCreditHour { get; set; }
    }

    public class FacultyManageVM
    {
        public List<Faculty> Faculties { get; set; } = [];
        public Faculty? EditFaculty { get; set; }
    }

    //YL


    public class CourseManageVM
    {
        public List<Course>? Courses { get; set; } = [];

        public string? CourseId { get; set; }
        public string? CourseName { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Credit hours must be at least 1.")]
        public int? CreditHours { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Lecture hours must be greater than 0.")]
        public int? LectureHours { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Practical hours must be greater than 0.")]
        public int? PracticalHours{get; set;}

        [Range(1, int.MaxValue, ErrorMessage = "Tutorial hours must be greater than 0.")]
        public int? TutorialHours { get; set; }
    }

    public class GroupManageVM
    {
        public List<TutorialGroup>? Groups { get; set; } = [];

        public string? GroupName { get; set; }

        [Range(1, 20, ErrorMessage = "Group number must be between 1 and 20.")]
        public int? GroupNumber { get; set; }
        public string? ProgramId { get; set; }  
        public int? YearLevel { get; set; }
        public int? Semester { get; set; }

    }
    public class ProgramManageVM
    {
        public List<Program>? Program { get; set; } = [];
        public Program? EditProgram { get; set; }

        [Required(ErrorMessage = "Program ID is required.")]
        public string? ProgramId { get; set; }

        [Required(ErrorMessage = "Program Name is required.")]
        public string? ProgramName { get; set; }

        [Required(ErrorMessage = "Faculty must be selected.")]
        public string? FacultyId { get; set; }
    }

    public class ProgramCourseVM
    {
        public List<Faculty>? Faculties { get; set; } = [];
        public List<Program>? Programs { get; set; } = [];
        public List<ProgramCourse>? ProgramCourses { get; set; } = [];
        public Faculty? SelectedFaculty {  get; set; }
        public Program? SelectedProgram { get;  set; }
        public string? CourseId { get; set; } 
    }
    public class AssignGroupVM
    {
        public List<TutorialGroup>? Groups { get; set; } = [];
        public List<Student>? StudentList { get; set; } = [];
        public int? SelectedGroupId { get; set; }
        public string? ProgramId { get; set; }
        public int? YearLevel { get; set; }
        public int? Semester { get; set; }
    }

    public class ClassSectionVM
    {
        public List<Faculty>? Faculties { get; set; } = [];
        public List<int>? GroupList { get; set; } = [];
        public List<ClassSection>? ClassSections { get; set; } = [];
        public ClassSection? EditClassSection { get; set; }
        public string? SectionId { get; set; }
        public string? SemesterId { get; set; }
        public string? RoomId { get; set; }
        public string? FacultyId { get; set; }
        public string? ProgramId { get; set; } 
        public string? Day { get; set; }

        public TimeOnly? StartTime { get; set; }

        public int? GroupId { get; set; }

    } 
    public class ProgramCsvVM
    {
        [Display(Name = "Program ID")]
        public string ProgramId { get; set; }

        [Display(Name = "Program Name")]
        public string ProgramName { get; set; }

        [Display(Name = "Faculty")]
        public string FacultyName { get; set; }

    }

    public class CourseCsvVM
    {
        [Display(Name = "Course ID")]
        public string CourseId { get; set; }
        [Display(Name = "Course Name")]
        public string CourseName { get; set; }
        [Display(Name = "Credit Hours")]
        public int CreditHours { get; set; }
    }

    public class GroupCsvVM
    {
        [Display(Name = "Group Name")]
        public string GroupName { get; set; }

        [Display(Name = "Program Name")]
        public string ProgramName { get; set; }

        [Display(Name = "Group Number")]
        public int GroupNumber { get; set; }

        [Display(Name = "Year Level")]
        public int YearLevel { get; set; }

        [Display(Name = "Semester")]
        public int Semester { get; set; }

        [Display(Name = "Number of Student")]
        public int NumOfStudent { get; set; } 
    }


    //KM

    public class ClassSelectionItemVM
    {
        public ClassSection Section { get; set; }
        public bool CanStart { get; set; }
        public string? DisabledReason { get; set; }
    }

    public class SessionSummaryVM
    {
        public ClassSection Section { get; set; }
        public int TotalEnrolled { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int FlaggedCount { get; set; }
        public double AttendancePercentage { get; set; }
        public List<Student> PresentStudents { get; set; } = [];
        public List<Student> AbsentStudents { get; set; } = [];
        public List<Attendance> FlaggedAttendances { get; set; } = [];
        public DateOnly Today { get; set; }
    }

    public class AttendanceManagementVM
    {
        public ClassSection Section { get; set; }
        public int TotalStudents { get; set; }
        public double AveragePresent { get; set; }
        public int AveragePresentPercentage { get; set; }
        public int TotalAbsences { get; set; }
        public int FlaggedCount { get; set; }
        public List<Attendance> Attendances { get; set; } = [];
        public List<Student> EnrolledStudents { get; set; } = [];
    }

    //student dashboard
    public class StudentDashboardVM
    {
        public List<SubjectAttendanceSummaryVM> SubjectAttendances { get; set; } = new();
        public List<Announcement> LatestAnnouncements { get; set; } = new();
        public List<TodayScheduleItemVM> TodaySchedule { get; set; } = new();
    }

    public class TodayScheduleItemVM
    {
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public string RoomLocation { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string Status { get; set; } // Upcoming, Inprogress, Completed, Absent
    }

    // student view attendacne
    public class StudentAttendanceVM
    {
        //for summary view (list of classes)
        public List<StudentAttendanceItemVM> ClassAttendances { get; set; } = [];
        
        // overall statistics
        public int OverallPresent { get; set; }
        public int OverallAbsent { get; set; }
        public int OverallLeave { get; set; }
        public double OverallPercentage { get; set; }
        public List<Attendance> RecentAttendances { get; set; } = [];
        
        // qr/token for taking attendance
        public string? SectionId { get; set; }
        public string? QrToken { get; set; }
        
        // filter options
        public List<SubjectFilterOption> AvailableSubjects { get; set; } = [];
        public List<SemesterFilterOption> AvailableSemesters { get; set; } = [];
        public string? SelectedSemesterId { get; set; }
    }

    public class SubjectFilterOption
    {
        public string CourseId { get; set; }
        public string CourseName { get; set; }
    }

    public class SemesterFilterOption
    {
        public string SemesterId { get; set; }
        public string DisplayText { get; set; }
    }

    public class StudentAttendanceItemVM
    {
        public ClassSection ClassSection { get; set; }
        public Course Course { get; set; }

        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int TotalSessions { get; set; }
        public double AttendancePercentage { get; set; }
    }


    // student attendance details view model
    public class StudentAttendanceDetailsVM
    {
        public ClassSection? ClassSection { get; set; }  
        public Course? Course { get; set; }  
        public Semester? Semester { get; set; }  
        
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int TotalSessions { get; set; }
        public double AttendancePercentage { get; set; }
        public List<Attendance> Attendances { get; set; } = [];
    }

    //attendance by subject
    public class SubjectAttendanceSummaryVM
    {
        public string CourseId { get; set; }
        public string CourseName { get; set; }

        public int Present { get; set; }
        public int Absent { get; set; }
        public int Leave { get; set; }  // currently 0, since you only store Present/Absent
        public double Percentage { get; set; }
    }

    // academic calendar
    public class SemesterConfigVM
    {
        public List<Semester> Semesters { get; set; } = [];
        public List<SemesterWithTypeInfo> SemestersWithType { get; set; } = [];
    }

    public class SemesterWithTypeInfo
    {
        public Semester Semester { get; set; }
        public string CalculatedType { get; set; }
        public double Weeks { get; set; }
    }

    public class EventManagementVM
    {
        public List<CalendarEvent> Events { get; set; } = [];
        public List<Semester> Semesters { get; set; } = [];
        public string? SelectedSemesterId { get; set; }
        public List<EventWithSemesterInfo> EventsWithSemester { get; set; } = [];
    }

    public class EventWithSemesterInfo
    {
        public CalendarEvent Event { get; set; }
        public Semester? Semester { get; set; }
    }

    public class CalendarEventVM
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Type { get; set; }
        public string? SemesterId { get; set; }
        public string Color { get; set; }
        public bool AllDay { get; set; }
    }

    public class ConflictCheckVM
    {
        public bool HasConflict { get; set; }
        public List<CalendarEvent> ConflictingEvents { get; set; } = [];
        public string Message { get; set; } = "";
    }

    public class InvoiceRowVM
    {
        public string InvoiceId { get; set; }
        public string SemesterId { get; set; }
        public DateTime DueDate { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsPaid { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class CurrentBillingVM
    {
        public string StudentStatus { get; set; } = "Active";

        public int YearLevel { get; set; }
        public int SemesterNo { get; set; }

        // active semester record (from Semesters table
        public string ActiveSemesterId { get; set; }

        public int TotalCredits { get; set; }
        public decimal CostPerCreditHour { get; set; }

        // current invoice 
        public string? CurrentInvoiceId { get; set; }
        public DateTime? CurrentInvoiceDueDate { get; set; }
        public decimal CurrentInvoiceAmount { get; set; }
        public bool CurrentInvoiceIsPaid { get; set; }

        // list table
        public decimal TotalPayable { get; set; }
        public List<InvoiceRowVM> OutstandingInvoices { get; set; } = [];
    }

    public class PaymentCheckoutVM
    {
        public string InvoiceId { get; set; }
        public string SemesterId { get; set; }
        public DateTime DueDate { get; set; }
        public decimal TotalAmount { get; set; }

        public int TotalCredits { get; set; }
        public decimal CostPerCreditHour { get; set; }
        public int SubjectCount { get; set; }
    }

    public class PaymentTransactionRowVM
    {
        public string ReceiptId { get; set; }
        public string InvoiceId { get; set; }
        public string SemesterId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string PaymentMethod { get; set; }
        public decimal AmountPaid { get; set; }
    }

    public class PaymentHistoryVM
    {
        public List<PaymentTransactionRowVM> Transactions { get; set; } = [];
    }

    //Payment Transaction Management
    public class PaymentTransactionManagementVM
    {
        public List<AdminPaymentTransactionRowVM> Transactions { get; set; } = [];
        public string? SearchQuery { get; set; }
        public string? FacultyFilter { get; set; }
        public string? ProgramFilter { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? DateSort { get; set; } 
    }

    public class AdminPaymentTransactionRowVM
    {
        public string ReceiptId { get; set; }
        public string InvoiceId { get; set; }
        public string StudentId { get; set; }
        public string StudentName { get; set; }
        public string SemesterId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string PaymentMethod { get; set; }
        public decimal AmountPaid { get; set; }
    }

    //Payment monitoring and reports
    public class PaymentMonitoringVM
    {
        public decimal TotalRevenue { get; set; }
        public decimal OutstandingPayments { get; set; }
        public int OverduePaymentsCount { get; set; }
        public string Period { get; set; } = "all"; // all, today, week, month, year, custom
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<PaymentTrendVM> PaymentTrends { get; set; } = [];
        public List<OverdueStudentVM> OverdueStudents { get; set; } = [];
    }

    public class PaymentTrendVM
    {
        public string Month { get; set; }
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
    }

    public class OverdueStudentVM
    {
        public string StudentId { get; set; }
        public string StudentName { get; set; }
        public string InvoiceId { get; set; }
        public string SemesterId { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysOverdue { get; set; }
    }

    public class PaymentReportCsvVM
    {
        [Display(Name = "Receipt ID")]
        public string ReceiptId { get; set; }
        
        [Display(Name = "Invoice ID")]
        public string InvoiceId { get; set; }
        
        [Display(Name = "Student ID")]
        public string StudentId { get; set; }
        
        [Display(Name = "Student Name")]
        public string StudentName { get; set; }
        
        [Display(Name = "Semester")]
        public string SemesterId { get; set; }
        
        [Display(Name = "Transaction Date")]
        public string TransactionDate { get; set; }
        
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; }
        
        [Display(Name = "Amount Paid")]
        public decimal AmountPaid { get; set; }
    }

    //Staff Dashboard VM
    public class StaffDashboardVM
    {
        public int TotalClasses { get; set; }
        public int TotalStudents { get; set; }
        public double AverageAttendance { get; set; }
        public List<ClassSummaryVM> MyClasses { get; set; } = new List<ClassSummaryVM>();
        public List<ScheduleItemVM> TodaySchedule { get; set; } = new List<ScheduleItemVM>();
        public List<PendingAttendanceVM> PendingAttendance { get; set; } = new List<PendingAttendanceVM>();
        public DateOnly TodayDate { get; set; }
    }

    public class ClassSummaryVM
    {
        public string SectionId { get; set; }
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public string SectionType { get; set; }
        public string Room { get; set; }
        public string Days { get; set; }
        public string Time { get; set; }
        public int StudentCount { get; set; }
        public double AttendancePercentage { get; set; }
    }

    public class ScheduleItemVM
    {
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string CourseName { get; set; }
        public string Room { get; set; }
    }

    public class PendingAttendanceVM
    {
        public string SectionId { get; set; }
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public TimeOnly StartTime { get; set; }
        public DateOnly Date { get; set; }
    }
        
    // JS
    public class RoomManageVM
    {
        public List<Room> Rooms { get; set; } = [];
        public Room? EditRoom { get; set; }
    }

    public class RoomCsvVM
    {
        [Display(Name = "Room ID")]
        public string RoomId { get; set; }

        [Display(Name = "Type")]
        public string Type { get; set; }

        [Display(Name = "Block")]
        public string Block { get; set; }

        [Display(Name = "Floor")]
        public int Floor { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; }
    }

    public class LeaveApplicationVM
    {
        [Required(ErrorMessage = "Start date is required")]
        [Display(Name = "Start Date")]
        public DateOnly StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        [Display(Name = "End Date")]
        public DateOnly EndDate { get; set; }

        [Required(ErrorMessage = "Leave type is required")]
        [Display(Name = "Leave Type")]
        public string? LeaveType { get; set; }

        [Required(ErrorMessage = "Reason is required")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        [MinLength(10, ErrorMessage = "Reason must be at least 10 characters")]
        [Display(Name = "Reason for Leave")]
        public string? Reason { get; set; }
    }

    public class AnnouncementManageVM
    {
        public List<Announcement> Announcements { get; set; } = [];
        public Announcement? EditAnnouncement { get; set; }
        public Staff? Staff { get; set; }
    }
    public class StudentTimetableVM
    {
        public Student Student { get; set; }
        public List<ClassSection> ClassSections { get; set; } = new List<ClassSection>();
        public List<Semester> Semesters { get; set; } = new List<Semester>();
        public string? SelectedSemesterId { get; set; }
        public Semester? SelectedSemester { get; set; }
        public int TotalWeeks { get; set; }
        public int SelectedWeekNumber { get; set; }
        public DateTime? WeekStartDate { get; set; }
        public DateTime? WeekEndDate { get; set; }
        public Dictionary<string, double> AttendancePercentages { get; set; } = new();
    }


    public class EmailVs
    {
        public string Email { get; set; }
        public string Subject { get; set; }

        public string Message { get; set; }
    }


    // For displaying Google Classroom-style materials and tasks
    public class ClassworkVM
    {
        // Grouping everything by Course so it looks organized
        public string CourseId { get; set; }
        public string CourseName { get; set; }

        // The items inside the "Classroom"
        public List<Material> CourseMaterials { get; set; } = [];
        public List<CourseTask> CourseTasks { get; set; } = [];
    }
}