#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using WebMTB.Service;

namespace WebMTB.Areas.Identity.Pages.Account
{
    public class VerifyResetPasswordOtpModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly OtpService _otpService;

        public VerifyResetPasswordOtpModel(
            UserManager<IdentityUser> userManager,
            OtpService otpService)
        {
            _userManager = userManager;
            _otpService = otpService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP gồm 6 chữ số.")]
            public string OtpCode { get; set; }
        }

        public IActionResult OnGet(string email)
        {
            Input = new InputModel
            {
                Email = email
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            string email = Input.Email.Trim().ToLower();

            var otpCheck = _otpService.ValidateOtp(email, "reset-password", Input.OtpCode);

            if (!otpCheck.Success)
            {
                ModelState.AddModelError(string.Empty, otpCheck.Message);
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy tài khoản.");
                return Page();
            }

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            return RedirectToPage("./ResetPassword", new
            {
                code,
                email
            });
        }
    }
}