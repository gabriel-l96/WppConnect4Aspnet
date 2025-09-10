using Microsoft.EntityFrameworkCore;

namespace WppConnect4Aspnet.Data
{
    public class ApiDbContext :DbContext
    {
        public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
        {
        }
        public DbSet<Models.WhatsappSession> WhatsappSessions { get; set; }
    }
}
