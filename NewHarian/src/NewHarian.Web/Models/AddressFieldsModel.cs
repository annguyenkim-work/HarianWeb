namespace NewHarian.Web.Models;

public class AddressFieldsModel
{
    public string ProvinceField { get; init; } = "ProvinceCode";
    public string CommuneField { get; init; } = "CommuneCode";
    public string LineField { get; init; } = "Address";
    public string? ProvinceValue { get; init; }
    public string? CommuneValue { get; init; }
    public string? LineValue { get; init; }
}
