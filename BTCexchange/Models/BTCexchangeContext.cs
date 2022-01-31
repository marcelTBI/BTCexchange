using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace BTCexchange.Models
{
    public partial class BTCexchangeContext : DbContext
    {
        public BTCexchangeContext()
        {
        }

        public BTCexchangeContext(DbContextOptions<BTCexchangeContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Order> Orders { get; set; } = null!;
        public virtual DbSet<User> Users { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("orders");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.AvgPrice).HasColumnName("avg_price");
                entity.Property(e => e.Buying).HasColumnName("buying");
                entity.Property(e => e.FilledQuantity).HasColumnName("filled_quantity");
                entity.Property(e => e.RemainQuantity).HasColumnName("remain_quantity");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.LimitPrice).HasColumnName("limit_price");
                entity.Property(e => e.NotifyUrl).HasColumnName("notify_url");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.BtcBalance).HasColumnName("btc_balance");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Token).HasColumnName("token");
                entity.Property(e => e.UsdBalance).HasColumnName("usd_balance");
            });

            modelBuilder.HasSequence("id_seq");

            OnModelCreatingPartial(modelBuilder);
        }

        public async Task<User?> FindUserByToken(string token)
        {
            return await Users.Where(e => e.Token == token).FirstOrDefaultAsync();            
        }

        public static string? GetToken(IHeaderDictionary headers)   // TODO move somewhere more appropriate?
        {
            if (headers.TryGetValue("token", out StringValues token) && token.Count == 1)
            {
                return token.ToString();
            }
            else return null;
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
