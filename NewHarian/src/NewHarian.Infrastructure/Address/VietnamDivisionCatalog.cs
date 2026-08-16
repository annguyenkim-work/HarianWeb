using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewHarian.Application.Address;

namespace NewHarian.Infrastructure.Address;

public sealed class VietnamDivisionCatalog : IVietnamDivisionCatalog
{
    private readonly IReadOnlyList<VietnamProvinceDto> _provinces;
    private readonly IReadOnlyDictionary<string, VietnamProvinceDto> _provinceByCode;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<VietnamCommuneDto>> _communesByProvince;
    private readonly IReadOnlyDictionary<(string Province, string Commune), VietnamCommuneDto> _communeIndex;

    public VietnamDivisionCatalog()
    {
        using var stream = typeof(VietnamDivisionCatalog).Assembly
            .GetManifestResourceStream("NewHarian.Infrastructure.Data.vietnam-divisions.json")
            ?? throw new InvalidOperationException("Embedded vietnam-divisions.json is missing.");
        var file = JsonSerializer.Deserialize<FileDto>(stream, JsonOptions)
                   ?? throw new InvalidOperationException("vietnam-divisions.json is empty.");

        _provinces = file.Province
            .Select(p => new VietnamProvinceDto(p.IdProvince.Trim(), p.Name.Trim()))
            .ToList();
        _provinceByCode = _provinces.ToDictionary(p => p.Code, StringComparer.Ordinal);

        var communes = file.Commune
            .Select(c => new VietnamCommuneDto(c.IdCommune.Trim(), c.IdProvince.Trim(), c.Name.Trim()))
            .ToList();

        _communesByProvince = communes
            .GroupBy(c => c.ProvinceCode, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<VietnamCommuneDto>)g.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.Ordinal);

        _communeIndex = communes.ToDictionary(
            c => (c.ProvinceCode, c.Code),
            c => c);
    }

    public IReadOnlyList<VietnamProvinceDto> Provinces => _provinces;

    public IReadOnlyList<VietnamCommuneDto> CommunesFor(string provinceCode)
        => _communesByProvince.TryGetValue(provinceCode?.Trim() ?? "", out var list)
            ? list
            : Array.Empty<VietnamCommuneDto>();

    public bool TryResolve(string? provinceCode, string? communeCode, out ResolvedVietnamAddress resolved)
    {
        resolved = default;
        var pCode = provinceCode?.Trim() ?? "";
        var cCode = communeCode?.Trim() ?? "";
        if (!_provinceByCode.TryGetValue(pCode, out var province))
            return false;
        if (!_communeIndex.TryGetValue((pCode, cCode), out var commune))
            return false;
        resolved = new ResolvedVietnamAddress(province.Code, province.Name, commune.Code, commune.Name);
        return true;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class FileDto
    {
        [JsonPropertyName("province")]
        public List<ProvinceRow> Province { get; set; } = [];

        [JsonPropertyName("commune")]
        public List<CommuneRow> Commune { get; set; } = [];
    }

    private sealed class ProvinceRow
    {
        public string IdProvince { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private sealed class CommuneRow
    {
        public string IdProvince { get; set; } = "";
        public string IdCommune { get; set; } = "";
        public string Name { get; set; } = "";
    }
}
