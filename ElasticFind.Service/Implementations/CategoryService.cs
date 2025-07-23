using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;

namespace ElasticFind.Service.Implementations;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    public CategoryService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }
    
    public async Task<DisplayCategoriesViewModel> GetCategories(PaginationViewModel pagination)
    {
        List<CategoryViewModel> categoryList = _categoryRepository.GetCategoryList(pagination);
        int totalRecords = pagination.SearchString == null ? await _categoryRepository.GetTotalCategories() : await _categoryRepository.GetTotalSearchedCategories(pagination.SearchString);
        pagination.TotalRecords = totalRecords;

        DisplayCategoriesViewModel viewModel = new()
        {
            PaginationViewModel = pagination,
            CategoryList = categoryList
        };

        return viewModel;
    }
}
