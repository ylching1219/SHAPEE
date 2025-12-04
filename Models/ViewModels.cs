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
}