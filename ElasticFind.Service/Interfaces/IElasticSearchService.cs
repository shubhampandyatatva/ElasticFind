using ElasticFind.Repository.Data;
using ElasticFind.Repository.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nest;

namespace ElasticFind.Service.Interfaces;

public interface IElasticSearchService
{
    Task<bool> CreateDocumentIndexAsync(string indexName);
    Task<bool> DeleteAsync(string id, string category);
    Task<SearchResultsViewModel> SearchDocumentsAsync(string selectedCategory, string? sortBy = null, int currentPage = 1, int pageSize = 5, string? esBoolQuery = null);
    Task<DisplayFilesViewModel> GetFilesAsync(PaginationViewModel paginationViewModel, string indexName);
    Task<List<string>> GetAllFileIdsAsync();
    Task<JsonResult> DeleteMultipleFilesAsync(List<string> ids, string category);
    Task<List<string>> GetAllFileIdsByIndexAsync(string index);
    Task UploadDocumentsAsync(List<IFormFile> files, string uploadCategory, User user);
}
