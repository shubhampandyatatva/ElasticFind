using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ElasticFind.Repository.Data;

public partial class ElasticFindContext : DbContext
{
    public ElasticFindContext(DbContextOptions<ElasticFindContext> options) : base(options)
    {
    }

    public virtual DbSet<File> Files { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<User> Users { get; set; }
}
