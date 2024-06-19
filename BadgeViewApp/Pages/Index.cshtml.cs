using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadgeViewApp.Pages;

public class IndexModel(IConfiguration _configuration, BadgeService _badgeService) : PageModel
{
    public List<string> Badges { get; set; } = [];

    public string Message => _configuration["BadgeMaker:BadgeView:Message"] ?? "No message found in configuration.";

    public BadgeViewConfiguration Configuration =>
        _configuration.GetSection("BadgeMaker:BadgeView").Get<BadgeViewConfiguration>() ?? new BadgeViewConfiguration();

    public void OnGet()
    {
        this.Badges = _badgeService.GetBadges();
    }
}
