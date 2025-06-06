using System.ComponentModel.DataAnnotations;

namespace ElasticFind.Repository.ViewModels;

public class ResetPasswordViewModel
{
    public required string Token { get; set; }

    [Required(ErrorMessage = "New Password is required.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$", ErrorMessage = "Password should be at least 8 characters long and contain an uppercase letter, a lowercase letter, a number, and a special character.")]
    public required string NewPassword { get; set; }

    [Required(ErrorMessage = "Confirm Password is required.")]
    [Compare("NewPassword", ErrorMessage = "New Password and Cofirm Password do not match.")]
    public required string ConfirmPassword { get; set; }
}
