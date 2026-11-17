using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web.Razor.Admin;

namespace Web.Razor.Pages.Admin;

// MQ Rewritten: /admin. Shows only the sign-in popup until an owner (or the ADMIN_USERNAME login)
// signs in, then the admin home. Not linked from anywhere and never indexed.
public class AdminIndexModel(AdminAuth auth) : PageModel
{
    public AdminSession AdminSession { get; private set; }
    public string ErrorMessage { get; private set; }

    [BindProperty]
    public string Username { get; set; }

    public IActionResult OnGet()
    {
        AdminSession = auth.GetSession(HttpContext);

        if (AdminSession == null)
            Response.StatusCode = StatusCodes.Status401Unauthorized;

        return Page();
    }

    public async Task<IActionResult> OnPostSignInAsync([FromForm] string password)
    {
        var (result, retryAfter) = await auth.SignInAsync(HttpContext, Username, password);

        if (result == AdminSignInResult.Success)
            return Redirect("/admin");

        switch (result)
        {
            case AdminSignInResult.RateLimited:
                var minutes = (int)Math.Ceiling((retryAfter ?? AdminLoginLimiter.Lockout).TotalMinutes);
                Response.Headers.RetryAfter = (minutes * 60).ToString();
                Response.StatusCode = StatusCodes.Status429TooManyRequests;
                ErrorMessage = $"Too many attempts. Try again in {minutes} minute{(minutes == 1 ? string.Empty : "s")}.";
                break;
            case AdminSignInResult.Busy:
                Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                ErrorMessage = "The server is busy. Try again in a moment.";
                break;
            default:
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                ErrorMessage = "Invalid username or password.";
                break;
        }

        return Page();
    }

    public IActionResult OnPostSignOut()
    {
        auth.SignOut(HttpContext);
        return Redirect("/admin");
    }
}
