using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;
using WebMTB.Models;
using Microsoft.AspNetCore.Authorization;

namespace WebMTB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] // Khóa chặt: Chỉ Admin mới vào được đây
    public class MoviesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MoviesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Movies
        public async Task<IActionResult> Index()
        {
            return View(await _context.Movies.ToListAsync());
        }

        // GET: Admin/Movies/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null) return NotFound();
            return View(movie);
        }

        // GET: Admin/Movies/Create
        public IActionResult Create()
        {
            ViewBag.Genres = _context.Genres.Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name }).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Description,Director,Cast,Duration,ReleaseDate,TrailerUrl,ImageUrl,CoverImageUrl,Rating,IsHot,IsActive,AgeRating")] Movie movie, string[] SelectedGenreIds)
        {
            if (ModelState.IsValid)
            {
                _context.Add(movie);
                await _context.SaveChangesAsync();

                await ProcessGenres(movie.Id, SelectedGenreIds);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            ViewBag.Genres = _context.Genres.Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name }).ToList();
            return View(movie);
        }

        // GET: Admin/Movies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var movie = await _context.Movies
                .Include(m => m.MovieGenres)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (movie == null) return NotFound();

            var currentGenreIds = movie.MovieGenres.Select(mg => mg.GenreId.ToString()).ToList();
            ViewBag.Genres = _context.Genres.Select(g => new SelectListItem
            {
                Value = g.Id.ToString(),
                Text = g.Name,
                Selected = currentGenreIds.Contains(g.Id.ToString())
            }).ToList();

            return View(movie);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Director,Cast,Duration,ReleaseDate,TrailerUrl,ImageUrl,CoverImageUrl,Rating,IsHot,IsActive,AgeRating")] Movie movie, string[] SelectedGenreIds)
        {
            if (id != movie.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(movie);
                    var existingGenres = _context.MovieGenres.Where(mg => mg.MovieId == id);
                    _context.MovieGenres.RemoveRange(existingGenres);

                    await ProcessGenres(movie.Id, SelectedGenreIds);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MovieExists(movie.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Genres = _context.Genres.Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name }).ToList();
            return View(movie);
        }

        // LOGIC XỬ LÝ THỂ LOẠI (Chỉ Admin mới có)
        private async Task ProcessGenres(int movieId, string[] selectedGenres)
        {
            if (selectedGenres == null || selectedGenres.Length == 0) return;
            foreach (var item in selectedGenres)
            {
                if (int.TryParse(item, out int genreId))
                {
                    _context.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genreId });
                }
                else
                {
                    var existing = await _context.Genres.FirstOrDefaultAsync(g => g.Name.ToLower() == item.ToLower());
                    if (existing != null)
                    {
                        _context.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = existing.Id });
                    }
                    else
                    {
                        var newGenre = new Genre { Name = item };
                        _context.Genres.Add(newGenre);
                        await _context.SaveChangesAsync();
                        _context.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = newGenre.Id });
                    }
                }
            }
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null) return NotFound();
            return View(movie);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie != null) _context.Movies.Remove(movie);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MovieExists(int id) => _context.Movies.Any(e => e.Id == id);


        // GET: /Admin/Movies/Import
        public IActionResult Import()
        {
            return View();
        }

        // POST: /Admin/Movies/Import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file CSV để import phim.";
                return RedirectToAction(nameof(Import));
            }

            var extension = Path.GetExtension(file.FileName);

            if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Hiện tại hệ thống chỉ hỗ trợ import file .csv.";
                return RedirectToAction(nameof(Import));
            }

            int inserted = 0;
            int skipped = 0;

            var existingTitles = new HashSet<string>(
                await _context.Movies.Select(m => m.Title).ToListAsync(),
                StringComparer.OrdinalIgnoreCase
            );

            var genreDict = new Dictionary<string, Genre>(StringComparer.OrdinalIgnoreCase);

            var currentGenres = await _context.Genres.ToListAsync();

            foreach (var genre in currentGenres)
            {
                genreDict[genre.Name] = genre;
            }

            using var stream = file.OpenReadStream();
            using var parser = new TextFieldParser(stream, Encoding.UTF8);

            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;

            // Bỏ qua dòng header
            if (!parser.EndOfData)
            {
                parser.ReadFields();
            }

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();

                if (fields == null || fields.Length < 1)
                {
                    skipped++;
                    continue;
                }

                string Get(int index)
                {
                    return fields.Length > index ? fields[index]?.Trim() ?? "" : "";
                }

                string title = Get(0);

                if (string.IsNullOrWhiteSpace(title))
                {
                    skipped++;
                    continue;
                }

                if (existingTitles.Contains(title))
                {
                    skipped++;
                    continue;
                }

                int.TryParse(Get(4), out int duration);

                DateTime releaseDate = DateTime.Today;

                DateTime.TryParseExact(
                    Get(5),
                    new[] { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out releaseDate
                );

                double.TryParse(
                    Get(10),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out double rating
                );

                bool isHot = ParseBool(Get(11), false);
                bool isActive = ParseBool(Get(12), true);

                var movie = new Movie
                {
                    Title = title,
                    Description = Get(1),
                    Director = Get(2),
                    Cast = Get(3),
                    Duration = duration > 0 ? duration : 90,
                    ReleaseDate = releaseDate == default ? DateTime.Today : releaseDate,
                    TrailerUrl = Get(6),
                    AgeRating = Get(7),
                    ImageUrl = Get(8),
                    CoverImageUrl = Get(9),
                    Rating = rating,
                    IsHot = isHot,
                    IsActive = isActive
                };

                string genresText = Get(13);

                if (!string.IsNullOrWhiteSpace(genresText))
                {
                    var genreNames = genresText
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(g => g.Trim())
                        .Where(g => !string.IsNullOrWhiteSpace(g))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var genreName in genreNames)
                    {
                        if (!genreDict.TryGetValue(genreName, out var genre))
                        {
                            genre = new Genre
                            {
                                Name = genreName
                            };

                            _context.Genres.Add(genre);
                            genreDict[genreName] = genre;
                        }

                        movie.MovieGenres.Add(new MovieGenre
                        {
                            Movie = movie,
                            Genre = genre
                        });
                    }
                }

                _context.Movies.Add(movie);

                existingTitles.Add(title);
                inserted++;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Import phim hoàn tất. Thêm mới: {inserted}, bỏ qua: {skipped}.";

            return RedirectToAction(nameof(Index));
        }

        private static bool ParseBool(string value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            value = value.Trim().ToLower();

            return value == "true"
                || value == "1"
                || value == "yes"
                || value == "y"
                || value == "có"
                || value == "co";
        }

        private static void ApplyMovieDisplayStatus(Movie movie, string? displayStatus)
        {
            var today = DateTime.Today;

            switch (displayStatus)
            {
                case "Upcoming":
                    movie.IsActive = true;

                    if (movie.ReleaseDate.Date <= today)
                    {
                        movie.ReleaseDate = today.AddDays(30);
                    }

                    break;

                case "Hidden":
                    movie.IsActive = false;
                    break;

                case "NowShowing":
                default:
                    movie.IsActive = true;

                    if (movie.ReleaseDate == default || movie.ReleaseDate.Date > today)
                    {
                        movie.ReleaseDate = today;
                    }

                    break;
            }
        }

    }
}