using System;
using System.Collections.Generic;

namespace ElasticFind.Repository.Data;

public partial class User
{
    public int Id { get; set; }

    public string FirstName { get; set; } = null!;

    public string? LastName { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? ProfileImage { get; set; }

    public int? RoleId { get; set; }

    public string Password { get; set; } = null!;

    public bool? Isdeleted { get; set; }

    public bool? Isactive { get; set; }

    public virtual Role? Role { get; set; }
}
