using System.Net;
using OmniAI.API.WhatsApp;
using Xunit;

namespace OmniAI.Tests.Unit;

public sealed class MetaSignupClientTests
{
    [Theory]
    [InlineData("{\"data\":[{\"id\":\"456\"}]}", MetaSignupResult.Verified)]
    [InlineData("{\"data\":[{\"id\":\"999\"}]}", MetaSignupResult.PhoneMismatch)]
    [InlineData("{\"data\":[null,{\"id\":123}]}", MetaSignupResult.PhoneMismatch)]
    [InlineData("[]", MetaSignupResult.UpstreamFailure)]
    [InlineData("invalid JSON", MetaSignupResult.UpstreamFailure)]
    public async Task ValidatesOnlyTheRequestedNumber(string phones, MetaSignupResult expected)
    {
        using var handler = new MetaHandler(HttpStatusCode.OK, phones);
        using var http = new HttpClient(handler);
        var result = await new MetaSignupClient(http).ValidateAsync("1", "secret", "code", "123", "456", default);

        Assert.Equal(expected, result);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, MetaSignupResult.CodeRejected)]
    [InlineData(HttpStatusCode.TooManyRequests, MetaSignupResult.UpstreamFailure)]
    [InlineData(HttpStatusCode.ServiceUnavailable, MetaSignupResult.UpstreamFailure)]
    public async Task StopsWhenTokenExchangeFails(HttpStatusCode status, MetaSignupResult expected)
    {
        using var handler = new MetaHandler(status, "");
        using var http = new HttpClient(handler);
        var result = await new MetaSignupClient(http).ValidateAsync("1", "secret", "code", "123", "456", default);

        Assert.Equal(expected, result);
        Assert.Equal(1, handler.Calls);
    }

    private sealed class MetaHandler(HttpStatusCode exchangeStatus, string phones) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("graph.facebook.com", request.RequestUri!.Host);
            if (Calls == 1)
            {
                Assert.EndsWith("/oauth/access_token", request.RequestUri.AbsolutePath);
                Assert.Null(request.Headers.Authorization);
                return Task.FromResult(new HttpResponseMessage(exchangeStatus)
                {
                    Content = new StringContent("{\"access_token\":\"fake-token\"}")
                });
            }

            Assert.EndsWith("/123/phone_numbers", request.RequestUri.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("fake-token", request.Headers.Authorization?.Parameter);
            Assert.DoesNotContain("fake-token", request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(phones) });
        }
    }
}
