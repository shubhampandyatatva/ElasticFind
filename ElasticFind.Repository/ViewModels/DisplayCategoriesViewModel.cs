namespace ElasticFind.Repository.ViewModels;

public class DisplayCategoriesViewModel
{
    public PaginationViewModel? PaginationViewModel { get; set; }
    public List<CategoryViewModel> CategoryList { get; set; } = new List<CategoryViewModel>();
}
