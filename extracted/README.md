# Par30Games API + Web Panel

ASP.NET Core 8 Web API + HtmlAgilityPack.

## Run
```bash
dotnet restore
dotnet run
```

Swagger:
`/swagger`

Web panel:
`/`

## API
- GET `/api/games?page=1`
- GET `/api/games/{id}`
- GET `/api/games/search?q=call%20of%20duty&page=1`
- GET `/api/games/categories`
- GET `/api/games/category?url=https%3A%2F%2Fpar30games.net%2F...&page=1`

## Publish
```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

For Linux:
```bash
dotnet publish -c Release -r linux-x64 --self-contained false -o publish
```

## Important
HTML selectors on third-party sites can change. The scraper intentionally uses several generic signals and should be regression-tested against the current site before production deployment.
