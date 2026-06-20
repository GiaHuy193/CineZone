using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;
using WebMTB.Models;

namespace WebMTB.Controllers
{
    public class MoviesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MoviesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Movies (Cho User xem danh sách phim ngoài trang chủ)
        public async Task<IActionResult> Index()
        {
            // Chỉ lấy những phim đang Active để hiện cho khách
            var movies = await _context.Movies
                                .Where(m => m.IsActive)
                                .ToListAsync();
            return View(movies);
        }

        // GET: Movies/Details/5 (Cho User xem chi tiết phim để đặt vé)
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var movie = await _context.Movies
                .Include(m => m.MovieGenres)
                .ThenInclude(mg => mg.Genre)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (movie == null) return NotFound();

            return View(movie);
        }
    }
}