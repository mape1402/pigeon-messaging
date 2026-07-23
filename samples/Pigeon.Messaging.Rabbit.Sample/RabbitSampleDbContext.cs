namespace Pigeon.Messaging.Rabbit.Sample
{
    using Microsoft.EntityFrameworkCore;

    internal sealed class RabbitSampleDbContext : DbContext
    {
        public RabbitSampleDbContext(DbContextOptions<RabbitSampleDbContext> options)
            : base(options)
        {
        }
    }
}
