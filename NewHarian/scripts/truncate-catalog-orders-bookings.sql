-- =============================================================================
-- NewHarian — Truncate catalog + commerce transactional data (PostgreSQL)
-- Mục đích: xóa danh mục, sản phẩm/dịch vụ, đơn hàng, booking để setup lại từ đầu.
-- GIỮ NGUYÊN: Users/Roles, CMS Pages, Menus, Slides, Settings, Shipping,
--              Colors, Tags (master), MediaFiles metadata, EmailTemplates, Logs…
--
-- Chạy trên DB đúng môi trường. BACKUP trước khi chạy.
-- =============================================================================

BEGIN;

-- Kiểm tra nhanh trước khi xóa (optional — xem số dòng)
-- SELECT 'Categories' AS t, COUNT(*) FROM "Categories"
-- UNION ALL SELECT 'Products', COUNT(*) FROM "Products"
-- UNION ALL SELECT 'Orders', COUNT(*) FROM "Orders"
-- UNION ALL SELECT 'ServiceBookings', COUNT(*) FROM "ServiceBookings";

TRUNCATE TABLE
    -- Đơn hàng + thanh toán
    "Payments",
    "OrderItems",
    "Orders",
    -- Đặt lịch dịch vụ
    "ServiceBookings",
    -- Catalog (sản phẩm / dịch vụ / danh mục)
    "ProductTags",
    "ProductTranslations",
    "ProductVariants",
    "Products",
    "CategoryTranslations",
    "Categories"
RESTART IDENTITY
CASCADE;

-- Audit trail liên quan đơn / booking / product (optional nhưng nên xóa cho sạch)
DELETE FROM "AuditLogs"
WHERE "EntityType" IN (
    'Order',
    'Orders',
    'ServiceBooking',
    'ServiceBookings',
    'Product',
    'Products',
    'Category',
    'Categories',
    'Payment',
    'Payments'
);

COMMIT;

-- =============================================================================
-- Sau khi chạy:
-- 1) Restart app (nếu đang chạy).
-- 2) Vào Admin tạo lại Danh mục → Sản phẩm / Dịch vụ.
-- 3) File ảnh cũ trong Media library / wwwroot vẫn còn (không xóa MediaFiles).
--    Nếu muốn dọn media orphan, chạy thêm script optional bên dưới.
-- =============================================================================

-- --- OPTIONAL: xóa toàn bộ Tags (không xóa ColorDefinitions) ---
-- BEGIN;
-- TRUNCATE TABLE "ProductTags", "Tags" RESTART IDENTITY CASCADE;
-- COMMIT;

-- --- OPTIONAL: xóa metadata MediaFiles (KHÔNG xóa file vật lý trên disk) ---
-- BEGIN;
-- -- Gỡ tham chiếu ảnh trên CMS / slides / settings trước nếu cần
-- UPDATE "Products" SET "MainImageMediaFileId" = NULL; -- đã truncate Products rồi
-- UPDATE "ContentBlocks" SET "MediaFileId" = NULL;
-- UPDATE "HomeSlides" SET "MediaFileId" = NULL;
-- UPDATE "SitePosts" SET "CoverImageMediaFileId" = NULL;
-- UPDATE "JobApplications" SET "AttachmentMediaFileId" = NULL;
-- TRUNCATE TABLE "MediaFiles" RESTART IDENTITY CASCADE;
-- COMMIT;
