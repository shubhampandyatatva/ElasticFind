using ElasticFind.Repository.Data;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ElasticFind.Repository.Implementations;

public class CategoryRepository : ICategoryRepository
{
    private readonly ElasticFindContext _dbcontext;
    public CategoryRepository(ElasticFindContext dbcontext)
    {
        _dbcontext = dbcontext;
    }

    public List<CategoryViewModel> GetCategoryList(PaginationViewModel pagination)
    {
        var query = _dbcontext.Categories.Where(u => u.IsDeleted != true).OrderBy(u => u.Id);
        if (!string.IsNullOrEmpty(pagination.SearchString))
        {
            query = query.Where(u => u.Name.ToLower().Contains(pagination.SearchString.ToLower()) || u.Description.ToLower().Contains(pagination.SearchString.ToLower())).OrderBy(u => u.Id);
        }

        query = pagination.SortOrder == "Asc" ? query.OrderBy(u => u.Name) : query.OrderByDescending(u => u.Name);

        List<CategoryViewModel> categories = query.Skip((pagination.Page - 1) * pagination.PageSize).Take(pagination.PageSize).Select(u => new CategoryViewModel
        {
            Id = u.Id,
            Name = u.Name,
            Description = u.Description,
            Status = u.IsDeleted == true ? "Active" : "Inactive",
        }).ToList();

        return categories;
    }

    public async Task<int> GetTotalCategories()
    {
        return await _dbcontext.Categories.Where(u => u.IsDeleted != true).CountAsync();
    }

    public async Task<int> GetTotalSearchedCategories(string searchString)
    {
        return await _dbcontext.Categories.Where(u => u.IsDeleted != true && (u.Name.ToLower().Contains(searchString.ToLower()) || u.Description.ToLower().Contains(searchString.ToLower()))).CountAsync();
    }
}
