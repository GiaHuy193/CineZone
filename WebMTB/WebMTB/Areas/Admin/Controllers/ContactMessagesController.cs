using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;

namespace WebMTB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ContactMessagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContactMessagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/ContactMessages
        public async Task<IActionResult> Index(string? status, string? keyword)
        {
            var query = _context.ContactMessages.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                query = query.Where(x => x.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();

                query = query.Where(x =>
                    x.FullName.Contains(keyword) ||
                    x.Email.Contains(keyword) ||
                    x.Subject.Contains(keyword) ||
                    x.Message.Contains(keyword));
            }

            var messages = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            ViewBag.Status = status ?? "All";
            ViewBag.Keyword = keyword ?? "";

            ViewBag.Total = await _context.ContactMessages.CountAsync();
            ViewBag.NewCount = await _context.ContactMessages.CountAsync(x => x.Status == "New");
            ViewBag.ProcessingCount = await _context.ContactMessages.CountAsync(x => x.Status == "Processing");
            ViewBag.DoneCount = await _context.ContactMessages.CountAsync(x => x.Status == "Done");

            return View(messages);
        }

        // GET: /Admin/ContactMessages/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var message = await _context.ContactMessages
                .FirstOrDefaultAsync(x => x.Id == id);

            if (message == null)
            {
                return NotFound();
            }

            // Khi admin mở chi tiết lần đầu, tự chuyển từ Mới gửi sang Đang xử lý
            if (message.Status == "New")
            {
                message.Status = "Processing";
                await _context.SaveChangesAsync();
            }

            return View(message);
        }

        // POST: /Admin/ContactMessages/UpdateStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var message = await _context.ContactMessages.FindAsync(id);

            if (message == null)
            {
                return NotFound();
            }

            if (status != "New" && status != "Processing" && status != "Done")
            {
                TempData["Error"] = "Trạng thái không hợp lệ.";
                return RedirectToAction(nameof(Details), new { id });
            }

            message.Status = status;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật trạng thái liên hệ thành công.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Admin/ContactMessages/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var message = await _context.ContactMessages
                .FirstOrDefaultAsync(x => x.Id == id);

            if (message == null)
            {
                return NotFound();
            }

            return View(message);
        }

        // POST: /Admin/ContactMessages/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var message = await _context.ContactMessages.FindAsync(id);

            if (message == null)
            {
                return NotFound();
            }

            _context.ContactMessages.Remove(message);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa liên hệ thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}