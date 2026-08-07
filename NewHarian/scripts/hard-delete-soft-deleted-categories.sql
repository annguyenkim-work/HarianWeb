-- =============================================================================
-- NewHarian — Hard-delete danh mục soft-deleted + dữ liệu liên quan (PostgreSQL)
--
-- Soft-delete danh mục trong app = Categories."IsActive" = false
--
-- XÓA VĨNH VIỄN:
--   • Categories (IsActive = false) + CategoryTranslations
--   • Products (+ translations / variants / tags) thuộc danh mục đó
--   • Services (+ translations / variants) thuộc danh mục đó
--   • ServiceBookings của các service đó
--   • Orders có OrderItem trỏ ProductId trong scope (+ Payments, OrderItems)
--
-- GIỮ: Users, CMS, MediaFiles, Colors, Tags master, Settings…
-- BACKUP trước khi chạy.
-- =============================================================================

BEGIN;

CREATE TEMP TABLE tmp_inactive_category_ids ON COMMIT DROP AS
SELECT c."Id"
FROM "Categories" c
WHERE c."IsActive" = false;

CREATE TEMP TABLE tmp_product_ids ON COMMIT DROP AS
SELECT p."Id"
FROM "Products" p
WHERE p."CategoryId" IN (SELECT "Id" FROM tmp_inactive_category_ids);

CREATE TEMP TABLE tmp_service_ids ON COMMIT DROP AS
SELECT s."Id"
FROM "Services" s
WHERE s."CategoryId" IN (SELECT "Id" FROM tmp_inactive_category_ids);

CREATE TEMP TABLE tmp_order_ids ON COMMIT DROP AS
SELECT DISTINCT oi."OrderId"
FROM "OrderItems" oi
WHERE oi."ProductId" IN (SELECT "Id" FROM tmp_product_ids);

CREATE TEMP TABLE tmp_booking_ids ON COMMIT DROP AS
SELECT b."Id"
FROM "ServiceBookings" b
WHERE b."ServiceId" IN (SELECT "Id" FROM tmp_service_ids);

SELECT 'inactive_categories' AS scope, COUNT(*)::bigint AS cnt FROM tmp_inactive_category_ids
UNION ALL SELECT 'products', COUNT(*) FROM tmp_product_ids
UNION ALL SELECT 'services', COUNT(*) FROM tmp_service_ids
UNION ALL SELECT 'orders', COUNT(*) FROM tmp_order_ids
UNION ALL SELECT 'service_bookings', COUNT(*) FROM tmp_booking_ids;

-- 1) Bookings (FK Restrict → Services / ServiceVariants)
DELETE FROM "ServiceBookings"
WHERE "Id" IN (SELECT "Id" FROM tmp_booking_ids);

-- 2) Orders linked to physical products
DELETE FROM "Payments"
WHERE "OrderId" IN (SELECT "OrderId" FROM tmp_order_ids);

DELETE FROM "OrderItems"
WHERE "OrderId" IN (SELECT "OrderId" FROM tmp_order_ids);

DELETE FROM "Orders"
WHERE "Id" IN (SELECT "OrderId" FROM tmp_order_ids);

-- 3) Products
DELETE FROM "ProductTags"
WHERE "ProductId" IN (SELECT "Id" FROM tmp_product_ids);

DELETE FROM "ProductTranslations"
WHERE "ProductId" IN (SELECT "Id" FROM tmp_product_ids);

DELETE FROM "ProductVariants"
WHERE "ProductId" IN (SELECT "Id" FROM tmp_product_ids);

DELETE FROM "Products"
WHERE "Id" IN (SELECT "Id" FROM tmp_product_ids);

-- 4) Services
DELETE FROM "ServiceTranslations"
WHERE "ServiceId" IN (SELECT "Id" FROM tmp_service_ids);

DELETE FROM "ServiceVariants"
WHERE "ServiceId" IN (SELECT "Id" FROM tmp_service_ids);

DELETE FROM "Services"
WHERE "Id" IN (SELECT "Id" FROM tmp_service_ids);

-- 5) Categories
DELETE FROM "CategoryTranslations"
WHERE "CategoryId" IN (SELECT "Id" FROM tmp_inactive_category_ids);

DELETE FROM "Categories"
WHERE "Id" IN (SELECT "Id" FROM tmp_inactive_category_ids);

-- 6) Audit
DELETE FROM "AuditLogs"
WHERE ("EntityType" IN ('Category', 'Categories')
       AND "EntityId" IN (SELECT "Id"::text FROM tmp_inactive_category_ids))
   OR ("EntityType" IN ('Product', 'Products')
       AND "EntityId" IN (SELECT "Id"::text FROM tmp_product_ids))
   OR ("EntityType" IN ('Service', 'Services')
       AND "EntityId" IN (SELECT "Id"::text FROM tmp_service_ids))
   OR ("EntityType" IN ('Order', 'Orders')
       AND "EntityId" IN (SELECT "OrderId"::text FROM tmp_order_ids))
   OR ("EntityType" IN ('ServiceBooking', 'ServiceBookings')
       AND "EntityId" IN (SELECT "Id"::text FROM tmp_booking_ids));

COMMIT;
