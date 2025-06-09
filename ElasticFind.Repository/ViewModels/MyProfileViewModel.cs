using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ElasticFind.Repository.ViewModels;

public class MyProfileViewModel
{
    public int? Id { get; set; }
    [Required(ErrorMessage = "First Name is required.")]
    public required string FirstName {get; set;}
    public string? LastName {get; set;}

    [Required(ErrorMessage = "Username is required.")]
    public required string Username {get; set;}

    [Required(ErrorMessage = "Email is required.")]
    [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Please enter a valid email address.")]
    public required string Email {get; set;}

    [DataType(DataType.Upload)]
    public IFormFile? ProfileImage {get; set;}

    public string? ProfileImagePath {get; set;}

    [Required(ErrorMessage = "Phone is required.")]
    [StringLength(10, ErrorMessage = "Phone number must be exactly 10 characters long.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must contain only numbers and should be 10 digits long.")]
    public required string Phone {get; set;}
    public string? Role {get; set;}
}
