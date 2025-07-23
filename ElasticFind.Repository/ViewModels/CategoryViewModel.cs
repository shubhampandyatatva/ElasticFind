namespace ElasticFind.Repository.ViewModels;

public class CategoryViewModel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
}
