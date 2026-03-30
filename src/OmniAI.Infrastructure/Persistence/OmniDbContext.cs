// Adicione estes DbSets ao OmniDbContext existente:
//
//   public DbSet<AppUser>      AppUsers      => Set<AppUser>();
//   public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
//
// E adicione ao IOmniDbContext (Common/Interfaces.cs):
//   DbSet<AppUser> AppUsers { get; }
//   DbSet<RefreshToken> RefreshTokens { get; }
//
// Este arquivo mostra o OmniDbContext completo para referência:

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OmniAI.Application;
using OmniAI.Domain.Entities;
using System.Reflection.Emit;
using System.Text.Json;

namespace OmniAI.Infrastructure.Persistence;

public class OmniDbContext(DbContextOptions<OmniDbContext> options) : DbContext(options), IOmniDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<WhatsAppNumber> WhatsAppNumbers => Set<WhatsAppNumber>();
    public DbSet<BotConfig> BotConfigs => Set<BotConfig>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<KnowledgeSource> KnowledgeSources => Set<KnowledgeSource>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<HandoffTrigger> HandoffTriggers => Set<HandoffTrigger>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    // ── Auth ──────────────────────────────────────────────────────
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder mb)
    {
        mb.ApplyConfigurationsFromAssembly(typeof(OmniDbContext).Assembly);
        mb.Entity<KnowledgeSource>().HasQueryFilter(x => !x.IsDeleted);
        var converter = new ValueConverter<Dictionary<string, string>, string>(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
        v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null));

        mb.Entity<Contact>()
            .Property(e => e.Tags)
            .HasConversion(converter);
        mb.Entity<AppUser>().HasQueryFilter(x => !x.IsDeleted);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }
}