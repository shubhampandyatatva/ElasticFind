using ElasticFind.Repository.Data;
using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Repository.Interfaces;

public interface ICategoryRepository
{
    Task AddCategory(Category category);
    Task CreateDefaultCategory();

    Task DeleteCategoryById(int id);

    Task DeleteCategoryByName(string name);
    Task<List<CategoryViewModel>> GetAllCategories();
    Task<Category?> GetCategoryById(int id);

    Task<Category?> GetCategoryByName(string name);
    List<CategoryViewModel> GetCategoryList(PaginationViewModel pagination);
    Task<string> GetFirstCategory();

    Task<int> GetTotalCategories();
    Task<int> GetTotalSearchedCategories(string searchString);
    Task UpdateCategory(Category category);
}
