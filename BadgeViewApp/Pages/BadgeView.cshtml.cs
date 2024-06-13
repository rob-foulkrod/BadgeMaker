using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadgeViewApp.Pages;

public class BadgeView(BadgeService _badgeService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    public ActionResult OnGet()
    {
        var stream = _badgeService.GetBadge(Id);

        if (stream == null)
        {
            return NotFound();
        }
        else
        {
            return File(stream, "image/png");
        }
        
    }
}
