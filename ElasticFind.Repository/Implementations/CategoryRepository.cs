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
        var query = _dbcontext.Categories.OrderBy(u => u.Id);
        if (!string.IsNullOrEmpty(pagination.SearchString))
        {
            query = query.Where(u => u.Name.ToLower().Contains(pagination.SearchString.ToLower())).OrderBy(u => u.Id);
        }

        List<CategoryViewModel> categories = query.Skip((pagination.Page - 1) * pagination.PageSize).Take(pagination.PageSize).Select(u => new CategoryViewModel
        {
            Id = u.Id,
            Name = u.Name,
            Description = u.Description,
            CreatedBy = u.CreatedBy,
        }).ToList();

        return categories;
    }

    public async Task<int> GetTotalCategories()
    {
        return await _dbcontext.Categories.CountAsync();
    }

    public async Task<int> GetTotalSearchedCategories(string searchString)
    {
        return await _dbcontext.Categories.Where(u => u.Name.ToLower().Contains(searchString.ToLower()) || u.Description.ToLower().Contains(searchString.ToLower())).CountAsync();
    }

    public async Task AddCategory(Category category)
    {
        try
        {
            await _dbcontext.Categories.AddAsync(category);
            await _dbcontext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("An error occurred while adding the category to the database.", ex);
        }
    }

    public async Task<Category?> GetCategoryByName(string name)
    {
        return await _dbcontext.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
    }

    public async Task UpdateCategory(Category category)
    {
        try
        {
            _dbcontext.Categories.Update(category);
            await _dbcontext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("An error occurred while updating the category.", ex);
        }
    }

    public async Task DeleteCategoryByName(string name)
    {
        try
        {
            Category? category = await _dbcontext.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower()) ?? throw new Exception("Category not found.");

            _dbcontext.Categories.Remove(category);
            await _dbcontext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("An error occurred while deleting the category.", ex);
        }
    }

    public async Task<List<CategoryViewModel>> GetAllCategories()
    {
        return await _dbcontext.Categories
            .Select(c => new CategoryViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                CreatedBy = c.CreatedBy
            })
            .ToListAsync();
    }

    public async Task<string> GetFirstCategory()
    {
        Category category = await _dbcontext.Categories.FirstAsync();
        return category.Name;
    }

    public async Task<Category?> GetCategoryById(int id)
    {
        return await _dbcontext.Categories.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task DeleteCategoryById(int id)
    {
        try
        {
            Category? category = await _dbcontext.Categories.FindAsync(id) ?? throw new Exception("Category not found.");

            _dbcontext.Categories.Remove(category);
            await _dbcontext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("An error occurred while deleting the category by ID.", ex);
        }
    }

    public async Task CreateDefaultCategory()
    {
        bool doesCategoryExist = await _dbcontext.Categories.AnyAsync(c => c.Name.ToLower() == "default");
        if (!doesCategoryExist)
        {
            Category newCategory = new()
            {
                Name = "Default",
                Description = "This is a default category.",
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            await _dbcontext.Categories.AddAsync(newCategory);
            await _dbcontext.SaveChangesAsync();
        }
    }
}
