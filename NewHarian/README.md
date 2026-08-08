# NewHarian

ASP.NET Core 10 MVC — Phase 1 closed. Roadmap: [docs/sprints](../docs/sprints/README.md).

## Run

```bash
cd src/NewHarian.Web
dotnet run
```

Cấu hình DB + SMTP **không** commit mật khẩu trong `appsettings.json`.

- **CI/CD / server:** secrets → env (`ConnectionStrings__DefaultConnection`, `Email__Smtp__User`, `Email__Smtp__Password`, `Email__Smtp__From`, …).
- **Local:** copy `appsettings.Development.local.json.example` → `appsettings.Development.local.json` (đã gitignore), điền giá trị thật rồi `dotnet run`.

Seed admin: `admin@harian.local` / `Admin@12345` — **change on any shared/production deploy**.

Email ghi `App_Data/outbox/` khi `Email:Smtp:Enabled=false`. VNPay tắt đến khi cấu hình `Payment:VnPay`.

Private CV files are stored under `App_Data/private/` (not publicly served); admin downloads via `/admin/Applications/Cv/{id}`.

Security notes: [docs/engineering/security.md](../docs/engineering/security.md).

## Key URLs

| Area | URL |
|------|-----|
| Home / About / Company | `/`, `/about`, `/company` |
| Legal | `/legal/privacy`, `/legal/terms` |
| Contact / Careers | `/contact`, `/careers` |
| Shop | `/products`, `/Cart`, `/checkout`, `/orders/track` |
| Admin | `/admin/login` |
| Health | `/health` (JSON + DB check) |
| CMS / Menus / Slides | `/admin/Pages`, `/admin/Menus`, `/admin/HomeSlides` |
| Inquiries / Applications | `/admin/Inquiries`, `/admin/Applications` |
| Users / Media / Ship | `/admin/Users`, `/admin/Media`, `/admin/Shipping` |

## Tests

```bash
dotnet test tests/NewHarian.Web.Tests/NewHarian.Web.Tests.csproj
```
