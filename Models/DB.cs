using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable disable warnings

namespace SHAPEE.Models;

public class DB(DbContextOptions options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Staff> Staffs { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Faculty> Faculties { get; set; }
    public DbSet<ResetPassToken> ResetPassTokens { get; set; }

    public DbSet<Program> Programs { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<ProgramCourse> ProgramCourses { get; set; }
    public DbSet<ClassSection> ClassSections { get; set; }
    public DbSet<TutorialGroup> TutorialGroups { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }

    public DbSet<Room> Rooms { get; set; }
    public DbSet<Announcement> Announcements { get; set; }
    public DbSet<LeaveApplication> LeaveApplications { get; set; }

    public DbSet<Semester> Semesters { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<CalendarEvent> CalendarEvents { get; set; }

    public DbSet<Material> Materials { get; set; }
    public DbSet<CourseTask> CourseTasks { get; set; }
    public DbSet<Submission> Submissions { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<SupportTicket> SupportTickets { get; set; }
    public DbSet<TicketMessage> TicketMessages { get; set; }

    //YQ
    public class User
    {

        [Key, MaxLength(50)]
        public string Id { get; set; }

        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        [MaxLength(100)]
        public string Email { get; set; }

        [MaxLength(100)]
        public string Hash { get; set; }

        [MaxLength(100)]
        public string? Contact { get; set; }

        public string Role => GetType().Name;

        public int LoginAttempts { get; set; } = 0;

        [MaxLength(20)]
        public string Status { get; set; } = "Active"; //Active, Locked

        [NotMapped]
        public string FullName => $"{LastName} {FirstName} ";
    }

    public class Admin : User
    {
    }

    public class Staff : User
    {
        public DateOnly DateJoin { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        [MaxLength(10)]
        public string Gender { get; set; }

        [MaxLength(200)]
        public string ProfilePictureUrl { get; set; }

        //FK to faculty -- 1 staff belong to 1 faculty
        public string? FacultyId { get; set; }
        public Faculty Faculty { get; set; }
    }

    public class Student : User
    {
        public DateOnly DateJoin { get; set; } = DateOnly.FromDateTime(DateTime.Now);
        public int YearLevel { get; set; }
        public int Semester { get; set; }

        [MaxLength(10)]
        public string Gender { get; set; }

        [MaxLength(200)]
        public string ProfilePictureUrl { get; set; }

        //FK to Program -- 1 user belong to 1 program
        public string? ProgramId { get; set; }

        public Program Program { get; set; }

        //FK to TutorialGroup -- 1 student belong to 1 tutorial group
        public string? TutorialGroupId { get; set; }
        public TutorialGroup TutorialGroup { get; set; }

        //1 student have many enrollments
        public List<Enrollment> Enrollments { get; set; } = [];

        //1 student have many attendance records
        public List<Attendance> Attendances { get; set; } = [];

        //1 student have many invoices
        public List<Invoice> Invoices { get; set; } = [];

        //1 student have many submissions
        public List<Submission> Submissions { get; set; } = [];

    }

    public class Faculty
    {
        [Key, MaxLength(50)]
        public string Id { get; set; }
        [MaxLength(100)]
        public string Name { get; set; }

        [Precision(3, 2)]
        public decimal CostPerCreditHour { get; set; }

        // 1 Faculty have many programs
        public List<Program> Programs { get; set; } = [];

        //1 faculty have many staffs
        public List<Staff> Staffs { get; set; } = [];
    }

    public class ResetPassToken
    {
        [Key, MaxLength(50)]
        public string TokenId { get; set; }

        // FK to User
        public string UserId { get; set; }
        public User User { get; set; }

        public DateTime ExpiryAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool Used { get; set; } = false;
    }

    //YL
    public class Program
    {
        [Key, MaxLength(50)]
        public string Id { get; set; }
        [MaxLength(100)]
        public string Name { get; set; }

        //FK to faculty -- 1 program belongs to one faculty
        public string FacultyId { get; set; }
        public Faculty Faculty { get; set; }

        //1 program have many students
        public List<Student> Students { get; set; } = [];

        //Many to Many Relationship for Program and Course
        public List<ProgramCourse> ProgramCourses { get; set; }

        //1 program have many tutorial groups
        public List<TutorialGroup> TutorialGroups { get; set; } = [];
    }

    public class Course
    {
        [Key, MaxLength(10)]
        public string CourseCode { get; set; }

        [MaxLength(100)]
        public string Name { get; set; }

        public int CreditHours { get; set; }

        //Many to Many Relationship for Program and Course
        public List<ProgramCourse> ProgramCourses { get; set; }

    }

    public class ProgramCourse //Define the program have what course in each year and semester
    {
        [Key]
        public int Id { get; set; }
        //FK to Program
        public string ProgramId { get; set; }
        public Program Program { get; set; }

        //FK to Course
        public string CourseCode { get; set; }
        [ForeignKey("CourseCode")]
        public Course Course { get; set; }

        public int YearLevel { get; set; }
        public int Semester { get; set; }
    }

    public class ClassSection
    {
        [Key, MaxLength(50)]
        public string SectionId { get; set; }

        //FK to Course -- 1 class section belong to 1 course
        public string CourseCode { get; set; }
        [ForeignKey("CourseCode")]
        public Course Course { get; set; }

        //FK to Staff -- 1 class section belong to 1 staff
        public string StaffId { get; set; }
        public Staff Staff { get; set; }

        //FK to Semester -- 1 class section belong to 1 semester
        public string SemesterId { get; set; } //202501 /202505 / 202509
        public Semester Semester { get; set; }

        public int YearLevel { get; set; }

        public int Sem { get; set; }

        [MaxLength(10)]
        public string Day { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        public string SectionType { get; set; } //Lecture, Practical, Tutorial

        //FK to TutorialGroup -- 1 class section belong to 1 tutorial group
        public string TutorialGroupId { get; set; }
        public TutorialGroup TutorialGroup { get; set; }

        //FK to Room -- 1 class section belong to 1 room
        public string RoomId { get; set; }
        public Room Room { get; set; }

        [MaxLength(10)]
        public string? AttendanceCode { get; set; }

        [MaxLength(10)]
        public string? JoinCode { get; set; }

        //1 class section have many enrollments
        public List<Enrollment> Enrollments { get; set; } = [];

        //1 class section have many attendance records
        public List<Attendance> Attendances { get; set; } = [];

    }

    public class TutorialGroup
    {
        [Key, MaxLength(50)]
        public string GroupId { get; set; }

        //FK to Program -- 1 tutorial group belong to 1 program
        public string ProgramId { get; set; }
        public Program Program { get; set; }

        public int YearLevel { get; set; }

        public int Semester { get; set; }

        public int NumberOfStudents { get; set; }

        //1 tutorial group have many students
        public List<Student> Students { get; set; } = [];

    }

    public class Enrollment
    {
        [Key]
        public int EnrollmentId { get; set; }

        //FK to Student--1 enrollment belong to 1 student
        public string StudentId { get; set; }
        public Student Student { get; set; }

        //FK to ClassSection --1 enrollment belong to 1 class section
        public string SectionId { get; set; }
        public ClassSection ClassSection { get; set; }

        [MaxLength(20)]
        public string Type { get; set; } //Enrolled, Dropped

        [MaxLength(200)]
        public string? Reason { get; set; }

        public DateOnly EnrollmentDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; //Approved, Rejected, Pending 
    }

    //JS
    public class Room
    {
        [Key, MaxLength(50)]
        public string RoomId { get; set; }

        [MaxLength(100)]
        public string Type { get; set; }

        [MaxLength(10)]
        public string Block { get; set; }

        public int Floor { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } //Available, Under Maintenance

    }

    public class Announcement
    {
        [Key, MaxLength(50)]
        public string AnnouncementId { get; set; }
        [MaxLength(100)]
        public string Title { get; set; }

        [MaxLength(500)]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(50)]
        public string Author { get; set; }
    }

    public class LeaveApplication
    {
        [Key]
        public int LeaveId { get; set; }

        //FK to User -- 1 leave application belong to 1 user (staff or student)
        public string? UserId { get; set; }
        public User User { get; set; }

        public DateOnly StartDate { get; set; }

        public DateOnly EndDate { get; set; }

        [MaxLength(50)]
        public string LeaveType { get; set; } //Sick Leave, Casual Leave, Emergency Leave

        [MaxLength(200)]
        public string Reason { get; set; }

        [MaxLength(200)]
        public string SupportDocumentUrl { get; set; }

        [MaxLength(200)]
        public string? RejectReason { get; set; }

        public DateOnly ApplicationDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);

        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; //Pending, Approved, Rejected
    }

    //KM
    public class Semester
    {
        [Key, MaxLength(10)]
        public string SemesterId { get; set; } //202501 /202505 / 202509

        public DateOnly StartDate { get; set; }

        public DateOnly EndDate { get; set; }

        public bool IsActive { get; set; }

        //1 semester have many class sections
        public List<ClassSection> ClassSections { get; set; } = [];

        //1 semester have many invoices
        public List<Invoice> Invoices { get; set; } = [];
    }

    public class Invoice
    {
        [Key, MaxLength(50)]
        public string InvoiceId { get; set; }

        //FK to Student -- 1 invoice belong to 1 student
        public string? StudentId { get; set; }
        public Student Student { get; set; }

        //FK to Semester -- 1 invoice belong to 1 semester
        public string SemesterId { get; set; }
        public Semester Semester { get; set; }

        [Precision(5, 2)]
        public decimal TotalAmount { get; set; }

        public bool IsPaid { get; set; }

        public DateTime DueDate { get; set; }

        public DateTime GenerateDate { get; set; } = DateTime.Now;

    }

    public class PaymentTransaction
    {
        [Key, MaxLength(50)]
        public string TransactionId { get; set; }

        //FK to Invoice -- 1 payment belong to 1 invoice
        public string InvoiceId { get; set; }
        public Invoice Invoice { get; set; }

        [Precision(5, 2)]
        public decimal AmountPaid { get; set; }

        [MaxLength(100)]
        public string PaymentMethod { get; set; } //Credit Card, Debit Card, Online Banking, Cash

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [MaxLength(200)]
        public string ReceiptUrl { get; set; }

    }

    public class Attendance
    {
        [Key, MaxLength(50)]
        public string AttendanceId { get; set; }

        //FK to ClassSection -- 1 attendance record belong to 1 class section
        public string SectionId { get; set; }
        public ClassSection ClassSection { get; set; }

        //FK to Student -- 1 attendance record belong to 1 student
        public string? StudentId { get; set; }
        public Student Student { get; set; }

        public DateOnly Date { get; set; }

        public DateTime RegisteredAt { get; set; } = DateTime.Now;

        [MaxLength(20)]
        public string Status { get; set; }

        public bool IsFlaged { get; set; }

    }

    public class CalendarEvent
    {
        [Key, MaxLength(50)]
        public string EventId { get; set; }

        [MaxLength(100)]
        public string Title { get; set; }

        [MaxLength(200)]
        public string Description { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        [MaxLength(50)]
        public string Type { get; set; }
    }

    //KS
    public class Material
    {
        [Key, MaxLength(50)]
        public string MaterialId { get; set; }

        //FK to Course -- 1 material belong to 1 course
        public string CourseCode { get; set; }
        [ForeignKey("CourseCode")]
        public Course Course { get; set; }

        [MaxLength(100)]
        public string Title { get; set; }

        [MaxLength(200)]
        public string FileUrl { get; set; }

        [MaxLength(20)]
        public string MaterialType { get; set; } //Lecture Notes, Assignment, Reference

        //1 material have many tasks
        public List<CourseTask> Tasks { get; set; } = [];
    }

    public class CourseTask
    {
        [Key, MaxLength(50)]
        public string TaskId { get; set; }

        //FK to ClassSection -- 1 task belong to 1 class section
        public string SectionId { get; set; }
        public ClassSection ClassSection { get; set; }

        [MaxLength(100)]
        public string Title { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        public DateTime DueDate { get; set; }

        //1 task have many materials
        public List<Material> Materials { get; set; } = [];

        //1 task have many submissions
        public List<Submission> Submissions { get; set; } = [];

    }

    public class Submission
    {
        [Key, MaxLength(50)]
        public string SubmissionId { get; set; }

        //FK to Task -- 1 submission belong to 1 task
        public string TaskId { get; set; }
        public CourseTask CourseTask { get; set; } 

        //FK to Student -- 1 submission belong to 1 student
        public string? StudentId { get; set; }
        public Student Student { get; set; }

        [MaxLength(200)]
        public string FileUrl { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.Now;

        [Precision(5, 2)]
        public decimal? Grade { get; set; }
        [MaxLength(500)]
        public string? Feedback { get; set; }
    }

    public class ChatMessage
    {
        [Key, MaxLength(50)]
        public string MessageId { get; set; }

        //FK to User -- 1 message belong to 1 user (sender)
        public string? SenderId { get; set; }
        public User Sender { get; set; }

        //FK to User -- 1 message belong to 1 user (receiver)
        public string? ReceiverId { get; set; }
        public User Receiver { get; set; }

        [MaxLength(1000)]
        public string Content { get; set; }

        public DateTime SentAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;
    }

    public class SupportTicket
    {
        [Key, MaxLength(50)]
        public string TicketId { get; set; }

        //FK to User -- 1 ticket belong to 1 user
        public string? UserId { get; set; }
        public User User { get; set; }

        [MaxLength(200)]
        public string Subject { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        [MaxLength(10)]
        public string Priority { get; set; } //Low, Medium, High
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ResolvedAt { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Open"; //Open, In Progress, Resolved, Closed

        public List<TicketMessage> TicketMessages { get; set; } = [];
    }

    public class TicketMessage
    {

        [Key, MaxLength(50)]
        public string MessageId { get; set; }

        //FK to SupportTicket -- 1 message belong to 1 support ticket
        public string TicketId { get; set; }
        public SupportTicket SupportTicket { get; set; }

        //FK to User -- 1 message belong to 1 user (sender)
        public string? SenderId { get; set; }
        public User Sender { get; set; }

        [MaxLength(1000)]
        public string Content { get; set; }

        public DateTime SentAt { get; set; } = DateTime.Now;

    }
}