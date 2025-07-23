using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Repository.Interfaces;

public interface ICategoryRepository
{
    List<CategoryViewModel> GetCategoryList(PaginationViewModel pagination);
    Task<int> GetTotalCategories();
    Task<int> GetTotalSearchedCategories(string searchString);

}
