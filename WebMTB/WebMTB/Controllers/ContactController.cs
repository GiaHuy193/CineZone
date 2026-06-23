using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using WebMTB.Data;
using WebMTB.Models;
using System.Text.Encodings.Web;

namespace WebMTB.Controllers
{
    public class ContactController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<IdentityUser> _userManager;

        public ContactController(
            ApplicationDbContext context,
            IEmailSender emailSender,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _emailSender = emailSender;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new ContactInputModel();

            if (User.Identity?.IsAuthenticated == true)
            {
                model.FullName = User.Identity.Name ?? "";
                model.Email = User.Identity.Name ?? "";
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ContactInputModel input, string? returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            string? userId = null;

            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                userId = user?.Id;
            }

            var contactMessage = new ContactMessage
            {
                FullName = input.FullName.Trim(),
                Email = input.Email.Trim(),
                Subject = input.Subject.Trim(),
                Message = input.Message.Trim(),
                UserId = userId,
                CreatedAt = DateTime.Now,
                Status = "New"
            };

            _context.ContactMessages.Add(contactMessage);
            await _context.SaveChangesAsync();

            string adminEmail = "maidoangiahuy2004@gmail.com";

            string emailBody = $@"
            <div style='font-family:Arial,sans-serif;background:#111;padding:32px;color:#fff;'>
                <div style='max-width:620px;margin:0 auto;background:#1b1b1f;border-radius:18px;padding:28px;border:1px solid #333;'>
                    <h1 style='color:#e50914;margin-top:0;'>CineZone</h1>
                    <h2>Liên hệ hỗ trợ mới</h2>

                    <p><b>Họ tên:</b> {contactMessage.FullName}</p>
                    <p><b>Email:</b> {contactMessage.Email}</p>
                    <p><b>Tiêu đề:</b> {contactMessage.Subject}</p>
                    <p><b>Thời gian gửi:</b> {contactMessage.CreatedAt:dd/MM/yyyy HH:mm}</p>

                    <hr style='border-color:#333;' />

                    <p style='line-height:1.7;white-space:pre-line;'>{contactMessage.Message}</p>
                </div>
            </div>";

            await _emailSender.SendEmailAsync(
                adminEmail,
                $"[CineZone] Liên hệ hỗ trợ: {contactMessage.Subject}",
                emailBody
            );

            TempData["Success"] = "Gửi liên hệ thành công. CineZone sẽ phản hồi bạn sớm nhất có thể.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }
    }


    public class ContactInputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(100, ErrorMessage = "Họ tên không được quá 100 ký tự.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề cần hỗ trợ.")]
        [StringLength(200, ErrorMessage = "Tiêu đề không được quá 200 ký tự.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập nội dung liên hệ.")]
        [StringLength(2000, ErrorMessage = "Nội dung không được quá 2000 ký tự.")]
        public string Message { get; set; } = string.Empty;
    }
}