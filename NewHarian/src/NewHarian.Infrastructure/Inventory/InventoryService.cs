using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NewHarian.Application.Abstractions;
using NewHarian.Application.Admin;
using NewHarian.Application.Email;
using NewHarian.Application.Inventory;
using NewHarian.Application.Settings;
using NewHarian.Domain.Entities;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Email;
using NewHarian.Infrastructure.Persistence;

namespace NewHarian.Infrastructure.Inventory;

public sealed class InventoryService(
    AppDbContext db,
    IEmailSender email,
    IEmailTemplateService emailTemplates,
    ISiteSettingsService siteSettings,
    ILogger<InventoryService> logger) : IInventoryService, IOrderStockService
{
    public const int DefaultLowStockThreshold = 5;

    public async Task<IReadOnlyList<WarehouseLocationListItemDto>> ListLocationsAsync(CancellationToken ct = default)
    {
        var locs = await db.WarehouseLocations.AsNoTracking()
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.SortOrder,
                x.IsActive,
                LotCount = x.Lots.Count(l => l.QuantityOnHand != 0),
                TotalQuantity = x.Lots.Sum(l => (int?)l.QuantityOnHand) ?? 0
            })
            .ToListAsync(ct);

        return locs.Select(x => new WarehouseLocationListItemDto(
            x.Id, x.Code, x.Name, x.SortOrder, x.IsActive, x.LotCount, x.TotalQuantity)).ToList();
    }

    public async Task<WarehouseLocationSaveRequest?> GetLocationForEditAsync(int id, CancellationToken ct = default)
    {
        var x = await db.WarehouseLocations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (x is null) return null;
        return new WarehouseLocationSaveRequest
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            SortOrder = x.SortOrder,
            IsActive = x.IsActive
        };
    }

    public async Task<(bool Ok, string? Error)> SaveLocationAsync(WarehouseLocationSaveRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("SaveWarehouseLocation Start Id={Id} Code={Code}", request.Id, request.Code);
        try
        {
            var code = (request.Code ?? "").Trim();
            var name = (request.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                logger.LogWarning("SaveWarehouseLocation Done rejected Error={Error}", "Mã vị trí bắt buộc.");
                return (false, "Mã vị trí bắt buộc.");
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                logger.LogWarning("SaveWarehouseLocation Done rejected Error={Error}", "Tên vị trí bắt buộc.");
                return (false, "Tên vị trí bắt buộc.");
            }

            var codeTaken = await db.WarehouseLocations.AnyAsync(
                l => l.Code == code && (!request.Id.HasValue || l.Id != request.Id.Value), ct);
            if (codeTaken)
            {
                logger.LogWarning("SaveWarehouseLocation Done rejected Error={Error}", "Mã vị trí đã tồn tại.");
                return (false, "Mã vị trí đã tồn tại.");
            }

            WarehouseLocation entity;
            if (request.Id is int id)
            {
                entity = await db.WarehouseLocations.FirstOrDefaultAsync(l => l.Id == id, ct)
                         ?? throw new InvalidOperationException("Location not found");
            }
            else
            {
                if (request.SortOrder <= 0)
                {
                    var max = await db.WarehouseLocations.Select(l => (int?)l.SortOrder).MaxAsync(ct) ?? 0;
                    request.SortOrder = max + 1;
                }
                entity = new WarehouseLocation();
                db.WarehouseLocations.Add(entity);
            }

            entity.Code = code;
            entity.Name = name;
            entity.SortOrder = request.SortOrder;
            entity.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("SaveWarehouseLocation Done Id={Id} Code={Code}", entity.Id, entity.Code);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveWarehouseLocation Error Id={Id}", request.Id);
            throw;
        }
    }

    public async Task<(IReadOnlyList<StockLotListItemDto> Items, int Total)> ListLotsAsync(
        string? q,
        int? locationId,
        bool? expiringSoon,
        bool? outOfStock,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = db.StockLots.AsNoTracking()
            .Include(l => l.WarehouseLocation)
            .Include(l => l.ProductVariant).ThenInclude(v => v.Product).ThenInclude(p => p.Translations)
            .AsQueryable();

        if (locationId is int locId)
            query = query.Where(l => l.WarehouseLocationId == locId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(l =>
                l.ProductVariant.Sku.ToLower().Contains(term)
                || (l.LotCode != null && l.LotCode.ToLower().Contains(term))
                || l.ProductVariant.Product.Translations.Any(t => t.Name.ToLower().Contains(term)));
        }

        if (outOfStock == true)
            query = query.Where(l => l.QuantityOnHand <= 0);
        else if (outOfStock == false)
            query = query.Where(l => l.QuantityOnHand > 0);

        if (expiringSoon == true)
        {
            var invSettings = await siteSettings.GetInventoryAsync(ct);
            var until = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(invSettings.ExpiringWithinDays);
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            query = query.Where(l => l.ExpiryDate >= today && l.ExpiryDate <= until && l.QuantityOnHand > 0);
        }

        var total = await query.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var rows = await query
            .OrderBy(l => l.ExpiryDate).ThenBy(l => l.ReceivedAt)
            .Skip(skip).Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(l =>
        {
            var name = l.ProductVariant.Product.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name
                       ?? l.ProductVariant.Product.Translations.FirstOrDefault()?.Name
                       ?? $"#{l.ProductVariant.ProductId}";
            return new StockLotListItemDto(
                l.Id,
                l.ProductVariant.Sku,
                name,
                l.ProductVariant.VariantLabel,
                l.WarehouseLocation.Code,
                l.WarehouseLocation.Name,
                l.LotCode,
                l.ExpiryDate,
                l.UnitCost,
                l.QuantityOnHand,
                l.QuantityOnHand * l.UnitCost,
                l.ReceivedAt);
        }).ToList();

        return (items, total);
    }

    public async Task<IReadOnlyList<StockLotListItemDto>> ListLotsByLocationAsync(int locationId, CancellationToken ct = default)
    {
        var (items, _) = await ListLotsAsync(null, locationId, null, null, 1, 500, ct);
        return items.OrderBy(l => l.Sku).ThenBy(l => l.ExpiryDate).ToList();
    }

    public async Task<(IReadOnlyList<StockMovementListItemDto> Items, int Total)> ListMovementsAsync(
        StockHistoryFilterKind kind,
        DateOnly? from,
        DateOnly? to,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        (from, to) = AdminListQuery.NormalizeDateRange(from, to);
        var query = db.StockMovements.AsNoTracking()
            .Include(m => m.StockLot).ThenInclude(l => l.ProductVariant).ThenInclude(v => v.Product).ThenInclude(p => p.Translations)
            .Include(m => m.StockLot).ThenInclude(l => l.WarehouseLocation)
            .AsQueryable();

        query = kind switch
        {
            StockHistoryFilterKind.In => query.Where(m => m.Type == StockMovementType.Receipt),
            StockHistoryFilterKind.Out => query.Where(m => m.Type == StockMovementType.Issue),
            _ => query
        };

        if (from is DateOnly f)
        {
            var utcFrom = DateTime.SpecifyKind(f.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(m => m.CreatedAt >= utcFrom);
        }
        if (to is DateOnly t)
        {
            var utcToExclusive = DateTime.SpecifyKind(t.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(m => m.CreatedAt < utcToExclusive);
        }

        var total = await query.CountAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var rows = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip).Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(m =>
        {
            var name = m.StockLot.ProductVariant.Product.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name
                       ?? m.StockLot.ProductVariant.Product.Translations.FirstOrDefault()?.Name
                       ?? "";
            return new StockMovementListItemDto(
                m.Id,
                m.CreatedAt,
                m.Type,
                TypeLabelVi(m.Type),
                name,
                FormatLocationLabel(m.StockLot),
                m.Quantity,
                m.Notes,
                m.ActorName);
        }).ToList();

        return (items, total);
    }

    private static string TypeLabelVi(StockMovementType type) => type switch
    {
        StockMovementType.Receipt => "Nhập",
        StockMovementType.Issue => "Xuất",
        StockMovementType.Adjust => "Điều chỉnh",
        StockMovementType.Restore => "Hoàn kho",
        _ => type.ToString()
    };

    private static string FormatLocationLabel(StockLot lot)
    {
        var lo = string.IsNullOrWhiteSpace(lot.LotCode) ? lot.Id.ToString() : lot.LotCode.Trim();
        return $"Khu {lot.WarehouseLocation.Code} Lô {lo}";
    }

    public async Task<StockLotListItemDto?> GetLotAsync(int lotId, CancellationToken ct = default)
    {
        var l = await db.StockLots.AsNoTracking()
            .Include(x => x.WarehouseLocation)
            .Include(x => x.ProductVariant).ThenInclude(v => v.Product).ThenInclude(p => p.Translations)
            .FirstOrDefaultAsync(x => x.Id == lotId, ct);
        if (l is null) return null;
        var name = l.ProductVariant.Product.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name
                   ?? l.ProductVariant.Product.Translations.FirstOrDefault()?.Name
                   ?? $"#{l.ProductVariant.ProductId}";
        return new StockLotListItemDto(
            l.Id, l.ProductVariant.Sku, name, l.ProductVariant.VariantLabel,
            l.WarehouseLocation.Code, l.WarehouseLocation.Name, l.LotCode,
            l.ExpiryDate, l.UnitCost, l.QuantityOnHand, l.QuantityOnHand * l.UnitCost, l.ReceivedAt);
    }

    public async Task<(bool Ok, string? Error, int? LotId)> ReceiveLotAsync(
        StockLotReceiveRequest request,
        string? actorUserId,
        string? actorName,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "ReceiveStockLot Start VariantId={VariantId} LocationId={LocationId} Qty={Qty}",
            request.ProductVariantId, request.WarehouseLocationId, request.Quantity);
        try
        {
            if (request.Quantity <= 0)
            {
                logger.LogWarning("ReceiveStockLot Done rejected Error={Error}", "Số lượng phải > 0.");
                return (false, "Số lượng phải > 0.", null);
            }
            if (request.UnitCost < 0)
            {
                logger.LogWarning("ReceiveStockLot Done rejected Error={Error}", "Giá vốn không hợp lệ.");
                return (false, "Giá vốn không hợp lệ.", null);
            }

            var variant = await db.ProductVariants.FirstOrDefaultAsync(v => v.Id == request.ProductVariantId, ct);
            if (variant is null || request.ProductVariantId <= 0)
            {
                logger.LogWarning("ReceiveStockLot Done rejected Error={Error}", "Chọn sản phẩm từ danh sách gợi ý.");
                return (false, "Chọn sản phẩm từ danh sách gợi ý.", null);
            }

            var location = await db.WarehouseLocations.FirstOrDefaultAsync(
                l => l.Id == request.WarehouseLocationId && l.IsActive, ct);
            if (location is null)
            {
                logger.LogWarning("ReceiveStockLot Done rejected Error={Error}", "Vị trí không hợp lệ.");
                return (false, "Vị trí không hợp lệ.", null);
            }

            var lot = new StockLot
            {
                ProductVariantId = variant.Id,
                WarehouseLocationId = location.Id,
                LotCode = string.IsNullOrWhiteSpace(request.LotCode) ? null : request.LotCode.Trim(),
                ExpiryDate = request.ExpiryDate,
                UnitCost = request.UnitCost,
                QuantityOnHand = request.Quantity,
                ReceivedAt = DateTime.UtcNow,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            };
            db.StockLots.Add(lot);
            await db.SaveChangesAsync(ct);

            db.StockMovements.Add(new StockMovement
            {
                StockLotId = lot.Id,
                Type = StockMovementType.Receipt,
                Quantity = request.Quantity,
                ActorUserId = actorUserId,
                ActorName = actorName,
                Notes = lot.Notes,
                CreatedAt = DateTime.UtcNow
            });
            variant.StockQuantity += request.Quantity;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("ReceiveStockLot Done LotId={LotId} VariantId={VariantId}", lot.Id, variant.Id);
            return (true, null, lot.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ReceiveStockLot Error VariantId={VariantId}", request.ProductVariantId);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> AdjustLotAsync(
        StockLotAdjustRequest request,
        string? actorUserId,
        string? actorName,
        CancellationToken ct = default)
    {
        logger.LogInformation("AdjustStockLot Start LotId={LotId} Qty={Qty}", request.LotId, request.QuantityOnHand);
        try
        {
            var lot = await db.StockLots.Include(l => l.ProductVariant)
                .FirstOrDefaultAsync(l => l.Id == request.LotId, ct);
            if (lot is null)
            {
                logger.LogWarning("AdjustStockLot Done rejected Error={Error}", "Không tìm thấy lô.");
                return (false, "Không tìm thấy lô.");
            }

            var delta = request.QuantityOnHand - lot.QuantityOnHand;
            if (delta == 0)
            {
                logger.LogInformation("AdjustStockLot Done LotId={LotId} (no change)", lot.Id);
                return (true, null);
            }

            lot.QuantityOnHand = request.QuantityOnHand;
            lot.ProductVariant.StockQuantity += delta;
            db.StockMovements.Add(new StockMovement
            {
                StockLotId = lot.Id,
                Type = StockMovementType.Adjust,
                Quantity = delta,
                ActorUserId = actorUserId,
                ActorName = actorName,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("AdjustStockLot Done LotId={LotId} Delta={Delta}", lot.Id, delta);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AdjustStockLot Error LotId={LotId}", request.LotId);
            throw;
        }
    }

    public Task<OrderStockPickPlanDto?> PreviewPickPlanAsync(int orderId, CancellationToken ct = default)
        => BuildPickPlanAsync(orderId, useExistingAllocations: false, ct);

    public Task<OrderStockPickPlanDto?> GetAllocationsAsync(int orderId, CancellationToken ct = default)
        => BuildPickPlanAsync(orderId, useExistingAllocations: true, ct);

    public async Task<OrderStockPickPlanDto> PreviewPickBySkusAsync(
        IReadOnlyList<StockPickSkuLineRequest> lines,
        CancellationToken ct = default)
    {
        var result = new List<OrderItemPickPlanDto>();
        var normalized = (lines ?? [])
            .Where(l => !string.IsNullOrWhiteSpace(l.Sku) && l.Quantity > 0)
            .Select(l => (Sku: l.Sku.Trim(), l.Quantity))
            .ToList();

        if (normalized.Count == 0)
            return new OrderStockPickPlanDto(0, "", false, result);

        var skuLower = normalized.Select(l => l.Sku.ToLowerInvariant()).Distinct().ToList();
        var variants = await db.ProductVariants.AsNoTracking()
            .Include(v => v.Product).ThenInclude(p => p.Translations)
            .Where(v => v.IsActive && skuLower.Contains(v.Sku.ToLower()))
            .ToListAsync(ct);
        var bySku = variants
            .GroupBy(v => v.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var lineIndex = 0;
        foreach (var (sku, qty) in normalized)
        {
            lineIndex++;
            if (!bySku.TryGetValue(sku, out var variant))
            {
                result.Add(new OrderItemPickPlanDto(
                    lineIndex, sku, "(SKU không tìm thấy)", "", qty, 0, qty, []));
                continue;
            }

            var name = variant.Product.Translations
                .OrderBy(t => t.LanguageCode == "vi" ? 0 : 1)
                .Select(t => t.Name)
                .FirstOrDefault() ?? variant.Product.Slug;

            var remaining = qty;
            var lots = await db.StockLots.AsNoTracking()
                .Include(l => l.WarehouseLocation)
                .Where(l => l.ProductVariantId == variant.Id && l.QuantityOnHand > 0)
                .OrderBy(l => l.ExpiryDate).ThenBy(l => l.ReceivedAt)
                .ToListAsync(ct);

            var suggestions = new List<StockLotPickSuggestionDto>();
            foreach (var lot in lots)
            {
                if (remaining <= 0) break;
                var take = Math.Min(lot.QuantityOnHand, remaining);
                if (take <= 0) continue;
                suggestions.Add(new StockLotPickSuggestionDto(
                    lot.Id, lot.LotCode, lot.WarehouseLocation.Code, lot.WarehouseLocation.Name,
                    lot.ExpiryDate, lot.QuantityOnHand, take));
                remaining -= take;
            }

            result.Add(new OrderItemPickPlanDto(
                lineIndex, variant.Sku, name, variant.VariantLabel,
                qty, qty - remaining, remaining, suggestions));
        }

        return new OrderStockPickPlanDto(0, "", false, result);
    }

    public async Task<(bool Ok, string? Error, bool HadShortfall)> DeductForOrderAsync(
        int orderId,
        string? actorUserId,
        string? actorName,
        CancellationToken ct = default)
    {
        logger.LogInformation("DeductStockForOrder Start OrderId={OrderId}", orderId);
        try
        {
            var order = await db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is null)
            {
                logger.LogWarning("DeductStockForOrder Done rejected OrderId={OrderId} Error={Error}", orderId, "Không tìm thấy đơn.");
                return (false, "Không tìm thấy đơn.", false);
            }

            if (order.StockDeductedAt is not null)
            {
                logger.LogInformation("DeductStockForOrder Done OrderId={OrderId} (already deducted)", orderId);
                return (true, null, false);
            }

            var shortfallLines = new List<string>();
            foreach (var item in order.Items)
            {
                var remaining = item.Quantity;
                var lots = await db.StockLots
                    .Include(l => l.WarehouseLocation)
                    .Where(l => l.ProductVariantId == item.ProductVariantId && l.QuantityOnHand > 0)
                    .OrderBy(l => l.ExpiryDate).ThenBy(l => l.ReceivedAt)
                    .ToListAsync(ct);

                foreach (var lot in lots)
                {
                    if (remaining <= 0) break;
                    var take = Math.Min(lot.QuantityOnHand, remaining);
                    if (take <= 0) continue;

                    lot.QuantityOnHand -= take;
                    remaining -= take;

                    db.OrderItemLotAllocations.Add(new OrderItemLotAllocation
                    {
                        OrderItemId = item.Id,
                        StockLotId = lot.Id,
                        Quantity = take
                    });
                    db.StockMovements.Add(new StockMovement
                    {
                        StockLotId = lot.Id,
                        Type = StockMovementType.Issue,
                        Quantity = -take,
                        OrderId = order.Id,
                        OrderItemId = item.Id,
                        ActorUserId = actorUserId,
                        ActorName = actorName,
                        Notes = $"Xuất cho đơn hàng {order.OrderNumber}",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                var variant = await db.ProductVariants.FirstAsync(v => v.Id == item.ProductVariantId, ct);
                variant.StockQuantity -= item.Quantity;

                if (remaining > 0)
                    shortfallLines.Add($"{item.Sku}: thiếu {remaining}/{item.Quantity}");
            }

            order.StockDeductedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var hadShortfall = shortfallLines.Count > 0;
            if (hadShortfall)
            {
                logger.LogWarning(
                    "Stock Oversell OrderId={OrderId} OrderNumber={OrderNumber} Details={Details}",
                    order.Id, order.OrderNumber, string.Join("; ", shortfallLines));
                await NotifyShortfallAsync(order, shortfallLines, ct);
            }

            logger.LogInformation(
                "DeductStockForOrder Done OrderId={OrderId} Shortfall={Shortfall}",
                orderId, hadShortfall);
            return (true, null, hadShortfall);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeductStockForOrder Error OrderId={OrderId}", orderId);
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> RestoreForOrderAsync(
        int orderId,
        string? actorUserId,
        string? actorName,
        CancellationToken ct = default)
    {
        logger.LogInformation("RestoreStockForOrder Start OrderId={OrderId}", orderId);
        try
        {
            var order = await db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is null)
            {
                logger.LogWarning("RestoreStockForOrder Done rejected OrderId={OrderId} Error={Error}", orderId, "Không tìm thấy đơn.");
                return (false, "Không tìm thấy đơn.");
            }

            if (order.StockDeductedAt is null)
            {
                logger.LogInformation("RestoreStockForOrder Done OrderId={OrderId} (nothing to restore)", orderId);
                return (true, null);
            }

            var itemIds = order.Items.Select(i => i.Id).ToList();
            var allocations = await db.OrderItemLotAllocations
                .Include(a => a.StockLot).ThenInclude(l => l.ProductVariant)
                .Where(a => itemIds.Contains(a.OrderItemId))
                .ToListAsync(ct);

            foreach (var alloc in allocations)
            {
                alloc.StockLot.QuantityOnHand += alloc.Quantity;
                alloc.StockLot.ProductVariant.StockQuantity += alloc.Quantity;
                db.StockMovements.Add(new StockMovement
                {
                    StockLotId = alloc.StockLotId,
                    Type = StockMovementType.Restore,
                    Quantity = alloc.Quantity,
                    OrderId = order.Id,
                    OrderItemId = alloc.OrderItemId,
                    ActorUserId = actorUserId,
                    ActorName = actorName,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Shortfall portion was deducted from cache only — restore remaining cache gap
            foreach (var item in order.Items)
            {
                var allocated = allocations.Where(a => a.OrderItemId == item.Id).Sum(a => a.Quantity);
                var gap = item.Quantity - allocated;
                if (gap > 0)
                {
                    var variant = await db.ProductVariants.FirstAsync(v => v.Id == item.ProductVariantId, ct);
                    variant.StockQuantity += gap;
                }
            }

            db.OrderItemLotAllocations.RemoveRange(allocations);
            order.StockDeductedAt = null;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("RestoreStockForOrder Done OrderId={OrderId}", orderId);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RestoreStockForOrder Error OrderId={OrderId}", orderId);
            throw;
        }
    }

    public async Task<InventorySummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var totalValue = await db.StockLots.AsNoTracking()
            .Where(l => l.QuantityOnHand > 0)
            .SumAsync(l => (decimal?)(l.QuantityOnHand * l.UnitCost), ct) ?? 0m;

        var invSettings = await siteSettings.GetInventoryAsync(ct);
        var threshold = invSettings.LowStockThreshold;
        var expiringDays = invSettings.ExpiringWithinDays;
        var until = today.AddDays(expiringDays);

        var low = await db.ProductVariants.AsNoTracking()
            .Where(v => v.IsActive && v.StockQuantity <= threshold)
            .CountAsync(ct);

        var expiring = await db.StockLots.AsNoTracking()
            .CountAsync(l => l.QuantityOnHand > 0 && l.ExpiryDate >= today && l.ExpiryDate <= until, ct);
        var expired = await db.StockLots.AsNoTracking()
            .CountAsync(l => l.QuantityOnHand > 0 && l.ExpiryDate < today, ct);

        return new InventorySummaryDto(totalValue, low, threshold, expiring, expiringDays, expired);
    }

    private async Task<OrderStockPickPlanDto?> BuildPickPlanAsync(int orderId, bool useExistingAllocations, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return null;

        var lines = new List<OrderItemPickPlanDto>();
        if (useExistingAllocations && order.StockDeductedAt is not null)
        {
            var itemIds = order.Items.Select(i => i.Id).ToList();
            var allocs = await db.OrderItemLotAllocations.AsNoTracking()
                .Include(a => a.StockLot).ThenInclude(l => l.WarehouseLocation)
                .Where(a => itemIds.Contains(a.OrderItemId))
                .ToListAsync(ct);

            foreach (var item in order.Items)
            {
                var itemAllocs = allocs.Where(a => a.OrderItemId == item.Id).ToList();
                var allocated = itemAllocs.Sum(a => a.Quantity);
                lines.Add(new OrderItemPickPlanDto(
                    item.Id,
                    item.Sku,
                    item.ProductName,
                    item.VariantLabel,
                    item.Quantity,
                    allocated,
                    Math.Max(0, item.Quantity - allocated),
                    itemAllocs.Select(a => new StockLotPickSuggestionDto(
                        a.StockLotId,
                        a.StockLot.LotCode,
                        a.StockLot.WarehouseLocation.Code,
                        a.StockLot.WarehouseLocation.Name,
                        a.StockLot.ExpiryDate,
                        a.StockLot.QuantityOnHand,
                        a.Quantity)).ToList()));
            }
        }
        else
        {
            foreach (var item in order.Items)
            {
                var remaining = item.Quantity;
                var lots = await db.StockLots.AsNoTracking()
                    .Include(l => l.WarehouseLocation)
                    .Where(l => l.ProductVariantId == item.ProductVariantId && l.QuantityOnHand > 0)
                    .OrderBy(l => l.ExpiryDate).ThenBy(l => l.ReceivedAt)
                    .ToListAsync(ct);

                var suggestions = new List<StockLotPickSuggestionDto>();
                foreach (var lot in lots)
                {
                    if (remaining <= 0) break;
                    var take = Math.Min(lot.QuantityOnHand, remaining);
                    if (take <= 0) continue;
                    suggestions.Add(new StockLotPickSuggestionDto(
                        lot.Id, lot.LotCode, lot.WarehouseLocation.Code, lot.WarehouseLocation.Name,
                        lot.ExpiryDate, lot.QuantityOnHand, take));
                    remaining -= take;
                }

                lines.Add(new OrderItemPickPlanDto(
                    item.Id, item.Sku, item.ProductName, item.VariantLabel,
                    item.Quantity, item.Quantity - remaining, remaining, suggestions));
            }
        }

        return new OrderStockPickPlanDto(order.Id, order.OrderNumber, order.StockDeductedAt is not null, lines);
    }

    private async Task NotifyShortfallAsync(Order order, IReadOnlyList<string> shortfallLines, CancellationToken ct)
    {
        try
        {
            var staff = await GetSettingAsync("notifications.order_email", ct)
                        ?? await GetSettingAsync("company.email", ct)
                        ?? "info@harian.local";
            var details = string.Join("<br/>", shortfallLines.Select(EmailTemplateService.Enc));
            var (subject, body) = await emailTemplates.RenderAsync(EmailTemplateCodes.StockShortfallStaff, new Dictionary<string, string?>
            {
                ["OrderNumber"] = EmailTemplateService.Enc(order.OrderNumber),
                ["DetailsHtml"] = details
            }, ct);
            await email.SendAsync(staff, subject, body, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stock shortfall email failed for Order={OrderNumber}", order.OrderNumber);
        }
    }

    private async Task<string?> GetSettingAsync(string key, CancellationToken ct)
    {
        return await db.SiteSettings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
    }
}
