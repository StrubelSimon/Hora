using System.Net;
using System.Text;
using Hora.Core.Jira;

namespace Hora.Core.Tests.Jira;

public class BasicAuthHandlerTests
{
    [Fact]
    public async Task Adds_basic_authorization_and_json_accept_header()
    {
        var fake = new FakeHttpMessageHandler().EnqueueJson(HttpStatusCode.OK, "{}");
        var client = new HttpClient(new BasicAuthHandler("a@b.de", "tok") { InnerHandler = fake });

        await client.GetAsync("https://example.atlassian.net/rest/api/3/myself");

        var request = Assert.Single(fake.Requests);
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("a@b.de:tok")), request.Headers.Authorization.Parameter);
        Assert.Contains(request.Headers.Accept, header => header.MediaType == "application/json");
    }
}
