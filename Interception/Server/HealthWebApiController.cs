using System.Text;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;

namespace OpenShock.Desktop.Modules.Interception.Server;

public sealed class HealthWebApiController : WebApiController
{
    [Route(HttpVerbs.Get, "/Check")]
    public async Task Check()
    {
        HttpContext.Response.StatusCode = 200;
        await HttpContext.SendStringAsync("OK", "text/plain", Encoding.UTF8);
    }

    [Route(HttpVerbs.Get, "/Server")]
    public Task Server()
    {
        HttpContext.Response.StatusCode = 204;
        return Task.CompletedTask;
    }
}
