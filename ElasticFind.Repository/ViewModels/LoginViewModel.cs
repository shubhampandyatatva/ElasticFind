using System.ComponentModel.DataAnnotations;

namespace ElasticFind.Repository.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Please enter a valid email address.")]
    public required string Email { get; set; }
    [Required(ErrorMessage = "Password is required.")]
    public required string Password { get; set; }
    public bool RememberMe { get; set; }
}
