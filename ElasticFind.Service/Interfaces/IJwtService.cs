using ElasticFind.Repository.Data;

namespace ElasticFind.Service.Interfaces;

public interface IJwtService
{
    string GenerateJwtToken(User user);
    string GenerateJwtTokenForOnlyOffice(object payload);
    string? GetClaimValue(string jwtToken, string claimType);
}
