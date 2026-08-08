using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Email;
using NewHarian.Infrastructure.Identity;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.Admin, AppRoles.Staff })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        const string adminEmail = "admin@harian.local";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "System Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(admin, "Admin@12345");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, AppRoles.Admin);
            else
                logger.LogError("Failed to create admin: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await db.SiteSettings.AnyAsync())
        {
            db.SiteSettings.AddRange(
                new SiteSetting { Key = "company.name", Value = "Harian Co., Ltd.", Group = "company" },
                new SiteSetting { Key = "company.brand", Value = "Harian", Group = "company" },
                new SiteSetting { Key = "company.logo", Value = "", Group = "company" },
                new SiteSetting { Key = "company.phone", Value = "0934 811 819", Group = "company" },
                new SiteSetting { Key = "company.phone2", Value = "0903 526 556", Group = "company" },
                new SiteSetting { Key = "company.email", Value = "hariancorp.vn@gmail.com", Group = "company" },
                new SiteSetting { Key = "company.address", Value = "Số 83 đường Võ Chí Công, Hoà Xuân Đà Nẵng, Việt Nam", Group = "company" },
                new SiteSetting
                {
                    Key = "company.tagline.vi",
                    Value = "Chúng tôi hỗ trợ toàn diện cho việc mở rộng đầu tư giữa Nhật Bản - Việt Nam và góp phần vào việc phát triển của các doanh nghiệp.",
                    Group = "company"
                },
                new SiteSetting
                {
                    Key = "company.tagline.en",
                    Value = "We provide comprehensive support for Japan-Vietnam investment expansion and business development.",
                    Group = "company"
                },
                new SiteSetting
                {
                    Key = "company.tagline.ja",
                    Value = "日本とベトナム間の投資拡大と企業の発展を総合的に支援します。",
                    Group = "company"
                },
                new SiteSetting { Key = "company.facebook", Value = "", Group = "company" },
                new SiteSetting { Key = "company.instagram", Value = "", Group = "company" },
                new SiteSetting { Key = "company.bank.name", Value = "Vietcombank", Group = "company" },
                new SiteSetting { Key = "company.bank.bin", Value = "", Group = "company" },
                new SiteSetting { Key = "company.bank.account", Value = "0123456789", Group = "company" },
                new SiteSetting { Key = "company.bank.account_name", Value = "", Group = "company" },
                new SiteSetting { Key = "company.bank.branch", Value = "Chi nhánh Hà Nội", Group = "company" },
                new SiteSetting { Key = "contactcta.title.vi", Value = "Liên hệ với chúng tôi", Group = "contactcta" },
                new SiteSetting { Key = "contactcta.title.en", Value = "Contact us", Group = "contactcta" },
                new SiteSetting { Key = "contactcta.title.ja", Value = "お問い合わせ", Group = "contactcta" },
                new SiteSetting { Key = "contactcta.button.vi", Value = "Liên hệ", Group = "contactcta" },
                new SiteSetting { Key = "contactcta.button.en", Value = "Contact", Group = "contactcta" },
                new SiteSetting { Key = "contactcta.button.ja", Value = "お問い合わせ", Group = "contactcta" },
                new SiteSetting { Key = "contactcta.button.url", Value = "/contact", Group = "contactcta" },
                new SiteSetting { Key = "contactcta.visible", Value = "true", Group = "contactcta" },
                new SiteSetting { Key = "shipping.free_threshold", Value = "1000000", Group = "shipping" },
                new SiteSetting { Key = "notifications.inquiry_email", Value = "info@harian.local", Group = "notifications" },
                new SiteSetting { Key = "notifications.application_email", Value = "info@harian.local", Group = "notifications" },
                new SiteSetting { Key = "notifications.service_booking_email", Value = "info@harian.local", Group = "notifications" },
                new SiteSetting { Key = "notifications.order_email", Value = "info@harian.local", Group = "notifications" }
            );
            await db.SaveChangesAsync();
        }
        else
        {
            await EnsureSettingAsync(db, "company.brand", "Harian", "company");
            await EnsureSettingAsync(db, "company.logo", "", "company");
            await EnsureSettingAsync(db, "company.phone2", "0903 526 556", "company");
            await EnsureSettingAsync(db, "company.tagline.vi",
                "Chúng tôi hỗ trợ toàn diện cho việc mở rộng đầu tư giữa Nhật Bản - Việt Nam và góp phần vào việc phát triển của các doanh nghiệp.",
                "company");
            await EnsureSettingAsync(db, "company.tagline.en",
                "We provide comprehensive support for Japan-Vietnam investment expansion and business development.",
                "company");
            await EnsureSettingAsync(db, "company.tagline.ja",
                "日本とベトナム間の投資拡大と企業の発展を総合的に支援します。",
                "company");
            await EnsureSettingAsync(db, "company.facebook", "", "company");
            await EnsureSettingAsync(db, "company.instagram", "", "company");
            await EnsureSettingAsync(db, "company.bank.name", "Vietcombank", "company");
            await EnsureSettingAsync(db, "company.bank.bin", "", "company");
            await EnsureSettingAsync(db, "company.bank.account", "0123456789", "company");
            await EnsureSettingAsync(db, "company.bank.account_name", "", "company");
            await EnsureSettingAsync(db, "company.bank.branch", "Chi nhánh Hà Nội", "company");
            await RemoveSettingAsync(db, "company.bank.qr");
            await EnsureSettingAsync(db, "contactcta.title.vi", "Liên hệ với chúng tôi", "contactcta");
            await EnsureSettingAsync(db, "contactcta.title.en", "Contact us", "contactcta");
            await EnsureSettingAsync(db, "contactcta.title.ja", "お問い合わせ", "contactcta");
            await EnsureSettingAsync(db, "contactcta.button.vi", "Liên hệ", "contactcta");
            await EnsureSettingAsync(db, "contactcta.button.en", "Contact", "contactcta");
            await EnsureSettingAsync(db, "contactcta.button.ja", "お問い合わせ", "contactcta");
            await EnsureSettingAsync(db, "contactcta.button.url", "/contact", "contactcta");
            await EnsureSettingAsync(db, "contactcta.visible", "true", "contactcta");
            await EnsureSettingAsync(db, "notifications.order_email", "info@harian.local", "notifications");
        }

        // Legacy setting removed (header overlays hero; no menu background image)
        var obsoleteHeaderBg = await db.SiteSettings.Where(s => s.Key == "company.header_bg").ToListAsync();
        if (obsoleteHeaderBg.Count > 0)
        {
            db.SiteSettings.RemoveRange(obsoleteHeaderBg);
            await db.SaveChangesAsync();
        }

        await EnsureEmailTemplatesAsync(db);

        if (!await db.ShippingProvinces.AnyAsync())
        {
            await SeedProvincesAsync(db);
        }
        else if (await db.ShippingProvinces.CountAsync() < 20)
        {
            await SeedMissingProvincesAsync(db);
        }

        if (!await db.Pages.AnyAsync(p => p.Slug == "home"))
        {
            db.Pages.Add(new Page
            {
                Slug = "home",
                ModuleCode = "home",
                TemplateType = 1,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow,
                Translations =
                {
                    new PageTranslation { LanguageCode = "vi", Title = "Trang chủ", HeroTitle = "Harian", MetaTitle = "Harian" },
                    new PageTranslation { LanguageCode = "en", Title = "Home", HeroTitle = "Harian", MetaTitle = "Harian" },
                    new PageTranslation { LanguageCode = "ja", Title = "ホーム", HeroTitle = "Harian", MetaTitle = "Harian" }
                }
            });
            await db.SaveChangesAsync();
        }

        await SeedCmsContentAsync(db);

        await SeedCatalogAsync(db, logger);

        logger.LogInformation("Database seed completed.");
    }

    private static async Task SeedCmsContentAsync(AppDbContext db)
    {
        await EnsurePageAsync(db, "home", "home", 1,
            ("vi", "Trang chủ", "Harian", "Harian - Chất lượng Nhật Bản"),
            ("en", "Home", "Harian", "Harian - Japanese quality"),
            ("ja", "ホーム", "Harian", "Harian - 日本品質"));

        await EnsurePageAsync(db, "about", "about", 2,
            ("vi", "Giới thiệu", "Về Harian", "Giới thiệu Harian"),
            ("en", "About", "About Harian", "About Harian"),
            ("ja", "会社概要", "Harianについて", "Harianについて"));

        await EnsurePageAsync(db, "about/concept", "about", 2,
            ("vi", "Concept", "Concept", "Concept Harian"),
            ("en", "Concept", "Concept", "Harian Concept"),
            ("ja", "コンセプト", "コンセプト", "コンセプト"));

        await EnsurePageAsync(db, "about/quality", "about", 2,
            ("vi", "Tiêu chuẩn chất lượng", "Chất lượng", "Tiêu chuẩn chất lượng"),
            ("en", "Quality", "Quality", "Quality standards"),
            ("ja", "品質基準", "品質", "品質基準"));

        var home = await db.Pages.Include(p => p.ContentBlocks).FirstAsync(p => p.Slug == "home");
        if (!home.ContentBlocks.Any())
        {
            db.ContentBlocks.Add(new ContentBlock
            {
                PageId = home.Id,
                BlockType = ContentBlockType.RichText,
                SortOrder = 10,
                IsPublished = true,
                Translations =
                {
                    new ContentBlockTranslation
                    {
                        LanguageCode = "vi",
                        Title = "Giới thiệu",
                        Body = "<p>Harian cung cấp hóa chất và dịch vụ chất lượng Nhật Bản tại Việt Nam.</p>"
                    },
                    new ContentBlockTranslation
                    {
                        LanguageCode = "en",
                        Title = "Introduction",
                        Body = "<p>Harian provides Japanese-quality chemicals and services in Vietnam.</p>"
                    },
                    new ContentBlockTranslation
                    {
                        LanguageCode = "ja",
                        Title = "紹介",
                        Body = "<p>Harianはベトナムで日本品質の化学品とサービスを提供します。</p>"
                    }
                }
            });
            await db.SaveChangesAsync();
        }

        var about = await db.Pages.Include(p => p.ContentBlocks).FirstAsync(p => p.Slug == "about");
        if (!about.ContentBlocks.Any())
        {
            var tableJson = """
                {"rows":[
                  {"id":"row-1","sortOrder":1,"label":{"vi":"Tên công ty","en":"Company name","ja":"会社名"},"value":{"vi":"Công ty Cổ Phần Harian","en":"Harian Joint Stock Company","ja":"Harian株式会社"}},
                  {"id":"row-2","sortOrder":2,"label":{"vi":"Lĩnh vực","en":"Business","ja":"事業"},"value":{"vi":"Hóa chất & dịch vụ","en":"Chemicals & services","ja":"化学品・サービス"}}
                ]}
                """;
            db.ContentBlocks.Add(new ContentBlock
            {
                PageId = about.Id,
                BlockType = ContentBlockType.DataTable,
                SortOrder = 10,
                IsPublished = true,
                ExtraData = tableJson
            });
            db.ContentBlocks.Add(new ContentBlock
            {
                PageId = about.Id,
                BlockType = ContentBlockType.CtaButton,
                SortOrder = 20,
                IsPublished = true,
                LinkUrl = "/products",
                Translations =
                {
                    new ContentBlockTranslation { LanguageCode = "vi", Title = "Xem sản phẩm" },
                    new ContentBlockTranslation { LanguageCode = "en", Title = "View products" },
                    new ContentBlockTranslation { LanguageCode = "ja", Title = "製品を見る" }
                }
            });
            await db.SaveChangesAsync();
        }

        var concept = await db.Pages.Include(p => p.ContentBlocks).FirstAsync(p => p.Slug == "about/concept");
        if (!concept.ContentBlocks.Any())
        {
            db.ContentBlocks.Add(new ContentBlock
            {
                PageId = concept.Id,
                BlockType = ContentBlockType.RichText,
                SortOrder = 10,
                IsPublished = true,
                Translations =
                {
                    new ContentBlockTranslation { LanguageCode = "vi", Title = "Concept", Body = "<p>Chúng tôi mang tiêu chuẩn Nhật Bản đến từng sản phẩm và dịch vụ.</p>" },
                    new ContentBlockTranslation { LanguageCode = "en", Title = "Concept", Body = "<p>We bring Japanese standards to every product and service.</p>" },
                    new ContentBlockTranslation { LanguageCode = "ja", Title = "コンセプト", Body = "<p>すべての製品とサービスに日本基準をお届けします。</p>" }
                }
            });
            await db.SaveChangesAsync();
        }

        var quality = await db.Pages.Include(p => p.ContentBlocks).FirstAsync(p => p.Slug == "about/quality");
        if (!quality.ContentBlocks.Any())
        {
            db.ContentBlocks.Add(new ContentBlock
            {
                PageId = quality.Id,
                BlockType = ContentBlockType.BulletList,
                SortOrder = 10,
                IsPublished = true,
                Translations =
                {
                    new ContentBlockTranslation
                    {
                        LanguageCode = "vi",
                        Title = "Cam kết chất lượng",
                        Body = "Nguyên liệu kiểm soát nguồn gốc\nQuy trình chuẩn hóa\nHỗ trợ khách hàng tận tâm"
                    },
                    new ContentBlockTranslation
                    {
                        LanguageCode = "en",
                        Title = "Quality commitment",
                        Body = "Sourced materials\nStandardized process\nDedicated support"
                    },
                    new ContentBlockTranslation
                    {
                        LanguageCode = "ja",
                        Title = "品質へのこだわり",
                        Body = "原材料の管理\n標準化された工程\n丁寧なサポート"
                    }
                }
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Menus.AnyAsync(m => m.Code == "sidebar-about"))
        {
            db.Menus.Add(new Menu
            {
                Code = "sidebar-about",
                Name = "About sidebar",
                Items =
                {
                    MenuItemWith("/about", 1, "Giới thiệu", "About", "会社概要"),
                    MenuItemWith("/about/concept", 2, "Concept", "Concept", "コンセプト"),
                    MenuItemWith("/about/quality", 3, "Tiêu chuẩn chất lượng", "Quality", "品質基準")
                }
            });
            await db.SaveChangesAsync();
        }

        await EnsurePageAsync(db, "company", "company", 2,
            ("vi", "Công ty", "Thông tin công ty", "Thông tin công ty Harian"),
            ("en", "Company", "Company profile", "Harian company profile"),
            ("ja", "会社情報", "会社情報", "Harian会社情報"));

        await EnsurePageAsync(db, "legal/privacy", "legal", 2,
            ("vi", "Chính sách bảo mật", "Chính sách bảo mật", "Chính sách bảo mật"),
            ("en", "Privacy policy", "Privacy policy", "Privacy policy"),
            ("ja", "プライバシーポリシー", "プライバシーポリシー", "プライバシーポリシー"));

        await EnsurePageAsync(db, "legal/terms", "legal", 2,
            ("vi", "Điều khoản sử dụng", "Điều khoản sử dụng", "Điều khoản sử dụng"),
            ("en", "Terms of use", "Terms of use", "Terms of use"),
            ("ja", "利用規約", "利用規約", "利用規約"));

        var company = await db.Pages.Include(p => p.ContentBlocks).FirstAsync(p => p.Slug == "company");
        if (!company.ContentBlocks.Any())
        {
            db.ContentBlocks.Add(new ContentBlock
            {
                PageId = company.Id,
                BlockType = ContentBlockType.RichText,
                SortOrder = 10,
                IsPublished = true,
                Translations =
                {
                    new ContentBlockTranslation
                    {
                        LanguageCode = "vi",
                        Title = "Hồ sơ công ty",
                        Body = "<p>Harian hoạt động trong lĩnh vực hóa chất và dịch vụ liên quan, hướng tới tiêu chuẩn Nhật Bản.</p>"
                    },
                    new ContentBlockTranslation
                    {
                        LanguageCode = "en",
                        Title = "Company profile",
                        Body = "<p>Harian operates in chemicals and related services, aiming for Japanese standards.</p>"
                    },
                    new ContentBlockTranslation
                    {
                        LanguageCode = "ja",
                        Title = "会社概要",
                        Body = "<p>Harianは化学品および関連サービスを扱い、日本基準を目指します。</p>"
                    }
                }
            });
            var companyTable = """
                {"rows":[
                  {"id":"c1","sortOrder":1,"label":{"vi":"Tên pháp lý","en":"Legal name","ja":"法人名"},"value":{"vi":"Công ty Cổ Phần Harian","en":"Harian Joint Stock Company","ja":"Harian株式会社"}},
                  {"id":"c2","sortOrder":2,"label":{"vi":"Mã số thuế","en":"Tax code","ja":"税コード"},"value":{"vi":"0123456789","en":"0123456789","ja":"0123456789"}},
                  {"id":"c3","sortOrder":3,"label":{"vi":"Địa chỉ","en":"Address","ja":"住所"},"value":{"vi":"[Địa chỉ công ty]","en":"[Company address]","ja":"[会社住所]"}}
                ]}
                """;
            db.ContentBlocks.Add(new ContentBlock
            {
                PageId = company.Id,
                BlockType = ContentBlockType.DataTable,
                SortOrder = 20,
                IsPublished = true,
                ExtraData = companyTable
            });
            await db.SaveChangesAsync();
        }

        var privacy = await db.Pages.Include(p => p.ContentBlocks).FirstAsync(p => p.Slug == "legal/privacy");
        if (!privacy.ContentBlocks.Any())
        {
            db.ContentBlocks.Add(new ContentBlock
            {
                PageId = privacy.Id,
                BlockType = ContentBlockType.RichText,
                SortOrder = 10,
                IsPublished = true,
                Translations =
                {
                    new ContentBlockTranslation
                    {
                        LanguageCode = "vi",
                        Title = "Chính sách bảo mật",
                        Body = "<p>Chúng tôi tôn trọng quyền riêng tư của bạn. Thông tin cá nhân chỉ dùng để xử lý đơn hàng, đặt lịch và liên hệ hỗ trợ.</p><h2>Thu thập dữ liệu</h2><p>Họ tên, email, số điện thoại, địa chỉ giao hàng khi bạn gửi form hoặc đặt hàng.</p><h2>Liên hệ</h2><p>Mọi thắc mắc về bảo mật: info@harian.local</p>"
                    },
                    new ContentBlockTranslation
                    {
                        LanguageCode = "en",
                        Title = "Privacy policy",
                        Body = "<p>We respect your privacy. Personal data is used only to process orders, bookings, and support requests.</p><h2>Data we collect</h2><p>Name, email, phone, shipping address when you submit forms or place orders.</p><h2>Contact</h2><p>Privacy questions: info@harian.local</p>"
                    },
                    new ContentBlockTranslation
                    {
                        LanguageCode = "ja",
                        Title = "プライバシーポリシー",
                        Body = "<p>お客様のプライバシーを尊重します。個人情報は注文・予約・サポート対応のみに使用します。</p><h2>収集データ</h2><p>氏名、メール、電話、配送先住所など。</p><h2>お問い合わせ</h2><p>info@harian.local</p>"
                    }
                }
            });
            await db.SaveChangesAsync();
        }

        var terms = await db.Pages.Include(p => p.ContentBlocks).FirstAsync(p => p.Slug == "legal/terms");
        if (!terms.ContentBlocks.Any())
        {
            db.ContentBlocks.Add(new ContentBlock
            {
                PageId = terms.Id,
                BlockType = ContentBlockType.RichText,
                SortOrder = 10,
                IsPublished = true,
                Translations =
                {
                    new ContentBlockTranslation
                    {
                        LanguageCode = "vi",
                        Title = "Điều khoản sử dụng",
                        Body = "<p>Khi sử dụng website Harian, bạn đồng ý với các điều khoản sau.</p><h2>Đơn hàng</h2><p>Đơn hàng có hiệu lực sau khi chúng tôi xác nhận (COD) hoặc xác nhận thanh toán (chuyển khoản).</p><h2>Giá &amp; phí ship</h2><p>Giá hiển thị bằng VND; phí vận chuyển tính theo tỉnh/thành tại checkout.</p>"
                    },
                    new ContentBlockTranslation
                    {
                        LanguageCode = "en",
                        Title = "Terms of use",
                        Body = "<p>By using the Harian website, you agree to these terms.</p><h2>Orders</h2><p>Orders take effect after confirmation (COD) or payment confirmation (bank transfer).</p><h2>Pricing &amp; shipping</h2><p>Prices are in VND; shipping is calculated by province at checkout.</p>"
                    },
                    new ContentBlockTranslation
                    {
                        LanguageCode = "ja",
                        Title = "利用規約",
                        Body = "<p>本サイトの利用をもって、以下の規約に同意したものとみなします。</p><h2>注文</h2><p>COD確認後、または振込確認後に注文が確定します。</p><h2>価格・送料</h2><p>表示価格はVND、送料はチェックアウト時に都道府県で計算します。</p>"
                    }
                }
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Menus.AnyAsync(m => m.Code == "sidebar-company"))
        {
            db.Menus.Add(new Menu
            {
                Code = "sidebar-company",
                Name = "Company sidebar",
                Items =
                {
                    MenuItemWith("/company", 1, "Công ty", "Company", "会社情報"),
                    MenuItemWith("/careers", 2, "Tuyển dụng", "Careers", "採用"),
                    MenuItemWith("/contact", 3, "Liên hệ", "Contact", "お問い合わせ")
                }
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Menus.AnyAsync(m => m.Code == "sidebar-legal"))
        {
            db.Menus.Add(new Menu
            {
                Code = "sidebar-legal",
                Name = "Legal sidebar",
                Items =
                {
                    MenuItemWith("/legal/privacy", 1, "Chính sách bảo mật", "Privacy policy", "プライバシーポリシー"),
                    MenuItemWith("/legal/terms", 2, "Điều khoản sử dụng", "Terms of use", "利用規約")
                }
            });
            await db.SaveChangesAsync();
        }

        await EnsureSettingAsync(db, "maps.embed_url", "", "maps");

        await EnsurePageAsync(db, "contact", "contact", 5,
            ("vi", "Liên hệ", "Liên hệ", "Liên hệ Harian"),
            ("en", "Contact", "Contact", "Contact Harian"),
            ("ja", "お問い合わせ", "お問い合わせ", "お問い合わせ"));

        await EnsurePageAsync(db, "careers", "careers", 2,
            ("vi", "Tuyển dụng", "Tuyển dụng", "Tuyển dụng Harian"),
            ("en", "Careers", "Careers", "Careers at Harian"),
            ("ja", "採用情報", "採用情報", "採用情報"));

        var careers = await db.Pages.Include(p => p.ContentBlocks).FirstAsync(p => p.Slug == "careers");
        if (!careers.ContentBlocks.Any())
        {
            db.ContentBlocks.Add(new ContentBlock
            {
                PageId = careers.Id,
                BlockType = ContentBlockType.RichText,
                SortOrder = 10,
                IsPublished = true,
                Translations =
                {
                    new ContentBlockTranslation { LanguageCode = "vi", Title = "Gia nhập Harian", Body = "<p>Chúng tôi tìm kiếm đồng nghiệp nhiệt huyết, cầu tiến.</p>" },
                    new ContentBlockTranslation { LanguageCode = "en", Title = "Join Harian", Body = "<p>We look for passionate, growth-minded teammates.</p>" },
                    new ContentBlockTranslation { LanguageCode = "ja", Title = "採用メッセージ", Body = "<p>情熱と成長意欲のある方を歓迎します。</p>" }
                }
            });
            var jobsTable = """
                {"rows":[
                  {"id":"j1","sortOrder":1,"label":{"vi":"Thời gian làm việc","en":"Working hours","ja":"勤務時間"},"value":{"vi":"8:00-17:00","en":"8:00-17:00","ja":"8:00-17:00"}},
                  {"id":"j2","sortOrder":2,"label":{"vi":"Phúc lợi","en":"Benefits","ja":"福利厚生"},"value":{"vi":"BHXH, thưởng","en":"Insurance, bonus","ja":"保険・賞与"}}
                ]}
                """;
            db.ContentBlocks.Add(new ContentBlock
            {
                PageId = careers.Id,
                BlockType = ContentBlockType.DataTable,
                SortOrder = 20,
                IsPublished = true,
                ExtraData = jobsTable
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Menus.AnyAsync(m => m.Code == "sidebar-contact"))
        {
            db.Menus.Add(new Menu
            {
                Code = "sidebar-contact",
                Name = "Contact sidebar",
                Items =
                {
                    MenuItemWith("/contact", 1, "Liên hệ", "Contact", "お問い合わせ"),
                    MenuItemWith("/company", 2, "Công ty", "Company", "会社情報")
                }
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Menus.AnyAsync(m => m.Code == "sidebar-careers"))
        {
            db.Menus.Add(new Menu
            {
                Code = "sidebar-careers",
                Name = "Careers sidebar",
                Items =
                {
                    MenuItemWith("/careers", 1, "Tuyển dụng", "Careers", "採用"),
                    MenuItemWith("/company", 2, "Công ty", "Company", "会社情報")
                }
            });
            await db.SaveChangesAsync();
        }

        await EnsureHeaderMainMenuAsync(db);
    }

    private static async Task EnsureHeaderMainMenuAsync(AppDbContext db)
    {
        if (await db.Menus.AnyAsync(m => m.Code == "header-main"))
            return;

        var about = MenuItemWith("about", "/about", 4, "Giới thiệu", "About", "会社情報");
        var aboutPage = MenuItemWith("about-page", "/about", 1, "Giới thiệu", "About", "会社概要");
        var concept = MenuItemWith("concept", "/about/concept", 2, "Triết lý", "Concept", "コンセプト");
        var quality = MenuItemWith("quality", "/about/quality", 3, "Chất lượng", "Quality", "品質");
        var company = MenuItemWith("company", "/company", 4, "Công ty", "Company", "会社情報");
        aboutPage.Parent = about;
        concept.Parent = about;
        quality.Parent = about;
        company.Parent = about;

        db.Menus.Add(new Menu
        {
            Code = "header-main",
            Name = "Top header menu",
            Items =
            {
                MenuItemWith("home", "/", 1, "Trang chủ", "Home", "ホーム"),
                MenuItemWith("products", "/products", 2, "Sản phẩm", "Products", "製品"),
                MenuItemWith("services", "/services", 3, "Dịch vụ", "Services", "サービス"),
                about,
                aboutPage,
                concept,
                quality,
                company,
                MenuItemWith("news", "/news", 5, "Tin tức", "News", "ニュース"),
                MenuItemWith("careers", "/careers", 6, "Tuyển dụng", "Careers", "採用"),
                MenuItemWith("contact", "/contact", 7, "Liên hệ", "Contact", "お問い合わせ"),
                MenuItemWith("order-track", "/Orders/Track", 8, "Tra cứu đơn", "Order lookup", "注文照会")
            }
        });
        await db.SaveChangesAsync();
    }

    private static MenuItem MenuItemWith(string itemKey, string url, int order, string vi, string en, string ja) => new()
    {
        ItemKey = itemKey,
        Url = url,
        SortOrder = order,
        IsActive = true,
        Translations =
        {
            new MenuItemTranslation { LanguageCode = "vi", Label = vi },
            new MenuItemTranslation { LanguageCode = "en", Label = en },
            new MenuItemTranslation { LanguageCode = "ja", Label = ja }
        }
    };

    private static MenuItem MenuItemWith(string url, int order, string vi, string en, string ja)
        => MenuItemWith("", url, order, vi, en, ja);

    private static async Task EnsurePageAsync(
        AppDbContext db,
        string slug,
        string module,
        int template,
        (string Lang, string Title, string Hero, string Meta) vi,
        (string Lang, string Title, string Hero, string Meta) en,
        (string Lang, string Title, string Hero, string Meta) ja)
    {
        if (await db.Pages.AnyAsync(p => p.Slug == slug)) return;
        db.Pages.Add(new Page
        {
            Slug = slug,
            ModuleCode = module,
            TemplateType = template,
            IsPublished = true,
            CreatedAt = DateTime.UtcNow,
            Translations =
            {
                new PageTranslation { LanguageCode = vi.Lang, Title = vi.Title, HeroTitle = vi.Hero, MetaTitle = vi.Meta },
                new PageTranslation { LanguageCode = en.Lang, Title = en.Title, HeroTitle = en.Hero, MetaTitle = en.Meta },
                new PageTranslation { LanguageCode = ja.Lang, Title = ja.Title, HeroTitle = ja.Hero, MetaTitle = ja.Meta }
            }
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCatalogAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Categories.AnyAsync())
            return;

        var chemicals = new Category
        {
            Slug = "hoa-chat",
            SortOrder = 1,
            IsActive = true,
            ShowOnHome = true,
            CreatedAt = DateTime.UtcNow,
            Translations =
            {
                new CategoryTranslation { LanguageCode = "vi", Name = "Hóa chất", Description = "Sản phẩm tẩy rửa chuyên dụng" },
                new CategoryTranslation { LanguageCode = "en", Name = "Chemicals", Description = "Specialty cleaning products" },
                new CategoryTranslation { LanguageCode = "ja", Name = "化学品", Description = "専用洗浄製品" }
            }
        };

        var services = new Category
        {
            Slug = "dich-vu",
            SortOrder = 2,
            IsActive = true,
            ShowOnHome = true,
            CreatedAt = DateTime.UtcNow,
            Translations =
            {
                new CategoryTranslation { LanguageCode = "vi", Name = "Dịch vụ", Description = "Dịch vụ phun xe / bảo dưỡng" },
                new CategoryTranslation { LanguageCode = "en", Name = "Services", Description = "Vehicle coating services" },
                new CategoryTranslation { LanguageCode = "ja", Name = "サービス", Description = "車両コーティング" }
            }
        };

        db.Categories.AddRange(chemicals, services);
        await db.SaveChangesAsync();

        var gko = new Product
        {
            CategoryId = chemicals.Id,
            Slug = "gko-tay-nam-moc",
            Status = ProductStatus.Published,
            IsFeatured = true,
            SortOrder = 1,
            HasVariantSize = true,
            CreatedAt = DateTime.UtcNow,
            Translations =
            {
                new ProductTranslation { LanguageCode = "vi", Name = "GKO Tẩy nấm mốc", ShortDescription = "Tẩy nấm mốc chuyên dụng", Description = "<p>GKO giúp loại bỏ nấm mốc trên bề mặt.</p>" },
                new ProductTranslation { LanguageCode = "en", Name = "GKO Mold Remover", ShortDescription = "Specialty mold remover", Description = "<p>GKO removes mold from surfaces.</p>" },
                new ProductTranslation { LanguageCode = "ja", Name = "GKO カビ取り", ShortDescription = "専用カビ取り剤", Description = "<p>表面のカビを除去します。</p>" }
            },
            Variants =
            {
                new ProductVariant { Sku = "GKO-200ML", VariantLabel = "200ML", Price = 180000m, IsDefault = true, SortOrder = 1, IsActive = true },
                new ProductVariant { Sku = "GKO-500ML", VariantLabel = "500ML", Price = 350000m, IsDefault = false, SortOrder = 2, IsActive = true },
                new ProductVariant { Sku = "GKO-4L", VariantLabel = "4L", Price = 1200000m, IsDefault = false, SortOrder = 3, IsActive = true }
            }
        };

        var donan = new Product
        {
            CategoryId = chemicals.Id,
            Slug = "donan-bot-tay",
            Status = ProductStatus.Published,
            IsFeatured = true,
            SortOrder = 2,
            HasVariantSize = true,
            CreatedAt = DateTime.UtcNow,
            Translations =
            {
                new ProductTranslation { LanguageCode = "vi", Name = "Donan Bột tẩy", ShortDescription = "Bột tẩy đa năng", Description = "<p>Donan dùng cho nhiều bề mặt.</p>" },
                new ProductTranslation { LanguageCode = "en", Name = "Donan Powder", ShortDescription = "Multi-purpose powder", Description = "<p>Donan for multiple surfaces.</p>" },
                new ProductTranslation { LanguageCode = "ja", Name = "Donan 粉末", ShortDescription = "多用途粉末", Description = "<p>様々な表面に使用。</p>" }
            },
            Variants =
            {
                new ProductVariant { Sku = "DONAN-120G", VariantLabel = "120G", Price = 95000m, IsDefault = true, SortOrder = 1, IsActive = true },
                new ProductVariant { Sku = "DONAN-45G", VariantLabel = "45G", Price = 45000m, IsDefault = false, SortOrder = 2, IsActive = true }
            }
        };

        var coating = new Service
        {
            CategoryId = services.Id,
            Slug = "phun-xe",
            Status = ProductStatus.Published,
            IsFeatured = true,
            SortOrder = 1,
            HasVariantSize = true,
            CreatedAt = DateTime.UtcNow,
            Translations =
            {
                new ServiceTranslation { LanguageCode = "vi", Name = "Phun xe bảo vệ", ShortDescription = "Đặt lịch phun xe", Description = "<p>Dịch vụ phun bảo vệ bề mặt xe tại showroom hoặc tại nhà.</p>" },
                new ServiceTranslation { LanguageCode = "en", Name = "Vehicle Coating", ShortDescription = "Book a coating appointment", Description = "<p>Protective coating at showroom or at home.</p>" },
                new ServiceTranslation { LanguageCode = "ja", Name = "車両コーティング", ShortDescription = "予約受付", Description = "<p>ショールームまたはご自宅で施工。</p>" }
            },
            Variants =
            {
                new ServiceVariant { Sku = "SVC-SHOWROOM", VariantLabel = "Tại showroom", Price = 500000m, IsDefault = true, SortOrder = 1, IsActive = true },
                new ServiceVariant { Sku = "SVC-HOME", VariantLabel = "Tại nhà", Price = 800000m, IsDefault = false, SortOrder = 2, IsActive = true }
            }
        };

        db.Products.AddRange(gko, donan);
        db.Services.Add(coating);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded sample catalog (GKO, Donan, Phun xe).");
    }

    private static async Task EnsureSettingAsync(AppDbContext db, string key, string value, string group)
    {
        if (await db.SiteSettings.AnyAsync(s => s.Key == key)) return;
        db.SiteSettings.Add(new SiteSetting { Key = key, Value = value, Group = group });
        await db.SaveChangesAsync();
    }

    private static async Task RemoveSettingAsync(AppDbContext db, string key)
    {
        var row = await db.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (row is null) return;
        db.SiteSettings.Remove(row);
        await db.SaveChangesAsync();
    }

    private static (string Code, string NameVi, decimal Fee)[] ProvinceSeedData() =>
    [
        ("HN", "Hà Nội", 30000m), ("HCM", "TP. Hồ Chí Minh", 30000m), ("DN", "Đà Nẵng", 35000m),
        ("HP", "Hải Phòng", 35000m), ("CT", "Cần Thơ", 35000m), ("AG", "An Giang", 40000m),
        ("BRVT", "Bà Rịa - Vũng Tàu", 35000m), ("BG", "Bắc Giang", 35000m), ("BK", "Bắc Kạn", 45000m),
        ("BL", "Bạc Liêu", 45000m), ("BN", "Bắc Ninh", 30000m), ("BTre", "Bến Tre", 40000m),
        ("BD", "Bình Định", 40000m), ("BDuong", "Bình Dương", 30000m), ("BP", "Bình Phước", 40000m),
        ("BThuan", "Bình Thuận", 40000m), ("CM", "Cà Mau", 45000m), ("CB", "Cao Bằng", 45000m),
        ("DL", "Đắk Lắk", 45000m), ("DNong", "Đắk Nông", 45000m), ("DB", "Điện Biên", 50000m),
        ("DNai", "Đồng Nai", 30000m), ("DT", "Đồng Tháp", 40000m), ("GL", "Gia Lai", 45000m),
        ("HG", "Hà Giang", 50000m), ("HNam", "Hà Nam", 35000m), ("HT", "Hà Tĩnh", 40000m),
        ("HD", "Hải Dương", 35000m), ("HGiang", "Hậu Giang", 40000m), ("HB", "Hòa Bình", 40000m),
        ("HY", "Hưng Yên", 30000m), ("KH", "Khánh Hòa", 40000m), ("KG", "Kiên Giang", 45000m),
        ("KT", "Kon Tum", 50000m), ("LC", "Lai Châu", 50000m), ("LD", "Lâm Đồng", 45000m)
    ];

    private static async Task SeedProvincesAsync(AppDbContext db)
    {
        var sort = 1;
        foreach (var p in ProvinceSeedData())
        {
            db.ShippingProvinces.Add(new ShippingProvince
            {
                Code = p.Code,
                NameVi = p.NameVi,
                NameEn = p.NameVi,
                NameJa = p.NameVi,
                SortOrder = sort++,
                IsActive = true,
                Rate = new ShippingRate { Fee = p.Fee }
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task EnsureEmailTemplatesAsync(AppDbContext db)
    {
        var existing = await db.EmailTemplates.Select(t => t.Code).ToListAsync();
        var missing = EmailTemplateDefaults.All().Where(t => !existing.Contains(t.Code)).ToList();
        if (missing.Count == 0) return;
        db.EmailTemplates.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static async Task SeedMissingProvincesAsync(AppDbContext db)
    {
        var existing = await db.ShippingProvinces.Select(p => p.Code).ToListAsync();
        var sort = (await db.ShippingProvinces.MaxAsync(p => (int?)p.SortOrder) ?? 0) + 1;
        foreach (var p in ProvinceSeedData().Where(x => !existing.Contains(x.Code)))
        {
            db.ShippingProvinces.Add(new ShippingProvince
            {
                Code = p.Code,
                NameVi = p.NameVi,
                NameEn = p.NameVi,
                NameJa = p.NameVi,
                SortOrder = sort++,
                IsActive = true,
                Rate = new ShippingRate { Fee = p.Fee }
            });
        }
        await db.SaveChangesAsync();
    }
}
