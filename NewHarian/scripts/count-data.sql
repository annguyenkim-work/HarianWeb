-- =============================================================================
-- NewHarian — Đếm data (chạy trước / sau khi clean)
-- PostgreSQL. Không sửa gì.
-- =============================================================================

SELECT 'KEEP — catalog / CMS / master' AS group, t, n FROM (
    SELECT 'Categories' AS t, COUNT(*)::bigint AS n FROM "Categories"
    UNION ALL SELECT 'Products', COUNT(*) FROM "Products"
    UNION ALL SELECT 'ProductVariants', COUNT(*) FROM "ProductVariants"
    UNION ALL SELECT 'Services', COUNT(*) FROM "Services"
    UNION ALL SELECT 'ServiceVariants', COUNT(*) FROM "ServiceVariants"
    UNION ALL SELECT 'ColorDefinitions', COUNT(*) FROM "ColorDefinitions"
    UNION ALL SELECT 'Tags', COUNT(*) FROM "Tags"
    UNION ALL SELECT 'Pages', COUNT(*) FROM "Pages"
    UNION ALL SELECT 'Menus', COUNT(*) FROM "Menus"
    UNION ALL SELECT 'HomeSlides', COUNT(*) FROM "HomeSlides"
    UNION ALL SELECT 'SiteSettings', COUNT(*) FROM "SiteSettings"
    UNION ALL SELECT 'ShippingProvinces', COUNT(*) FROM "ShippingProvinces"
    UNION ALL SELECT 'SitePosts (news+jobs)', COUNT(*) FROM "SitePosts"
    UNION ALL SELECT 'MediaFiles', COUNT(*) FROM "MediaFiles"
    UNION ALL SELECT 'EmailTemplates', COUNT(*) FROM "EmailTemplates"
    UNION ALL SELECT 'AspNetUsers', COUNT(*) FROM "AspNetUsers"
) k

UNION ALL

SELECT 'WIPE — transactional / engagement' AS group, t, n FROM (
    SELECT 'Orders' AS t, COUNT(*)::bigint AS n FROM "Orders"
    UNION ALL SELECT 'OrderItems', COUNT(*) FROM "OrderItems"
    UNION ALL SELECT 'Payments', COUNT(*) FROM "Payments"
    UNION ALL SELECT 'OrderHistories', COUNT(*) FROM "OrderHistories"
    UNION ALL SELECT 'ServiceBookings', COUNT(*) FROM "ServiceBookings"
    UNION ALL SELECT 'ServiceBookingHistories', COUNT(*) FROM "ServiceBookingHistories"
    UNION ALL SELECT 'Inquiries', COUNT(*) FROM "Inquiries"
    UNION ALL SELECT 'JobApplications', COUNT(*) FROM "JobApplications"
    UNION ALL SELECT 'Dealers', COUNT(*) FROM "Dealers"
    UNION ALL SELECT 'AdminNotifications', COUNT(*) FROM "AdminNotifications"
    UNION ALL SELECT 'EmailOutboxMessages', COUNT(*) FROM "EmailOutboxMessages"
    UNION ALL SELECT 'AppLogEntries', COUNT(*) FROM "AppLogEntries"
    UNION ALL SELECT 'AuditLogs', COUNT(*) FROM "AuditLogs"
) w

ORDER BY 1, 2;
