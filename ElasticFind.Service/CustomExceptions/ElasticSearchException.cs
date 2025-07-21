namespace ElasticFind.Service.Exceptions;

public class ElasticSearchException : Exception
{
    public ElasticSearchException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}
