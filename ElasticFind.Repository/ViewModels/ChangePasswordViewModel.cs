using System.ComponentModel.DataAnnotations;

namespace ElasticFind.Repository.ViewModels;

public class ChangePasswordViewModel
{
    public required string Email {get; set;}

    [Required(ErrorMessage = "Current password is required.")]
    public required string CurrentPassword {get; set;}

    [Required(ErrorMessage = "New password is required.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$", ErrorMessage = "Password should be at least 8 characters long and contain an uppercase letter, a lowercase letter, a number, and a special character.")]
    public required string NewPassword { get; set; }

    [Required(ErrorMessage = "Please confirm your new password.")]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    public required string ConfirmNewPassword {get; set;}
}
