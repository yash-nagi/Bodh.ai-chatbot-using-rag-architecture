using Microsoft.Extensions.Configuration;

public interface IConfigurationService
{
    string GetConnectionString();
}

public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;

    public ConfigurationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetConnectionString()
    {
        var connectionString = _configuration.GetSection("DBConnectionString:ConnectionString").Value;

        // Developer code: keep this method fail-fast to prevent the app from running with an empty or invalid DB configuration.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured.");
        }

        return connectionString;
    }
}