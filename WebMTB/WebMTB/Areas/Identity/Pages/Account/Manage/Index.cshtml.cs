using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;
using WebMTB.Models;

namespace WebMTB.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public IndexModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        public string Username { get; set; } = string.Empty;

        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public int TotalBookings { get; set; }
        public int CompletedBookings { get; set; }
        public int PendingBookings { get; set; }
        public int CancelledBookings { get; set; }
        public decimal TotalSpent { get; set; }

        public List<Booking> RecentBookings { get; set; } = new List<Booking>();

        public class InputModel
        {
            [Phone]
            [Display(Name = "Số điện thoại")]
            public string? PhoneNumber { get; set; }
        }

        private async Task LoadAsync(IdentityUser user)
        {
            Username = await _userManager.GetUserNameAsync(user) ?? "";

            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            Input = new InputModel
            {
                PhoneNumber = phoneNumber
            };

            var userId = user.Id;

            TotalBookings = await _context.Bookings
                .CountAsync(b => b.UserId == userId);

            CompletedBookings = await _context.Bookings
                .CountAsync(b => b.UserId == userId && b.Status == "Completed");

            PendingBookings = await _context.Bookings
                .CountAsync(b => b.UserId == userId && b.Status == "Pending");

            CancelledBookings = await _context.Bookings
                .CountAsync(b => b.UserId == userId && b.Status == "Cancelled");

            TotalSpent = await _context.Bookings
                .Where(b => b.UserId == userId && b.Status == "Completed")
                .SumAsync(b => (decimal?)b.TotalAmount) ?? 0;

            RecentBookings = await _context.Bookings
                .Where(b => b.UserId == userId)
                .Include(b => b.Tickets)
                    .ThenInclude(t => t.Seat)
                .Include(b => b.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(b => b.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Room)
                            .ThenInclude(r => r.Cinema)
                .OrderByDescending(b => b.BookingDate)
                .Take(4)
                .ToListAsync();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound($"Không tìm thấy user có ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound($"Không tìm thấy user có ID '{_userManager.GetUserId(User)}'.");
            }

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
                    StatusMessage = "Lỗi: Không thể cập nhật số điện thoại.";
                    await LoadAsync(user);
                    return Page();
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Hồ sơ của bạn đã được cập nhật.";

            return RedirectToPage();
        }
    }
}