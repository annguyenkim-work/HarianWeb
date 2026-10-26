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

Mã đại lý được hash bằng khóa `DealerAccessCode:HashKey` (base64 ≥ 32 byte); không cấu hình thì app tự tạo `App_Data/keys/dealer-access-code.key`. Giữ / backup file này: mất khóa thì mọi mã đại lý phải cấp lại.

Security notes: [docs/engineering/security.md](../docs/engineering/security.md).

## Key URLs

| Area | URL |
|------|-----|
| Home / About | `/`, `/about` (`/company` → redirect) |
| Legal | `/legal/privacy`, `/legal/terms` |
| Contact / Careers | `/contact`, `/careers` |
| Shop | `/products`, `/Cart`, `/checkout`, `/orders/track` |
| Admin | `/admin/login` |
| Health | `/health` (JSON + DB check) |
| CMS / Menus / Slides | `/admin/Pages`, `/admin/Menus`, `/admin/HomeSlides` |
| Inquiries / Applications | `/admin/Inquiries`, `/admin/Applications` |
| Users / Media / Ship | `/admin/Users`, `/admin/Media`, `/admin/Shipping` |

## Tests

**Bắt buộc:** mọi thay đổi code phải có test Unit + Integration và cả hai project phải xanh trước khi merge. CI chặn deploy khi `build-and-test` đỏ. Bỏ tầng nào thì ghi lý do trong PR (chỉ chấp nhận khi tầng đó không có hành vi để kiểm, vd. chỉ sửa docs).

| Tầng | Project | Phạm vi |
|------|---------|---------|
| Unit | `tests/NewHarian.UnitTests` | Logic thuần (policy, validator, helper), không web host, không DB |
| Integration | `tests/NewHarian.Web.Tests` | HTTP + DI + Identity + phân quyền, EF InMemory |

```bash
dotnet test tests/NewHarian.UnitTests
dotnet test tests/NewHarian.Web.Tests
```

Chi tiết, quy ước và Definition of Done: [docs/engineering/testing.md](../docs/engineering/testing.md); checklist PR: [.github/pull_request_template.md](../.github/pull_request_template.md).
