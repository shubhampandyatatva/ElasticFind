using ElasticFind.Repository.Data;
using ElasticFind.Repository.Interfaces;

namespace ElasticFind.Repository.Implementations;

public class ProfileRepository : IProfileRepository
{
    private readonly ElasticFindContext _dbcontext;
    public ProfileRepository(ElasticFindContext dbcontext)
    {
        _dbcontext = dbcontext;
    }
    
}
