using ElasticFind.Repository.Data;
using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Repository.Interfaces;

public interface ICategoryRepository
{
    Task AddCategoryToDb(Category category);
    Task DeleteCategoryByName(string name);

    Task<Category?> GetCategoryByName(string name);
    List<CategoryViewModel> GetCategoryList(PaginationViewModel pagination);
    Task<int> GetTotalCategories();
    Task<int> GetTotalSearchedCategories(string searchString);
    Task UpdateCategory(Category category);
}
