using System.ComponentModel.DataAnnotations;

namespace ElasticFind.Repository.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "First name is required.")]
    public required string FirstName { get; set; }
    public string? LastName { get; set; }

    [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Please enter a valid email address.")]
    public required string Email { get; set; }
    [Required(ErrorMessage = "Password is required.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$", ErrorMessage = "Password should be at least 8 characters long and contain an uppercase letter, a lowercase letter, a number, and a special character.")]
    public required string Password { get; set; }
    [Required(ErrorMessage = "Username is required.")]
    [RegularExpression(@"^[a-zA-Z0-9._-]{3,}$", ErrorMessage = "Username must be at least 3 characters long and can only contain letters, numbers, underscores, and hyphens.")]
    public required string Username { get; set; }
    [Required(ErrorMessage = "Phone number is required.")]
    public required string Phone { get; set; }
}
