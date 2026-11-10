using System.Net;
using Hora.Core.Http;

namespace Hora.Core.Tests.Http;

public class ApiKeyAuthHandlerTests
{
    [Fact]
    public async Task SetsApiKeyHeaderAndJsonAccept()
    {
        var inner = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, "{}");
        var handler = new ApiKeyAuthHandler("secret-key") { InnerHandler = inner };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.everhour.com/") };

        await http.GetAsync("users/me");

        var request = Assert.Single(inner.Requests);
        Assert.Equal("secret-key", Assert.Single(request.Headers.GetValues("X-Api-Key")));
        Assert.Null(request.Headers.Authorization);
        Assert.Equal("application/json", Assert.Single(request.Headers.Accept).MediaType);
    }

    [Fact]
    public async Task ReplacesAnyApiKeyHeaderAlreadyPresent()
    {
        var inner = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, "{}");
        var handler = new ApiKeyAuthHandler("right") { InnerHandler = inner };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.everhour.com/") };
        http.DefaultRequestHeaders.Add("X-Api-Key", "wrong");

        await http.GetAsync("users/me");

        Assert.Equal("right", Assert.Single(Assert.Single(inner.Requests).Headers.GetValues("X-Api-Key")));
    }
}
