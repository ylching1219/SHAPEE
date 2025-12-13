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
        public Course? EditCourse { get; set; }

        public string? CourseId { get; set; }
        public string? CourseName { get; set; }
        public int? CreditHours { get; set; } 

    }

    public class GroupManageVM
    {
        public List<TutorialGroup>? Groups { get; set; } = [];
        public TutorialGroup? EditGroup { get; set; }
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

    // JS
    public class RoomManageVM
    {
        public List<Room> Rooms { get; set; } = [];
        public Room? EditRoom { get; set; }
    }
}