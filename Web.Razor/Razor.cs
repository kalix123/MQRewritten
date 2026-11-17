using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server.Base.Core.Abstractions;
using Server.Web.Abstractions;
using Web.Razor.Services;
using Microsoft.AspNetCore.Builder;
using Web.Razor.Hubs;
using Server.Colliders.Abstractions;
using Web.Razor.Admin;

namespace Web.Razor;

public class Razor(ILogger<Razor> logger) : WebModule(logger)
{
    public override void AddServices(IServiceCollection services, Module[] modules)
    {
        services.AddSingleton<PagesService>()
            .AddSingleton<IColliderUpdatePublisher, SignalRColliderPublisher>()
            .AddAdminAuth();

        services.AddSignalR();
        services.AddRazorPages();
    }

    public override void PostWebBuild(WebApplication app)
    {
        base.PostWebBuild(app);

        // MQ Rewritten: /admin sign-in, and the old admin pages (/commands, /colliders) only for a signed-in admin.
        // The site's login cookies can't prove who someone is, so they never count. See Admin/AdminGateMiddleware.cs.
        app.UseAdminGate();

        app.MapRazorPages();
        app.MapHub<ColliderHub>("/hubs/colliders");
    }
}
