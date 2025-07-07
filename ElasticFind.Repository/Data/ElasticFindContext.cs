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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.SetNull); 
    }
}
