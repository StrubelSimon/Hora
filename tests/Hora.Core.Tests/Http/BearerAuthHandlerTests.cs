using System.Net;
using Hora.Core.Http;

namespace Hora.Core.Tests.Http;

public class BearerAuthHandlerTests
{
    [Fact]
    public async Task SetsBearerHeaderAndJsonAccept()
    {
        var inner = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, "{}");
        var handler = new BearerAuthHandler("secret-token") { InnerHandler = inner };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://app.asana.com/api/1.0/") };

        await http.GetAsync("users/me");

        var request = Assert.Single(inner.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("secret-token", request.Headers.Authorization.Parameter);
        Assert.Equal("application/json", Assert.Single(request.Headers.Accept).MediaType);
    }

    [Fact]
    public async Task ReplacesAnyAcceptHeaderAlreadyPresent()
    {
        var inner = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, "{}");
        var handler = new BearerAuthHandler("t") { InnerHandler = inner };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://app.asana.com/api/1.0/") };
        http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html"));

        await http.GetAsync("users/me");

        Assert.Equal("application/json", Assert.Single(Assert.Single(inner.Requests).Headers.Accept).MediaType);
    }
}
