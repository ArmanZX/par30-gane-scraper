using Microsoft.AspNetCore.Mvc;
using Par30GamesApi.Services;

namespace Par30GamesApi.Controllers;

[ApiController]
[Route("api/games")]
public sealed class GamesController : ControllerBase
{
    private readonly Par30GamesScraper _scraper;
    public GamesController(Par30GamesScraper scraper) => _scraper = scraper;

    [HttpGet]
    public async Task<IActionResult> List(int page = 1) => Ok(await _scraper.GetGamesAsync(page));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var game = await _scraper.GetGameAsync(id);
        return game is null ? NotFound() : Ok(game);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, int page = 1) =>
        Ok(await _scraper.SearchGamesAsync(q, page));

    [HttpGet("categories")]
    public async Task<IActionResult> Categories() => Ok(await _scraper.GetPcCategoriesAsync());

    [HttpGet("category")]
    public async Task<IActionResult> Category([FromQuery] string url, int page = 1) =>
        Ok(await _scraper.GetGamesByCategoryAsync(url, page));
}
