using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MonitoringSystem.Pages.ACOEE.Quality
{
    /// <summary>
    /// Endpoint ditutup pada Panamon Audio (deprecated).
    /// </summary>
    public class QualityModel : PageModel
    {
        public IActionResult OnGet() => NotFound();
        public IActionResult OnPost() => NotFound();
    }
}