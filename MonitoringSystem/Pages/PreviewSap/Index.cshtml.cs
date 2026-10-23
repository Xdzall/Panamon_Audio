using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MonitoringSystem.Pages.PreviewSap
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet() => NotFound();
        public IActionResult OnPost() => NotFound();
    }
}
