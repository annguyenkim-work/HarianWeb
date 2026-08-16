using NewHarian.Application.Validation;

namespace NewHarian.Application.Address;

public static class AddressFormat
{
    public static string Join(string? line, string? commune, string? province)
        => string.Join(", ", new[] { line, commune, province }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public static string? Require(IVietnamDivisionCatalog catalog, string? provinceCode, string? communeCode, string? line)
    {
        if (!GuestValidation.HasLength(line, 5, GuestValidation.AddressMax))
            return "Vui lòng điền địa chỉ (số nhà, đường) từ 5-500 ký tự.";
        if (!catalog.TryResolve(provinceCode, communeCode, out _))
            return "Vui lòng chọn Tỉnh/Thành phố và Xã/Phường.";
        return null;
    }

    public static bool TryBind(
        IVietnamDivisionCatalog catalog,
        string? provinceCode,
        string? communeCode,
        string? line,
        out ResolvedVietnamAddress resolved,
        out string? error)
    {
        error = Require(catalog, provinceCode, communeCode, line);
        if (error is not null)
        {
            resolved = default;
            return false;
        }

        catalog.TryResolve(provinceCode, communeCode, out resolved);
        return true;
    }
}
