using NewHarian.Application.DealerDiscounts;

namespace NewHarian.Web.Models;

public record DiscountGroupTableModel(
    DealerDiscountGroupDto Group,
    IReadOnlyDictionary<(int TierId, int ProductId), decimal>? Overrides = null);
