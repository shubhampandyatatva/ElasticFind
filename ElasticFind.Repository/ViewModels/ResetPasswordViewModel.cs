using System.ComponentModel.DataAnnotations;

namespace ElasticFind.Repository.ViewModels;

public class ResetPasswordViewModel
{
    public required string Token { get; set; }

    [Required(ErrorMessage = "New Password is required.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$", ErrorMessage = "Password should be at least 8 characters long and contain an uppercase letter, a lowercase letter, a number, and a special character.")]
    public required string NewPassword { get; set; }

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare("NewPassword", ErrorMessage = "New Password and Confirm Password do not match.")]
    public required string ConfirmPassword { get; set; }
}
