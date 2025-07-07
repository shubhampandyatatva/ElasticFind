using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ElasticFind.Repository.Data;

[Table("roles")]
public partial class Role
{
    public int Id { get; set; }

    public string RoleName { get; set; } = null!;

    public virtual ICollection<User> Users { get; } = new List<User>();
}
