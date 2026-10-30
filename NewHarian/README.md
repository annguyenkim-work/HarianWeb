# NewHarian

ASP.NET Core 10 MVC, PostgreSQL, EF Core 10.

Hiện trạng (chi tiết [docs/sprints/README.md](../docs/sprints/README.md)): Phase 1–6 theo sprint đã có trong code (CMS, shop, VietQR, vận hành đơn đa kênh, CCCD, đại lý, kho lô/vị trí/FEFO, RBAC 7 role). QA quét VietQR với ngân hàng còn owner. VNPay là scaffold, chưa gắn checkout.

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

Routing không phân biệt hoa thường. Hreflang dùng `?lang=vi|en|ja`.

| Area | URL |
|------|-----|
| Home | `/` |
| About | `/about` (`/company`, `/about/concept`, `/about/quality` redirect về đây) |
| Products | `/products`, `/products/search`, `/products/{category}/{slug}` |
| Services | `/services`, `/services/{category}/{slug}`, `.../book` |
| Shop | `/cart`, `/checkout`, `/orders/track` |
| Contact | `/contact` |
| Careers | `/careers`, `/careers/{slug}` |
| News | `/news`, `/news/{slug}` |
| Dealers | `/dealers`, `/dealers/register` |
| Help / Legal | `/help`, `/legal/privacy`, `/legal/terms` |
| SEO | `/sitemap.xml`, `/robots.txt` |
| Admin login | `/admin/login` |
| Health | `/health` (JSON + DB check) |

Menu admin nằm ở `src/NewHarian.Web/Areas/Admin/AdminNavigation.cs`. Mỗi mục hiện khi user có permission của mục đó. Form sản phẩm và dịch vụ là modal trên trang list.

| Nhóm | Mục |
|------|-----|
| Vận hành | Thống kê, Danh mục, Sản phẩm, Dịch vụ, Màu sắc, Đặt lịch, Đơn hàng, Kho, Liên hệ, Đại lý, Chiết khấu đại lý, Hồ sơ ứng tuyển, Hướng dẫn, Tin tức (`/admin/Posts?kind=News`), Tin tuyển dụng (`/admin/Posts?kind=Job`) |
| Nhân sự | Nhân viên (`/admin/Employees`) |
| Cài đặt website | Trang nội dung, Menus, Slides, Media, Phí ship (`/admin/Shipping`), Users, Thương hiệu & logo, Email & thông báo, Mẫu email, Cài đặt kho (`/admin/InventorySettings`), Ngân hàng / QR |

Ngoài menu: `/admin/logs`, `/admin/audit-logs`, `/admin/MyProfile`.

## Tests

**Bắt buộc:** mọi thay đổi code phải có test Unit + Integration và cả hai project phải xanh trước khi merge. CI chặn deploy khi `build-and-test` đỏ. Bỏ tầng nào thì ghi lý do trong PR (chỉ chấp nhận khi tầng đó không có hành vi để kiểm, vd. chỉ sửa docs).

Hai project dưới đây là toàn bộ test tự động. Chưa có project `NewHarian.E2E` và CI chưa có job `e2e`. Có file `appsettings.E2E.json`.

| Tầng | Project | Phạm vi |
|------|---------|---------|
| Unit | `tests/NewHarian.UnitTests` | Logic thuần (policy, validator, helper), không web host, không DB |
| Integration | `tests/NewHarian.Web.Tests` | HTTP + DI + Identity + phân quyền, EF InMemory |

```bash
dotnet test tests/NewHarian.UnitTests
dotnet test tests/NewHarian.Web.Tests
```

Chi tiết, quy ước và Definition of Done: [docs/engineering/testing.md](../docs/engineering/testing.md); checklist PR: [.github/pull_request_template.md](../.github/pull_request_template.md).
