#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InvenTrackFinalProject.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        public IActionResult OnGet(string returnUrl = null)
        {
            return RedirectToPage("/Account/Login");
        }

        public IActionResult OnPost(string returnUrl = null)
        {
            return RedirectToPage("/Account/Login");
        }
    }
}