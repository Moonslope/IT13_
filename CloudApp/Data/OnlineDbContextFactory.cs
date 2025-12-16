using Microsoft.EntityFrameworkCore;
using HestiaLink.Data;

namespace CloudApp.Data
{
    /// <summary>
    /// Factory for creating online database contexts
    /// </summary>
    public class OnlineDbContextFactory : IDbContextFactory<HestiaLinkContext>
    {
        private readonly DbContextOptions<HestiaLinkContext> _options;

        public OnlineDbContextFactory(DbContextOptions<HestiaLinkContext> options)
        {
            _options = options;
        }

        public HestiaLinkContext CreateDbContext()
        {
            return new HestiaLinkContext(_options);
        }
    }
}

