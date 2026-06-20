#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebMTB.Service;

namespace WebMTB.Areas.Identity.Pages.Account
{
    public class ConfirmEmailOtpModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly OtpService _otpService;

        public ConfirmEmailOtpModel(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender,
            OtpService otpService)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _otpService = otpService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

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

            var email = Input.Email.Trim().ToLower();
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy tài khoản.");
                return Page();
            }

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                TempData["StatusMessage"] = "Tài khoản đã được xác thực. Vui lòng đăng nhập.";
                return RedirectToPage("./Login");
            }

            var otpCheck = _otpService.ValidateOtp(email, "confirm-email", Input.OtpCode);

            if (!otpCheck.Success)
            {
                ModelState.AddModelError(string.Empty, otpCheck.Message);
                return Page();
            }

            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);

            TempData["StatusMessage"] = "Xác thực tài khoản thành công. Vui lòng đăng nhập.";
            return RedirectToPage("./Login");
        }

        public async Task<IActionResult> OnPostResendAsync()
        {
            if (string.IsNullOrWhiteSpace(Input?.Email))
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy email để gửi lại OTP.");
                return Page();
            }

            var email = Input.Email.Trim().ToLower();
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy tài khoản.");
                return Page();
            }

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                TempData["StatusMessage"] = "Tài khoản đã được xác thực. Vui lòng đăng nhập.";
                return RedirectToPage("./Login");
            }

            var otpResult = _otpService.GenerateOtp(email, "confirm-email");

            if (!otpResult.Success)
            {
                ModelState.AddModelError(string.Empty, otpResult.Message);
                return Page();
            }

            await _emailSender.SendEmailAsync(
                email,
                "Mã OTP xác thực tài khoản CineZone",
                $"<h2>Mã OTP của bạn là: <strong>{otpResult.Code}</strong></h2><p>Mã có hiệu lực trong 5 phút.</p>");

            StatusMessage = "Đã gửi lại mã OTP. Vui lòng kiểm tra email.";
            return Page();
        }
    }
}