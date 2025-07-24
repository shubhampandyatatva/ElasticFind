using System.ComponentModel.DataAnnotations.Schema;

namespace ElasticFind.Repository.Data;

[Table("categories")]
public partial class Category
{
    public int Id { get; set; }
    
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsDeleted { get; set; } = false;

    public string? CreatedBy { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ModifiedAt { get; set; }
}
