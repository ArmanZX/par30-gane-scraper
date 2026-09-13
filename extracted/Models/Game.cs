namespace Par30GamesApi.Models;

public sealed class Game
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Image { get; set; } = "";
    public string Description { get; set; } = "";
    public string Developer { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string Genre { get; set; } = "";
    public string ReleaseDate { get; set; } = "";
    public string Platforms { get; set; } = "";
    public List<GameDownload> Downloads { get; set; } = new();
}

public sealed class GameDownload
{
    public string Version { get; set; } = "";
    public string Size { get; set; } = "";
    public List<GamePart> Parts { get; set; } = new();
}

public sealed class GamePart
{
    public int Part { get; set; }
    public string Name { get; set; } = "";
    public string Size { get; set; } = "";
    public string Url { get; set; } = "";
}

public sealed class GameCategory
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}
