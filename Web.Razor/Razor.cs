using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server.Base.Core.Abstractions;
using Server.Web.Abstractions;
using Web.Razor.Services;
using Microsoft.AspNetCore.Builder;
using Web.Razor.Hubs;
using Server.Colliders.Abstractions;

namespace Web.Razor;

public class Razor(ILogger<Razor> logger) : WebModule(logger)
{
    public override void AddServices(IServiceCollection services, Module[] modules)
    {
        services.AddSingleton<PagesService>()
            .AddSingleton<IColliderUpdatePublisher, SignalRColliderPublisher>();

        services.AddSignalR();
        services.AddRazorPages();
    }

    public override void PostWebBuild(WebApplication app)
    {
        base.PostWebBuild(app);

        // MQ Rewritten: keep the admin pages private. The site's login cookies can't prove who someone is, so block them for everyone.
        app.Use((context, next) =>
        {
            var path = context.Request.Path;

            if (!path.StartsWithSegments("/commands") && !path.StartsWithSegments("/colliders") && !path.StartsWithSegments("/hubs/colliders"))
                return next();

            context.Response.StatusCode = 404;
            return System.Threading.Tasks.Task.CompletedTask;
        });

        app.MapRazorPages();
        app.MapHub<ColliderHub>("/hubs/colliders");
    }
}
