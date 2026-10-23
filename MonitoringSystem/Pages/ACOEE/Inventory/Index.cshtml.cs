using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MonitoringSystem.Pages.ACOEE.Inventory
{
    /// <summary>
    /// Endpoint ditutup pada Panamon Audio (deprecated).
    /// </summary>
    public class indexModel : PageModel
    {
        public IActionResult OnGet() => NotFound();
        public IActionResult OnPost() => NotFound();
    }
}