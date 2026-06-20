using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;
using WebMTB.Models;

namespace WebMTB.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index(string? keyword, int? genreId)
        {
            var today = DateTime.Today;

            bool hasKeyword = !string.IsNullOrWhiteSpace(keyword);
            bool hasGenre = genreId.HasValue && genreId.Value > 0;
            bool isSearch = hasKeyword || hasGenre;

            if (hasKeyword)
            {
                keyword = keyword!.Trim();
            }

            // 1. Phim hot: chỉ lấy phim đang active và đã đến ngày khởi chiếu
            var hotMovies = await _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Where(m => m.IsHot == true
                            && m.IsActive == true
                            && m.ReleaseDate.Date <= today)
                .OrderByDescending(m => m.Id)
                .Take(5)
                .ToListAsync();

            ViewBag.HotMovies = hotMovies;

            // 2. Phim sắp chiếu: active nhưng ngày khởi chiếu ở tương lai
            var upcomingMovies = await _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Where(m => m.IsActive == true
                            && m.ReleaseDate.Date > today)
                .OrderBy(m => m.ReleaseDate)
                .Take(12)
                .ToListAsync();

            ViewBag.UpcomingMovies = upcomingMovies;

            // 3. Danh sách phim chính
            var moviesQuery = _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Where(m => m.IsActive == true)
                .AsQueryable();

            // Khi không tìm kiếm, chỉ hiện phim đang chiếu.
            // Khi có tìm kiếm, cho phép tìm cả phim đang chiếu và phim sắp chiếu.
            if (!isSearch)
            {
                moviesQuery = moviesQuery.Where(m => m.ReleaseDate.Date <= today);
            }

            if (hasKeyword)
            {
                moviesQuery = moviesQuery.Where(m =>
                    m.Title.Contains(keyword!) ||
                    m.Description.Contains(keyword!) ||
                    m.Director.Contains(keyword!) ||
                    m.Cast.Contains(keyword!));
            }

            if (hasGenre)
            {
                moviesQuery = moviesQuery.Where(m =>
                    m.MovieGenres.Any(mg => mg.GenreId == genreId!.Value));
            }

            ViewBag.SearchKeyword = keyword;
            ViewBag.SelectedGenreId = hasGenre ? genreId : null;
            ViewBag.IsSearch = isSearch;

            var showingMovies = await moviesQuery
                .OrderByDescending(m => m.Id)
                .Take(24)
                .ToListAsync();

            return View(showingMovies);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
