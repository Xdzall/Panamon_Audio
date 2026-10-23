using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MonitoringSystem.Pages.ACOEE.Performance
{
    /// <summary>
    /// Endpoint ditutup pada Panamon Audio (deprecated).
    /// </summary>
    public class IndexModel : PageModel
    {
        public IActionResult OnGet() => NotFound();
        public IActionResult OnPost() => NotFound();
    }
}