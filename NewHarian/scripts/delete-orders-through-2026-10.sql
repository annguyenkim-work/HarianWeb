-- =============================================================================
-- NewHarian — Xóa đơn hàng dump: tháng 10/2026 trở về trước (PostgreSQL)
--
-- Mốc: CreatedAt lưu UTC (timestamp without time zone).
-- Hết 31/10/2026 giờ Việt Nam (UTC+7) = 2026-10-31 17:00:00 UTC.
-- Đơn có CreatedAt < mốc này bị xóa. Đơn từ 01/11/2026 00:00 giờ VN được giữ.
--
-- XÓA (chỉ các đơn trong mốc):
--   Orders, OrderItems, Payments, OrderHistories, OrderItemLotAllocations
--   StockMovements gắn đơn đó
--   AdminNotifications / AdminNotificationReads (EntityType = Order)
--   AuditLogs (EntityType Order / Orders)
--   EmailOutboxMessages có OrderNumber trong Subject
--
-- HOÀN KHO trước khi xóa, chỉ đơn đã trừ kho (StockDeductedAt IS NOT NULL):
--   StockLots.QuantityOnHand += số đã phân bổ lô
--   ProductVariants.StockQuantity += toàn bộ số lượng dòng
--
-- GIỮ: đại lý, catalog, CMS, user, đơn từ 01/11/2026 giờ VN.
-- BACKUP database trước khi chạy. Chạy đúng database (không chạy nhầm DB E2E).
-- =============================================================================

-- ----- Xem trước (không ghi). Chạy riêng block này nếu muốn đối chiếu. -----
SELECT COUNT(*) AS orders_to_delete,
       MIN("CreatedAt") AS oldest_utc,
       MAX("CreatedAt") AS newest_utc,
       COUNT(*) FILTER (WHERE "StockDeductedAt" IS NOT NULL) AS deducted_stock
FROM "Orders"
WHERE "CreatedAt" < TIMESTAMP '2026-10-31 17:00:00';

SELECT to_char("CreatedAt" AT TIME ZONE 'UTC' AT TIME ZONE 'Asia/Ho_Chi_Minh', 'YYYY-MM') AS month_vn,
       COUNT(*) AS orders
FROM "Orders"
WHERE "CreatedAt" < TIMESTAMP '2026-10-31 17:00:00'
GROUP BY 1
ORDER BY 1;

SELECT COUNT(*) AS orders_kept
FROM "Orders"
WHERE "CreatedAt" >= TIMESTAMP '2026-10-31 17:00:00';

BEGIN;

CREATE TEMP TABLE dump_orders ON COMMIT DROP AS
SELECT "Id", "OrderNumber"
FROM "Orders"
WHERE "CreatedAt" < TIMESTAMP '2026-10-31 17:00:00';

-- Hoàn tồn lô theo phân bổ FEFO của đơn đã trừ kho.
UPDATE "StockLots" AS lot
SET "QuantityOnHand" = lot."QuantityOnHand" + alloc.qty
FROM (
    SELECT a."StockLotId", SUM(a."Quantity") AS qty
    FROM "OrderItemLotAllocations" AS a
    JOIN "OrderItems" AS oi ON oi."Id" = a."OrderItemId"
    JOIN dump_orders AS d ON d."Id" = oi."OrderId"
    JOIN "Orders" AS o ON o."Id" = oi."OrderId"
    WHERE o."StockDeductedAt" IS NOT NULL
    GROUP BY a."StockLotId"
) AS alloc
WHERE lot."Id" = alloc."StockLotId";

-- Hoàn cache SKU bằng đúng số lượng dòng (gồm phần thiếu lô).
UPDATE "ProductVariants" AS v
SET "StockQuantity" = v."StockQuantity" + line.qty
FROM (
    SELECT oi."ProductVariantId", SUM(oi."Quantity") AS qty
    FROM "OrderItems" AS oi
    JOIN "Orders" AS o ON o."Id" = oi."OrderId"
    JOIN dump_orders AS d ON d."Id" = o."Id"
    WHERE o."StockDeductedAt" IS NOT NULL
    GROUP BY oi."ProductVariantId"
) AS line
WHERE v."Id" = line."ProductVariantId";

DELETE FROM "StockMovements" AS sm
WHERE sm."OrderId" IN (SELECT "Id" FROM dump_orders)
   OR sm."OrderItemId" IN (
        SELECT oi."Id"
        FROM "OrderItems" AS oi
        JOIN dump_orders AS d ON d."Id" = oi."OrderId"
   );

DELETE FROM "AdminNotificationReads" AS r
USING "AdminNotifications" AS n
WHERE r."NotificationId" = n."Id"
  AND n."EntityType" = 'Order'
  AND n."EntityId" IN (SELECT "Id"::text FROM dump_orders);

DELETE FROM "AdminNotifications"
WHERE "EntityType" = 'Order'
  AND "EntityId" IN (SELECT "Id"::text FROM dump_orders);

DELETE FROM "AuditLogs"
WHERE "EntityType" IN ('Order', 'Orders')
  AND "EntityId" IN (SELECT "Id"::text FROM dump_orders);

DELETE FROM "EmailOutboxMessages" AS m
USING dump_orders AS d
WHERE position(d."OrderNumber" in m."Subject") > 0
  AND d."OrderNumber" <> '';

-- Cascade: OrderItems, Payments, OrderHistories, OrderItemLotAllocations.
DELETE FROM "Orders" AS o
USING dump_orders AS d
WHERE o."Id" = d."Id";

COMMIT;
