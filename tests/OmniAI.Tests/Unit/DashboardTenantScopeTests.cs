using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OmniAI.API.Controllers;
using OmniAI.Application.Contracts.Requests;
using OmniAI.Domain.Entities;
using OmniAI.Infrastructure.Persistence;
using Xunit;

namespace OmniAI.Tests.Unit;

public sealed class DashboardTenantScopeTests
{
    [Fact]
    public async Task ListsNumbersForAuthenticatedTenantRegardlessOfQueryTenant()
    {
        await using var db = CreateDb();
        var own = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = own, Slug = "own", Name = "Own" });
        await db.SaveChangesAsync();
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator.Setup(m => m.Send(new GetNumbersQuery(own), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OmniAI.Application.Contracts.DTOs.WhatsAppNumberDto>());
        var controller = new WhatsAppNumbersController(mediator.Object, db) { ControllerContext = ContextFor(own) };

        var result = await controller.GetAll(Guid.NewGuid());

        Assert.IsType<OkObjectResult>(result.Result);
        mediator.VerifyAll();
    }

    [Fact]
    public async Task ConversationLookupWithoutTenantCannotReachHandler()
    {
        await using var db = CreateDb();
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var controller = new ConversationsController(mediator.Object, db) { ControllerContext = ContextFor(null) };

        var result = await controller.GetMessages(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
        mediator.VerifyNoOtherCalls();
    }

    private static OmniDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<OmniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ControllerContext ContextFor(Guid? tenantId)
    {
        Claim[] claims = tenantId is { } id ? [new Claim("tenantId", id.ToString())] : [];
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
    }
}
