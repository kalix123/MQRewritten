using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Razor.Admin;

// MQ Rewritten: base class for every admin page after the sign-in one. It re-checks the session and
// access level on the server for every handler (GET and POST), so a page can't forget to.
// Razor Pages already validates the antiforgery token on every POST; keep forms on the form tag helper
// (or send the RequestVerificationToken header from scripts) and never add [IgnoreAntiforgeryToken].
public abstract class AdminPageModel : PageModel
{
    public AdminSession AdminSession { get; private set; }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var auth = HttpContext.RequestServices.GetRequiredService<AdminAuth>();
        AdminSession = auth.GetSession(HttpContext);

        if (AdminSession == null)
        {
            context.Result = NotFound();
            return;
        }

        await next();
    }
}
