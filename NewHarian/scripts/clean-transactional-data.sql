-- =============================================================================
-- NewHarian — Clean transactional / test data (PostgreSQL)
--
-- GIỮ:
--   Catalog (Categories, Products, Services, Variants, Colors, Tags)
--   CMS (Pages, Blocks, Menus, Slides, SiteSettings)
--   ShippingProvinces / Rates
--   SitePosts (tin tức + bài tuyển dụng) + translations + cover images
--   MediaFiles metadata (ảnh SP/CMS/posts)
--   Users / Roles, EmailTemplates
--
-- XÓA:
--   Orders + items + payments + order histories
--   ServiceBookings + booking histories
--   Inquiries
--   JobApplications (hồ sơ nộp — KHÔNG xóa bài đăng tuyển)
--   Dealers
--   AdminNotifications (+ reads)
--   EmailOutboxMessages
--   AppLogEntries
--   AuditLogs liên quan các entity trên
--
-- KHÔNG chạy script truncate-catalog-orders-bookings.sql (script đó xóa cả SP).
-- BACKUP DB trước. Chạy trên đúng môi trường test.
--
-- Sau khi chạy:
--   1) Restart app → DbSeeder chỉ bổ sung setting/template thiếu, KHÔNG seed lại catalog.
--   2) (Tuỳ chọn) xóa file CV vật lý: App_Data/private/applications/
-- =============================================================================

BEGIN;

TRUNCATE TABLE
    "AdminNotificationReads",
    "AdminNotifications",
    "EmailOutboxMessages",
    "OrderHistories",
    "Payments",
    "OrderItems",
    "Orders",
    "ServiceBookingHistories",
    "ServiceBookings",
    "Inquiries",
    "JobApplications",
    "Dealers",
    "AppLogEntries"
RESTART IDENTITY
CASCADE;

DELETE FROM "AuditLogs"
WHERE "EntityType" IN (
    'Order', 'Orders',
    'Payment', 'Payments',
    'ServiceBooking', 'ServiceBookings',
    'Inquiry', 'Inquiries',
    'JobApplication', 'Applications',
    'Dealer', 'Dealers'
);

COMMIT;

-- --- OPTIONAL: dọn MediaFiles của CV ứng tuyển (không đụng ảnh catalog/CMS) ---
-- BEGIN;
-- DELETE FROM "MediaFiles"
-- WHERE "StoredPath" ILIKE '%/private/applications/%'
--    OR "StoredPath" ILIKE '%applications%';
-- COMMIT;
