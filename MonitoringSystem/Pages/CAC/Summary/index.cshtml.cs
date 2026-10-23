using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MonitoringSystem.Pages.CAC.Summary
{
    /// <summary>
    /// Endpoint ditutup pada Panamon Audio (deprecated).
    /// </summary>
    public class SummaryModel : PageModel
    {
        public IActionResult OnGet() => NotFound();
        public IActionResult OnPost() => NotFound();
    }
}