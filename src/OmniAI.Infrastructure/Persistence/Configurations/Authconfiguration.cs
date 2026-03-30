using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OmniAI.Domain.Entities;

namespace OmniAI.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        b.Property(x => x.Role).HasMaxLength(20).IsRequired();

        b.HasIndex(x => x.Email).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();

        b.HasOne(x => x.Tenant)
         .WithMany()
         .HasForeignKey(x => x.TenantId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Token).HasMaxLength(500).IsRequired();
        b.HasIndex(x => x.Token).IsUnique();

        b.HasOne(x => x.User)
         .WithMany()
         .HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}