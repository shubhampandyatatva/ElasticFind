using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ElasticFind.Repository.Data;
using ElasticFind.Service.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ElasticFind.Service.Implementations;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    // private readonly IRolePermissionsService _rolePermissionsService;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
        // _rolePermissionsService = rolePermissionsService;
    }
    public string GenerateJwtToken(User user)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.RoleId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
        };
        // Console.WriteLine(user.RememberMe);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string? GetClaimValue(string jwtToken, string claimType)
    {
        ClaimsPrincipal? claimsPrincipal = ValidateToken(jwtToken);  // Verify if the token is valid
        if (claimsPrincipal == null)
        {
            Console.WriteLine("Error: JWT Token is null!");
            return null;
        }

        Claim? claim = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == claimType);

        return claim?.Value;
    }
    
    public static ClaimsPrincipal? ValidateToken(string token)
    {
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken? jsonToken = handler.ReadToken(token) as JwtSecurityToken;

        // If token is valid, then return ClaimsPrincipal
        if (jsonToken != null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(jsonToken.Claims));
        }

        return null;
    }
}
