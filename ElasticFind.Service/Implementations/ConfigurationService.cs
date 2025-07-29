using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Nest;
using Serilog;

namespace ElasticFind.Service.Implementations;

public class ConfigurationService : IConfigurationService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUserRepository _userRepository;
    public ConfigurationService(ICategoryRepository categoryRepository, IUserRepository userRepository)
    {
        _categoryRepository = categoryRepository;
        _userRepository = userRepository;
    }

    public async Task ConfigureElasticFind(IElasticClient client, string indexName)
    {
        try
        {
            await ValidateAndInitializeElasticsearchAsync(client, indexName);

            await _categoryRepository.CreateDefaultCategory();

            await _userRepository.CreateDefaultAdminUser();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.ElasticsearchError = ex.Message;
            Log.Logger.Error(ex.Message);
        }
    }

    async Task ValidateAndInitializeElasticsearchAsync(IElasticClient client, string indexName)
    {
        var pingResponse = await client.PingAsync();
        if (!pingResponse.IsValid)
            throw new Exception("Elasticsearch service is not reachable! Please start the elasticsearch service.");

        var health = await client.Cluster.HealthAsync();
        if (health.Status.ToString().Equals("red", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Elasticsearch service is not ready. Try restarting the service.");

        var indexExists = await client.Indices.ExistsAsync(indexName);
        if (!indexExists.Exists)
        {
            var createIndexResponse = await client.Indices.CreateAsync(indexName, c => c
                .Map<DocumentViewModel>(m => m.AutoMap())
            );

            if (!createIndexResponse.IsValid)
                throw new Exception("There was some issue initializing Elasticsearch properly! Try restarting the elasticsearch service or the application.");
        }

        var info = await client.RootNodeInfoAsync();
        var version = info.Version.Number;

        if (string.Compare(version, "8.0.0") < 0)
        {
            // On versions older than 8, we can optionally check plugin
            var pluginResponse = await client.Cat.PluginsAsync();
            bool hasAttachmentPlugin = pluginResponse.Records.Any(r => r.Component.Contains("ingest-attachment"));

            if (!hasAttachmentPlugin)
                throw new Exception("Ingest-Attachment plugin is required for processing documents. Please install it to use ElasticFind.");
        }

        var pipelineResponse = await client.Ingest.GetPipelineAsync(p => p.Id("attachment"));
        if (!pipelineResponse.IsValid || !pipelineResponse.Pipelines.ContainsKey("attachment"))
        {
            var putPipelineResponse = await client.Ingest.PutPipelineAsync("attachment", p => p
                .Description("Extract attachment information")
                .Processors(pr => pr
                    .Attachment<DocumentViewModel>(a => a
                        .Field(f => f.Data)
                        .TargetField(f => f.Attachment)
                        .IndexedCharacters(-1)
                    )
                )
            );

            if (!putPipelineResponse.IsValid)
                throw new Exception("There was some issue initializing Elasticsearch properly! Try restarting the application.");
        }
    }
}
