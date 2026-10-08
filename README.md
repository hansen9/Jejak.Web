# SFD — Leader Dashboard (ASP.NET Core MVC)

MVC port of the React Router + PocketBase dashboard in `apps/`. Targets .NET 10.

## Run

    dotnet run -- create-user leader@example.com "a-password-8+" "Leader Name"   # sign-up is closed; create accounts here
    dotnet run                                                                  # http://localhost:5080

Data lives in SQLite (`ConnectionStrings:Default`, default `sfd.db`), created on first start.

## Layout

- `Controllers/` — `PagesController` (pages, robots, sitemap, error), `AccountController` (cookie login/logout), `TrackingApiController` (JSON API).
- `Services/` — `Geo` and `TrackingBuilder` (distance and route coverage), `ImportParsers` (GeoJSON/CSV), `DemoData`, `TrackingService`.
- `Views/` — Razor layout, dashboard and device pages; `_Sidebar`, `_Topbar`, `_Dialogs` partials.
- `wwwroot/js/` — `dashboard.js`, `device.js`, `map.js` (Leaflet), `common.js`. `wwwroot/css/site.css` is the original stylesheet minus Tailwind wrappers.
