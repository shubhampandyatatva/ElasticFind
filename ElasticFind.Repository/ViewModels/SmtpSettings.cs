namespace ElasticFind.Repository.ViewModels;

public class SmtpSettings
{
    public required string Server { get; set; }
    public int Port { get; set; }
    public required string SenderEmail { get; set; }
    public required string Password { get; set; }
}
