using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OmniAI.API.Controllers;
using OmniAI.API;
using OmniAI.Domain.Entities;
using OmniAI.Infrastructure.Persistence;
using Xunit;

namespace OmniAI.Tests.Unit;

public sealed class PublicReadControllerTests
{
    private static readonly string Secret = new('a', 43);

    [Theory]
    [InlineData(true, true, false, "contacts:read", true)]
    [InlineData(false, true, false, "contacts:read", false)]
    [InlineData(true, false, false, "contacts:read", false)]
    [InlineData(true, true, true, "contacts:read", false)]
    [InlineData(true, true, false, "tenant:read", false)]
    public async Task ContactsRequireAnActiveKeyAndFilterByTenant(
        bool enabled, bool keyEnabled, bool expired, string scope, bool allowed)
    {
        await using var db = new OmniDbContext(new DbContextOptionsBuilder<OmniDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var own = new Tenant { Id = Guid.NewGuid(), Name = "Conta A", Slug = "a" };
        var other = new Tenant { Id = Guid.NewGuid(), Name = "Conta B", Slug = "b" };

        db.Tenants.AddRange(own, other);
        db.Contacts.AddRange(
            new Contact { Id = Guid.NewGuid(), TenantId = own.Id, Name = "Contato A", PhoneNumber = "100" },
            new Contact { Id = Guid.NewGuid(), TenantId = other.Id, Name = "Contato B", PhoneNumber = "200" });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PublicApi:Enabled"] = enabled.ToString(),
            ["PublicApi:Keys:test:Enabled"] = keyEnabled.ToString(),
            ["PublicApi:Keys:test:TenantId"] = own.Id.ToString(),
            ["PublicApi:Keys:test:ExpiresAt"] = DateTimeOffset.UtcNow.AddDays(expired ? -1 : 1).ToString("O"),
            ["PublicApi:Keys:test:Scopes"] = scope,
            ["PublicApi:Keys:test:Sha256"] = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(Secret)))
        }).Build();

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "test." + Secret;
        using var quota = new PublicApiQuota();
        var controller = new PublicReadController(db, config, quota) { ControllerContext = new() { HttpContext = context } };

        var result = await controller.GetContacts();
        if (allowed)
        {
            var response = Assert.IsType<OkObjectResult>(result);
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
            Assert.Equal("100", Assert.Single(json.RootElement.EnumerateArray()).GetProperty("PhoneNumber").GetString());
        }
        else
        {
            Assert.Equal(401, Assert.IsType<ObjectResult>(result).StatusCode);
        }
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());

        if (allowed)
        {
            config["PublicApi:Keys:test:RequestsPerMinute"] = "1";
            Assert.IsType<OkObjectResult>(await controller.GetContacts());
            context.Request.Headers["X-Api-Key"] = "TEST." + Secret;
            Assert.Equal(429, Assert.IsType<ObjectResult>(await controller.GetContacts()).StatusCode);
            Assert.True(int.TryParse(context.Response.Headers.RetryAfter.ToString(), out var seconds) && seconds > 0);
        }
    }
}
