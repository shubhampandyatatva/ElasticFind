// using DocumentFormat.OpenXml.Drawing.Diagrams;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Repository.Data;
using ElasticFind.Service.Interfaces;
using Nest;
using Serilog;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Elasticsearch.Net;
using ElasticFind.Service.Exceptions;

namespace ElasticFind.Service.Implementations;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IElasticClient _elasticClient;
    private readonly IUserService _userService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public CategoryService(ICategoryRepository categoryRepository, IElasticClient elasticClient, IUserService userService, IHttpContextAccessor httpContextAccessor)
    {
        _categoryRepository = categoryRepository;
        _elasticClient = elasticClient;
        _userService = userService;
        _httpContextAccessor = httpContextAccessor;
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

    public async Task<JsonResponse> AddCategory(string name, string? description)
    {
        try
        {
            Category? existingCategory = await _categoryRepository.GetCategoryByName(name);
            if (existingCategory != null)
            {
                return new JsonResponse { Success = false, Message = "This category already exists!" };
            }

            string indexName = name.ToLowerInvariant();
            var existsResponse = await _elasticClient.Indices.ExistsAsync(indexName);
            if (existsResponse.ApiCall.HttpStatusCode != 404)
            {
                return new JsonResponse { Success = false, Message = "This category already exists!" };
            }

            var createIndexResponse = await _elasticClient.Indices.CreateAsync(indexName, c => c
                .Map<DocumentViewModel>(m => m
                    .AutoMap()
                )
            );

            if (!createIndexResponse.IsValid)
            {
                return new JsonResponse { Success = false, Message = "There was an error creating the category! Please try again." };
            }

            string? jwtToken = _httpContextAccessor.HttpContext?.Request.Cookies["JwtToken"];

            Category category = new()
            {
                Name = name,
                Description = description,
                ModifiedAt = DateTime.UtcNow,
                CreatedBy = _userService.GetClaimValue(jwtToken, ClaimTypes.Name),
            };

            await _categoryRepository.AddCategoryToDb(category);

            return new JsonResponse { Success = true, Message = "Category created successfully!" };
        }
        catch (Exception)
        {
            return new JsonResponse { Success = false, Message = "There was an error creating the category! Please try again." };
        }
    }

    public async Task<JsonResponse> DeleteCategory(string name)
    {
        try
        {
            var response = await _elasticClient.Indices.DeleteAsync(name);
            Console.WriteLine(response.DebugInformation);
            if (response.IsValid)
            {
                await _categoryRepository.DeleteCategoryByName(name);

                return new JsonResponse { Success = true, Message = "Category deleted successfully!" };
            }
            else
            {
                return new JsonResponse { Success = false, Message = "There was an error deleting the category! Please try again." };
            }
        }
        catch (Exception)
        {
            return new JsonResponse { Success = false, Message = "There was an error deleting the category! Please try again." };
        }
    }

    public async Task<Category?> GetCategoryByName(string name)
    {
        return await _categoryRepository.GetCategoryByName(name);
    }

    public async Task<JsonResponse> EditCategory(string name, string oldCategoryName, string? description)
    {
        try
        {
            if (name.ToLowerInvariant() == oldCategoryName.ToLowerInvariant())
            {
                var deleteIndexResponse = await _elasticClient.Indices.DeleteAsync(name.ToLowerInvariant());
                if (!deleteIndexResponse.IsValid)
                {
                    return new JsonResponse { Success = false, Message = "There was an error deleting the existing category index! Please try again." };
                }

                await _categoryRepository.DeleteCategoryByName(name);

                var createIndexResponse = await _elasticClient.Indices.CreateAsync(name.ToLowerInvariant(), c => c
                    .Map<DocumentViewModel>(m => m
                        .AutoMap()
                    )
                );

                if (!createIndexResponse.IsValid)
                {
                    return new JsonResponse { Success = false, Message = "There was an error creating the category! Please try again." };
                }

                string? jwtToken = _httpContextAccessor.HttpContext?.Request.Cookies["JwtToken"];

                Category category = new()
                {
                    Name = name,
                    Description = description,
                    ModifiedAt = DateTime.UtcNow,
                    CreatedBy = _userService.GetClaimValue(jwtToken, ClaimTypes.Name),
                };

                await _categoryRepository.AddCategoryToDb(category);

                return new JsonResponse { Success = true, Message = "Category created successfully!" };
            }
            else
            {
                Category? existingCategory = await _categoryRepository.GetCategoryByName(name);
                if (existingCategory != null)
                {
                    return new JsonResponse { Success = false, Message = "Category by this name already exists!" };
                }

                string newIndexName = name.ToLowerInvariant();
                var existsResponse = await _elasticClient.Indices.ExistsAsync(newIndexName);
                if (existsResponse.ApiCall.HttpStatusCode != 404)   // Index exists
                {
                    return new JsonResponse { Success = false, Message = "Category by this name already exists!" };
                }

                var deleteIndexResponse = await _elasticClient.Indices.DeleteAsync(oldCategoryName.ToLowerInvariant());
                if (!deleteIndexResponse.IsValid)
                {
                    return new JsonResponse { Success = false, Message = "There was an error deleting the existing category index! Please try again." };
                }

                await _categoryRepository.DeleteCategoryByName(oldCategoryName);

                var createIndexResponse = await _elasticClient.Indices.CreateAsync(newIndexName, c => c
                    .Map<DocumentViewModel>(m => m
                        .AutoMap()
                    )
                );

                if (!createIndexResponse.IsValid)
                {
                    return new JsonResponse { Success = false, Message = "There was an error creating the category! Please try again." };
                }

                string? jwtToken = _httpContextAccessor.HttpContext?.Request.Cookies["JwtToken"];

                Category category = new()
                {
                    Name = name,
                    Description = description,
                    ModifiedAt = DateTime.UtcNow,
                    CreatedBy = _userService.GetClaimValue(jwtToken, ClaimTypes.Name),
                };

                await _categoryRepository.AddCategoryToDb(category);

                return new JsonResponse { Success = true, Message = "Category created successfully!" };
            }
        }
        catch (Exception)
        {
            return new JsonResponse { Success = false, Message = "There was an error creating the category! Please try again." };
        }
    }

}
