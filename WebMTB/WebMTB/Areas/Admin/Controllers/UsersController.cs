using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;

namespace WebMTB.Areas.Admin.Controllers
{
    public class AdminUserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public bool LockoutEnabled { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public int BookingCount { get; set; }
        public decimal TotalSpent { get; set; }
    }

    [Area("Admin")]
    [Authorize]
    public class UsersController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<IdentityUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users
                .OrderBy(u => u.Email)
                .ToListAsync();

            var result = new List<AdminUserViewModel>();

            foreach (var user in users)
            {
                int bookingCount = await _context.Bookings
                    .CountAsync(b => b.UserId == user.Id);

                decimal totalSpent = await _context.Bookings
                    .Where(b => b.UserId == user.Id && b.Status == "Completed")
                    .SumAsync(b => (decimal?)b.TotalAmount) ?? 0;

                result.Add(new AdminUserViewModel
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    UserName = user.UserName ?? "",
                    EmailConfirmed = user.EmailConfirmed,
                    LockoutEnabled = user.LockoutEnabled,
                    LockoutEnd = user.LockoutEnd,
                    BookingCount = bookingCount,
                    TotalSpent = totalSpent
                });
            }

            ViewBag.TotalUsers = result.Count;
            ViewBag.ConfirmedUsers = result.Count(u => u.EmailConfirmed);
            ViewBag.LockedUsers = result.Count(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.Now);
            ViewBag.TotalCustomerRevenue = result.Sum(u => u.TotalSpent);

            return View(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.Now.AddYears(10));

            TempData["Success"] = "Đã khóa tài khoản người dùng.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            await _userManager.SetLockoutEndDateAsync(user, null);

            TempData["Success"] = "Đã mở khóa tài khoản người dùng.";
            return RedirectToAction(nameof(Index));
        }
    }
}