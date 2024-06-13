using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadgeViewApp.Pages;

public class IndexModel(BadgeService _badgeService) : PageModel
{
    public List<string> Badges { get; set; } = [];

    public void OnGet()
    {
        this.Badges = _badgeService.GetBadges();
    }
}
