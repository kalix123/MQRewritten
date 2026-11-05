using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Base.Core.Configs;
using Server.Base.Core.Extensions;
using Server.Base.Core.Services;
using Server.Base.Database.Accounts;
using System.ComponentModel.DataAnnotations;
using Web.Razor.Services;

namespace Web.Razor.Pages.En.SignUp;

[BindProperties]
public class Forgot_PasswordModel(InternalRwConfig iConfig, AccountHandler aHandler,
    PagesService email, TemporaryDataStorage tempStorage) : PageModel
{
    [Required(ErrorMessage = "Please Enter Username")]
    [Display(Name = "User Name")]
    [StringLength(15, ErrorMessage = "The {0} cannot be over {1} characters long.")]
    public string Username { get; set; }

    public void OnGet() => ViewData["ServerName"] = iConfig.ServerName;

    public async Task<IActionResult> OnPost()
    {
        if (!ModelState.IsValid)
            return Page();

        Username = Username.Sanitize();

        if (string.IsNullOrEmpty(Username))
            return Page();

        if (aHandler.ContainsUsername(Username))
        {
            var account = aHandler.GetAccountFromUsername(Username);

            var sId = PagesService.NewResetToken();

            tempStorage.AddData(sId, account.Write);

            await email.SendPasswordResetEmailAsync(account.Email, $"{email.SiteUrl}/en/signup/resetpassword?id={sId}", account.Username);
        }
        else
        {
            await PagesService.Delay();
        }

        return RedirectToPage("ForgotPasswordConfirmation");
    }
}
