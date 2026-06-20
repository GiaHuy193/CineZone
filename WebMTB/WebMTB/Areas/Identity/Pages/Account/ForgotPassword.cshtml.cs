// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using WebMTB.Service;

namespace WebMTB.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IMemoryCache _cache;
        private readonly OtpService _otpService;

        public ForgotPasswordModel(
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender,
            IMemoryCache cache,
            OtpService otpService)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _cache = cache;
            _otpService = otpService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập email.")]
            [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
            [RegularExpression(@"^[A-Za-z0-9._%+-]+@gmail\.com$", ErrorMessage = "Chỉ cho phép sử dụng email Gmail, ví dụ: name@gmail.com.")]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            string email = Input.Email.Trim().ToLower();
            string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            string cacheKey = $"forgot-password:{email}:{ip}";

            if (_cache.TryGetValue(cacheKey, out _))
            {
                ModelState.AddModelError(string.Empty, "Bạn vừa yêu cầu đặt lại mật khẩu. Vui lòng chờ 2 phút trước khi gửi lại.");
                return Page();
            }

            _cache.Set(cacheKey, true, TimeSpan.FromMinutes(2));

            var user = await _userManager.FindByEmailAsync(email);

            if (user != null && await _userManager.IsEmailConfirmedAsync(user))
            {
                var otpResult = _otpService.GenerateOtp(email, "reset-password");

                if (otpResult.Success)
                {
                    var emailBody = $@"
            <div style='font-family:Arial,sans-serif;background:#111;padding:32px;color:#fff;'>
                <div style='max-width:560px;margin:0 auto;background:#1b1b1f;border-radius:18px;padding:28px;border:1px solid #333;'>
                    <h1 style='color:#e50914;margin-top:0;'>CineZone</h1>

                    <h2>Đặt lại mật khẩu</h2>

                    <p style='color:#ccc;line-height:1.6;'>
                        Mã OTP đặt lại mật khẩu CineZone của bạn là:
                    </p>

                    <div style='font-size:34px;font-weight:bold;letter-spacing:8px;color:#fff;background:#e50914;padding:16px 20px;border-radius:14px;text-align:center;margin:22px 0;'>
                        {otpResult.Code}
                    </div>

                    <p style='color:#aaa;font-size:14px;'>
                        Mã OTP có hiệu lực trong 5 phút. Nếu bạn không yêu cầu thao tác này, vui lòng bỏ qua email.
                    </p>
                </div>
            </div>";

                    await _emailSender.SendEmailAsync(
                        email,
                        "Mã OTP đặt lại mật khẩu CineZone",
                        emailBody);
                }
            }

            return RedirectToPage("./VerifyResetPasswordOtp", new { email });
        }
    }
}