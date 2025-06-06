using System.ComponentModel.DataAnnotations;

namespace ElasticFind.Repository.ViewModels;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Please enter a valid email address.")]
    public required string Email { get; set; } 
}
