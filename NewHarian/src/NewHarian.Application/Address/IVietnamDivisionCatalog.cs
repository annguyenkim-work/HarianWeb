namespace NewHarian.Application.Address;

public record VietnamProvinceDto(string Code, string Name);

public record VietnamCommuneDto(string Code, string ProvinceCode, string Name);

public readonly record struct ResolvedVietnamAddress(
    string ProvinceCode,
    string ProvinceName,
    string CommuneCode,
    string CommuneName);

public interface IVietnamDivisionCatalog
{
    IReadOnlyList<VietnamProvinceDto> Provinces { get; }
    IReadOnlyList<VietnamCommuneDto> CommunesFor(string provinceCode);
    bool TryResolve(string? provinceCode, string? communeCode, out ResolvedVietnamAddress resolved);
}
