using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMTB.Data;

namespace WebMTB.ViewComponents
{
    public class MovieSearchViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public MovieSearchViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var genres = await _context.Genres
                .OrderBy(g => g.Name)
                .ToListAsync();

            return View(genres);
        }
    }
}