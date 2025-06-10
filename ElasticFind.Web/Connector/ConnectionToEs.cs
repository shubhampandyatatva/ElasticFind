using Elasticsearch.Net;
using Nest;

namespace ElasticFind.Web.Connector;

public class ConnectionToEs
{
    public ElasticClient EsClient()
    {
        Uri[] nodes = new Uri[]
        {
                new("http://localhost:9200/")
        };

        var connectionPool = new StaticConnectionPool(nodes);
        var connectionSettings = new ConnectionSettings(connectionPool).DisableDirectStreaming();
        var elasticClient = new ElasticClient(connectionSettings);

        return elasticClient;
    }
}
