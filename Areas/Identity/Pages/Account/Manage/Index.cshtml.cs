#nullable disable

using System.ComponentModel.DataAnnotations;
using InvenTrack.Data;
using InvenTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace InvenTrackFinalProject.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly InvenTrackContext _db;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            InvenTrackContext db)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
        }

        public string Username { get; set; }
        public string AssignedLocationName { get; set; } = "All Locations";

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Display(Name = "Full name")]
            [Required]
            [StringLength(120)]
            public string FullName { get; set; }

            [Display(Name = "Job title")]
            [StringLength(120)]
            public string JobTitle { get; set; }

            [StringLength(120)]
            public string Department { get; set; }

            [Phone]
            [Display(Name = "Phone number")]
            public string PhoneNumber { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            Username = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            AssignedLocationName = user.AssignedStorageLocationId.HasValue
                ? await _db.StorageLocations
                    .AsNoTracking()
                    .Where(x => x.ID == user.AssignedStorageLocationId.Value)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync() ?? "Assigned location"
                : "All Locations";

            Input = new InputModel
            {
                FullName = user.FullName,
                JobTitle = user.JobTitle,
                Department = user.Department,
                PhoneNumber = phoneNumber
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Unexpected error when trying to set phone number.";
                    return RedirectToPage();
                }
            }

            user.FullName = (Input.FullName ?? string.Empty).Trim();
            user.JobTitle = string.IsNullOrWhiteSpace(Input.JobTitle) ? null : Input.JobTitle.Trim();
            user.Department = string.IsNullOrWhiteSpace(Input.Department) ? null : Input.Department.Trim();

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                StatusMessage = "Unexpected error when trying to update your profile.";
                return RedirectToPage();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Your profile has been updated";
            return RedirectToPage();
        }
    }
}
