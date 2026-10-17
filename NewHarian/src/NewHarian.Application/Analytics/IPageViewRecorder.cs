using NewHarian.Domain.Enums;

namespace NewHarian.Application.Analytics;

public interface IPageViewRecorder
{
    /// <summary>Increments the daily rollup for <paramref name="pageKind"/> and, when set, the product detail rollup.</summary>
    Task RecordAsync(PageKind pageKind, int? productId = null, CancellationToken ct = default);
}
