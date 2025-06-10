namespace ElasticFind.Repository.ViewModels;

public class UserViewModel
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public string? Profileimage { get; set; }

    public string Firstname { get; set; } = null!;

    public string? Lastname { get; set; }

    public string Username { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public bool IsActive { get; set; }
}
