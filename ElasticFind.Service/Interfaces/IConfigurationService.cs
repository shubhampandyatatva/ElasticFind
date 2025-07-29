
using Nest;

namespace ElasticFind.Service.Interfaces;

public interface IConfigurationService
{
    Task ConfigureElasticFind(IElasticClient client, string indexName);
}
