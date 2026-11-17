using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace Web.Razor.Admin;

// MQ Rewritten: the first line of defence for everything admin. Without a valid admin session:
// - /admin itself shows only the sign-in popup (the page handles that),
// - anything under /admin/ and the old /commands, /colliders and /hubs/colliders pages are a plain 404.
// Every page under /admin also gets no-index, no-cache and no-framing headers.
public class AdminGateMiddleware(RequestDelegate next)
{
    public const string NonceKey = "mqr-admin-nonce";

    private static readonly string[] _legacyAdminPaths = ["/commands", "/colliders", "/hubs/colliders"];

    public Task Invoke(HttpContext context, AdminAuth auth)
    {
        var path = context.Request.Path;

        if (path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase, out var rest))
        {
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            context.Items[NonceKey] = nonce;

            var headers = context.Response.Headers;
            headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
            headers.CacheControl = "no-store";
            headers.Pragma = "no-cache";
            headers.XFrameOptions = "DENY";
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy =
                $"default-src 'none'; style-src 'nonce-{nonce}'; script-src 'nonce-{nonce}'; img-src 'self'; " +
                "font-src 'self'; connect-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";

            var isSignInPage = !rest.HasValue || rest.Value == "/" ||
                rest.Equals("/index", StringComparison.OrdinalIgnoreCase);

            if (!isSignInPage && auth.GetSession(context) == null)
                return NotFound(context);

            return next(context);
        }

        foreach (var legacy in _legacyAdminPaths)
        {
            if (!path.StartsWithSegments(legacy, StringComparison.OrdinalIgnoreCase))
                continue;

            if (auth.GetSession(context) == null)
                return NotFound(context);

            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
            context.Response.Headers.CacheControl = "no-store";
            break;
        }

        return next(context);
    }

    private static Task NotFound(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}

public static class AdminExtensions
{
    public static IServiceCollection AddAdminAuth(this IServiceCollection services) =>
        services.AddSingleton<AdminCredentials>()
            .AddSingleton<AdminLoginLimiter>()
            .AddSingleton<AdminAuth>();

    public static IApplicationBuilder UseAdminGate(this IApplicationBuilder app) =>
        app.UseMiddleware<AdminGateMiddleware>();
}
