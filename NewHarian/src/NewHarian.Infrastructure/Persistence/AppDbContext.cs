using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NewHarian.Domain.Entities;
using NewHarian.Infrastructure.Identity;

namespace NewHarian.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryTranslation> CategoryTranslations => Set<CategoryTranslation>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductTranslation> ProductTranslations => Set<ProductTranslation>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceTranslation> ServiceTranslations => Set<ServiceTranslation>();
    public DbSet<ServiceVariant> ServiceVariants => Set<ServiceVariant>();
    public DbSet<ColorDefinition> ColorDefinitions => Set<ColorDefinition>();
    public DbSet<ColorDefinitionTranslation> ColorDefinitionTranslations => Set<ColorDefinitionTranslation>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();
    public DbSet<ServiceBooking> ServiceBookings => Set<ServiceBooking>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderHistory> OrderHistories => Set<OrderHistory>();
    public DbSet<ServiceBookingHistory> ServiceBookingHistories => Set<ServiceBookingHistory>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ShippingProvince> ShippingProvinces => Set<ShippingProvince>();
    public DbSet<ShippingRate> ShippingRates => Set<ShippingRate>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<PageTranslation> PageTranslations => Set<PageTranslation>();
    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();
    public DbSet<ContentBlockTranslation> ContentBlockTranslations => Set<ContentBlockTranslation>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuItemTranslation> MenuItemTranslations => Set<MenuItemTranslation>();
    public DbSet<HomeSlide> HomeSlides => Set<HomeSlide>();
    public DbSet<HomeSlideTranslation> HomeSlideTranslations => Set<HomeSlideTranslation>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<SiteSettingTranslation> SiteSettingTranslations => Set<SiteSettingTranslation>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<SitePost> SitePosts => Set<SitePost>();
    public DbSet<SitePostTranslation> SitePostTranslations => Set<SitePostTranslation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();
    public DbSet<AdminNotificationRead> AdminNotificationReads => Set<AdminNotificationRead>();
    public DbSet<AppLogEntry> AppLogEntries => Set<AppLogEntry>();
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Category>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(100);
        });

        builder.Entity<CategoryTranslation>(e =>
        {
            e.HasIndex(x => new { x.CategoryId, x.LanguageCode }).IsUnique();
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasOne(x => x.Category).WithMany(x => x.Translations).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Product>(e =>
        {
            e.HasIndex(x => new { x.CategoryId, x.Slug }).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(200);
            e.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.MainImage).WithMany().HasForeignKey(x => x.MainImageMediaFileId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ProductTranslation>(e =>
        {
            e.HasIndex(x => new { x.ProductId, x.LanguageCode }).IsUnique();
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.Property(x => x.Name).HasMaxLength(300);
            e.HasOne(x => x.Product).WithMany(x => x.Translations).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProductVariant>(e =>
        {
            e.HasIndex(x => x.Sku).IsUnique();
            e.Property(x => x.Sku).HasMaxLength(50);
            e.Property(x => x.VariantLabel).HasMaxLength(100);
            e.Property(x => x.ColorDefinitionId).IsRequired(false);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.CompareAtPrice).HasPrecision(18, 2);
            e.HasOne(x => x.Product).WithMany(x => x.Variants).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ColorDefinition).WithMany().HasForeignKey(x => x.ColorDefinitionId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Image).WithMany().HasForeignKey(x => x.ImageMediaFileId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Service>(e =>
        {
            e.ToTable("Services");
            e.HasIndex(x => new { x.CategoryId, x.Slug }).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(200);
            e.HasOne(x => x.Category).WithMany(x => x.Services).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.MainImage).WithMany().HasForeignKey(x => x.MainImageMediaFileId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ServiceTranslation>(e =>
        {
            e.ToTable("ServiceTranslations");
            e.HasIndex(x => new { x.ServiceId, x.LanguageCode }).IsUnique();
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.Property(x => x.Name).HasMaxLength(300);
            e.HasOne(x => x.Service).WithMany(x => x.Translations).HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ServiceVariant>(e =>
        {
            e.ToTable("ServiceVariants");
            e.HasIndex(x => x.Sku).IsUnique();
            e.Property(x => x.Sku).HasMaxLength(50);
            e.Property(x => x.VariantLabel).HasMaxLength(100);
            e.Property(x => x.ColorDefinitionId).IsRequired(false);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.CompareAtPrice).HasPrecision(18, 2);
            e.HasOne(x => x.Service).WithMany(x => x.Variants).HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ColorDefinition).WithMany().HasForeignKey(x => x.ColorDefinitionId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Image).WithMany().HasForeignKey(x => x.ImageMediaFileId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ColorDefinition>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        builder.Entity<ColorDefinitionTranslation>(e =>
        {
            e.HasIndex(x => new { x.ColorDefinitionId, x.LanguageCode }).IsUnique();
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Meaning).HasMaxLength(500);
            e.HasOne(x => x.ColorDefinition)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.ColorDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Tag>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(100);
        });

        builder.Entity<ProductTag>(e =>
        {
            e.HasKey(x => new { x.ProductId, x.TagId });
            e.HasOne(x => x.Product).WithMany(x => x.ProductTags).HasForeignKey(x => x.ProductId);
            e.HasOne(x => x.Tag).WithMany(x => x.ProductTags).HasForeignKey(x => x.TagId);
        });

        builder.Entity<ServiceBooking>(e =>
        {
            e.HasIndex(x => x.BookingNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.PreferredDate);
            e.Property(x => x.BookingNumber).HasMaxLength(32);
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.HasOne(x => x.Service).WithMany(x => x.ServiceBookings).HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ServiceVariant).WithMany().HasForeignKey(x => x.ServiceVariantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Order>(e =>
        {
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CustomerEmail);
            e.HasIndex(x => x.CreatedAt);
            e.Property(x => x.OrderNumber).HasMaxLength(32);
            e.Property(x => x.SubTotal).HasPrecision(18, 2);
            e.Property(x => x.ShippingFee).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.HasOne(x => x.ShippingProvince).WithMany().HasForeignKey(x => x.ShippingProvinceId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<OrderItem>(e =>
        {
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.LineTotal).HasPrecision(18, 2);
            e.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.OrderId).IsUnique();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasOne(x => x.Order).WithOne(x => x.Payment).HasForeignKey<Payment>(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ShippingProvince>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.NameVi).HasMaxLength(100);
        });

        builder.Entity<ShippingRate>(e =>
        {
            e.HasIndex(x => x.ProvinceId).IsUnique();
            e.Property(x => x.Fee).HasPrecision(18, 2);
            e.HasOne(x => x.Province).WithOne(x => x.Rate).HasForeignKey<ShippingRate>(x => x.ProvinceId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Page>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(200);
            e.Property(x => x.ModuleCode).HasMaxLength(50);
        });

        builder.Entity<PageTranslation>(e =>
        {
            e.HasIndex(x => new { x.PageId, x.LanguageCode }).IsUnique();
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.HasOne(x => x.Page).WithMany(x => x.Translations).HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ContentBlock>(e =>
        {
            e.HasOne(x => x.Page).WithMany(x => x.ContentBlocks).HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MediaFile).WithMany().HasForeignKey(x => x.MediaFileId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.SpacingAfterRem).HasPrecision(6, 2);
        });

        builder.Entity<ContentBlockTranslation>(e =>
        {
            e.HasIndex(x => new { x.ContentBlockId, x.LanguageCode }).IsUnique();
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.HasOne(x => x.ContentBlock).WithMany(x => x.Translations).HasForeignKey(x => x.ContentBlockId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Menu>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50);
        });

        builder.Entity<MenuItem>(e =>
        {
            e.Property(x => x.ItemKey).HasMaxLength(50);
            e.HasIndex(x => new { x.MenuId, x.ItemKey });
            e.HasOne(x => x.Menu).WithMany(x => x.Items).HasForeignKey(x => x.MenuId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MenuItemTranslation>(e =>
        {
            e.HasIndex(x => new { x.MenuItemId, x.LanguageCode }).IsUnique();
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.HasOne(x => x.MenuItem).WithMany(x => x.Translations).HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<HomeSlide>(e =>
        {
            e.HasOne(x => x.MediaFile).WithMany().HasForeignKey(x => x.MediaFileId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<HomeSlideTranslation>(e =>
        {
            e.HasIndex(x => new { x.HomeSlideId, x.LanguageCode }).IsUnique();
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.HasOne(x => x.HomeSlide).WithMany(x => x.Translations).HasForeignKey(x => x.HomeSlideId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SiteSetting>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Group).HasMaxLength(50);
        });

        builder.Entity<SiteSettingTranslation>(e =>
        {
            e.HasIndex(x => new { x.SiteSettingId, x.LanguageCode }).IsUnique();
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.HasOne(x => x.SiteSetting).WithMany(x => x.Translations).HasForeignKey(x => x.SiteSettingId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MediaFile>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.StoredPath).HasMaxLength(500);
            e.Property(x => x.ContentType).HasMaxLength(100);
        });

        builder.Entity<Inquiry>(e =>
        {
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
        });

        builder.Entity<JobApplication>(e =>
        {
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentMediaFileId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.SitePost).WithMany(x => x.Applications).HasForeignKey(x => x.SitePostId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.SitePostId);
        });

        builder.Entity<SitePost>(e =>
        {
            e.HasIndex(x => new { x.Kind, x.Slug }).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(200);
            e.HasOne(x => x.CoverImage).WithMany().HasForeignKey(x => x.CoverImageMediaFileId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.Kind, x.IsPublished, x.PublishedAt });
        });

        builder.Entity<SitePostTranslation>(e =>
        {
            e.HasIndex(x => new { x.SitePostId, x.LanguageCode }).IsUnique();
            e.Property(x => x.LanguageCode).HasMaxLength(5);
            e.Property(x => x.Title).HasMaxLength(300);
            e.HasOne(x => x.SitePost).WithMany(x => x.Translations).HasForeignKey(x => x.SitePostId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        builder.Entity<OrderHistory>(e =>
        {
            e.Property(x => x.EventType).HasMaxLength(50);
            e.Property(x => x.ActorType).HasMaxLength(20);
            e.Property(x => x.ActorName).HasMaxLength(200);
            e.Property(x => x.MessageVi).HasMaxLength(500);
            e.HasIndex(x => new { x.OrderId, x.CreatedAt });
            e.HasOne(x => x.Order).WithMany(x => x.Histories).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ServiceBookingHistory>(e =>
        {
            e.Property(x => x.EventType).HasMaxLength(50);
            e.Property(x => x.ActorType).HasMaxLength(20);
            e.Property(x => x.ActorName).HasMaxLength(200);
            e.Property(x => x.MessageVi).HasMaxLength(500);
            e.HasIndex(x => new { x.BookingId, x.CreatedAt });
            e.HasOne(x => x.Booking).WithMany(x => x.Histories).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AdminNotification>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(80);
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Body).HasMaxLength(500);
            e.Property(x => x.EntityType).HasMaxLength(40);
            e.Property(x => x.EntityId).HasMaxLength(50);
            e.Property(x => x.Url).HasMaxLength(300);
            e.HasIndex(x => x.CreatedAt);
        });

        builder.Entity<AdminNotificationRead>(e =>
        {
            e.HasKey(x => new { x.NotificationId, x.UserId });
            e.Property(x => x.UserId).HasMaxLength(450);
            e.HasOne(x => x.Notification).WithMany(x => x.Reads).HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AppLogEntry>(e =>
        {
            e.ToTable("AppLogEntries");
            e.Property(x => x.Module).HasMaxLength(64);
            e.Property(x => x.Category).HasMaxLength(256);
            e.Property(x => x.Message).HasMaxLength(4000);
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => new { x.Module, x.CreatedAtUtc });
            e.HasIndex(x => new { x.Level, x.CreatedAtUtc });
        });

        builder.Entity<EmailOutboxMessage>(e =>
        {
            e.ToTable("EmailOutboxMessages");
            e.Property(x => x.ToAddress).HasMaxLength(320);
            e.Property(x => x.Subject).HasMaxLength(500);
            e.Property(x => x.LastError).HasMaxLength(2000);
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
        });

        builder.Entity<EmailTemplate>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(80);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.PlaceholdersHelp).HasMaxLength(1000);
            e.Property(x => x.SubjectTemplate).HasMaxLength(300);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(200);
        });
    }
}
