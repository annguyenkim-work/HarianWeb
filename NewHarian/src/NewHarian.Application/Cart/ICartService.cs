namespace NewHarian.Application.Cart;

public record CartItemDto(
    int ProductVariantId,
    int ProductId,
    string ProductName,
    string VariantLabel,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    string? ImageUrl)
{
    public decimal LineTotal => UnitPrice * Quantity;
    public string DisplayName =>
        string.IsNullOrWhiteSpace(VariantLabel) ? ProductName : $"{ProductName} - {VariantLabel}";
}

public record CartDto(IReadOnlyList<CartItemDto> Items)
{
    public decimal SubTotal => Items.Sum(i => i.LineTotal);
    public int DistinctCount => Items.Count;
    public int TotalQuantity => Items.Sum(i => i.Quantity);
}

public interface ICartService
{
    CartDto GetCart();
    /// <summary>Distinct line count from session only — no catalog hydrate.</summary>
    int GetDistinctCount();
    (bool Ok, string? Error) Add(int productVariantId, int quantity);
    (bool Ok, string? Error) Update(int productVariantId, int quantity);
    void Remove(int productVariantId);
    void Clear();
}
