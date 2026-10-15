using System.Text.RegularExpressions;
using NewHarian.Application.Address;
using NewHarian.Application.Validation;
using NewHarian.Domain.Enums;

namespace NewHarian.Application.Dealers;

/// <summary>Single source of dealer profile rules (guest register + admin modal).</summary>
public static partial class DealerProfileValidator
{
    public const int BusinessNameMax = 300;
    public const int RegistrationNoMax = 50;
    public const int TitleMax = 100;

    [GeneratedRegex(@"^[\p{L}\d\-/. ]{1,50}$")]
    private static partial Regex RegistrationNoRegex();

    /// <summary>Trims values and drops fields that do not apply to the chosen type / shipping option.</summary>
    public static void Normalize(DealerProfileInput m)
    {
        m.BusinessName = m.BusinessName?.Trim() ?? "";
        m.TaxCode = DealerTaxCode.Normalize(m.TaxCode);
        m.HouseholdRegistrationNo = NullIfEmpty(m.HouseholdRegistrationNo);
        m.RepresentativeName = m.RepresentativeName?.Trim() ?? "";
        m.RepresentativeTitle = NullIfEmpty(m.RepresentativeTitle);
        m.CitizenId = NullIfEmpty(GuestValidation.NormalizeCitizenId(m.CitizenId));
        m.FullName = m.FullName?.Trim() ?? "";
        m.Phone = m.Phone?.Trim() ?? "";
        m.Email = m.Email?.Trim() ?? "";
        m.Address = m.Address?.Trim() ?? "";
        m.InvoiceEmail = NullIfEmpty(m.InvoiceEmail);
        m.Message = NullIfEmpty(m.Message);

        if (m.BusinessType == DealerBusinessType.Company)
            m.HouseholdRegistrationNo = null;
        else if (m.BusinessType == DealerBusinessType.Household)
            m.RepresentativeTitle = null;

        if (m.ShipToHeadOffice)
        {
            m.ShippingProvinceCode = null;
            m.ShippingCommuneCode = null;
            m.ShippingAddress = null;
        }
        else
        {
            m.ShippingAddress = NullIfEmpty(m.ShippingAddress);
        }
    }

    /// <summary>Returns the first validation error (VI) or null. Call <see cref="Normalize"/> first.</summary>
    public static string? Validate(DealerProfileInput m, IVietnamDivisionCatalog catalog)
    {
        var household = m.BusinessType == DealerBusinessType.Household;
        if (m.BusinessType is not (DealerBusinessType.Company or DealerBusinessType.Household))
            return "Vui lòng chọn loại hình kinh doanh.";
        if (!GuestValidation.HasLength(m.BusinessName, 2, BusinessNameMax))
            return household
                ? "Vui lòng điền tên hộ kinh doanh (2-300 ký tự)."
                : "Vui lòng điền tên doanh nghiệp (2-300 ký tự).";
        if (!DealerTaxCode.IsValid(m.BusinessType, m.TaxCode))
            return household
                ? "MST hộ kinh doanh phải gồm 10 hoặc 12 chữ số."
                : "MST doanh nghiệp phải gồm 10 chữ số (chi nhánh dạng 0101234567-001).";
        if (household && (string.IsNullOrWhiteSpace(m.HouseholdRegistrationNo)
                          || !RegistrationNoRegex().IsMatch(m.HouseholdRegistrationNo.Trim())))
            return "Vui lòng điền số GCN đăng ký hộ kinh doanh (tối đa 50 ký tự: chữ, số, - / .).";
        if (!GuestValidation.HasLength(m.RepresentativeName, 2, GuestValidation.NameMax))
            return household
                ? "Vui lòng điền họ tên chủ hộ (2-200 ký tự)."
                : "Vui lòng điền người đại diện theo pháp luật (2-200 ký tự).";
        if (!household && !GuestValidation.HasLength(m.RepresentativeTitle, 2, TitleMax))
            return "Vui lòng điền chức vụ người đại diện (2-100 ký tự).";
        if (household && !GuestValidation.IsCitizenId(m.CitizenId))
            return "CCCD chủ hộ phải gồm 9 hoặc 12 chữ số.";
        if (!household && !string.IsNullOrWhiteSpace(m.CitizenId) && !GuestValidation.IsCitizenId(m.CitizenId))
            return "CCCD người đại diện phải gồm 9 hoặc 12 chữ số.";
        if (!GuestValidation.HasLength(m.FullName, 2, GuestValidation.NameMax))
            return "Vui lòng điền họ tên người liên hệ (2-200 ký tự).";
        if (string.IsNullOrWhiteSpace(m.Phone) || !GuestValidation.IsPhone(m.Phone))
            return "Số điện thoại không hợp lệ.";
        if (!GuestValidation.IsEmail(m.Email))
            return "Email không hợp lệ.";

        var headOfficeErr = AddressFormat.Require(catalog, m.ProvinceCode, m.CommuneCode, m.Address);
        if (headOfficeErr is not null)
            return "Địa chỉ trụ sở: " + headOfficeErr;
        if (!m.ShipToHeadOffice)
        {
            var shipErr = AddressFormat.Require(catalog, m.ShippingProvinceCode, m.ShippingCommuneCode, m.ShippingAddress);
            if (shipErr is not null)
                return "Địa chỉ nhận hàng: " + shipErr;
        }

        if (!string.IsNullOrWhiteSpace(m.InvoiceEmail) && !GuestValidation.IsEmail(m.InvoiceEmail))
            return "Email nhận hóa đơn không hợp lệ.";
        if (!GuestValidation.FitsMax(m.Message, GuestValidation.NotesMax))
            return "Ghi chú tối đa 2000 ký tự.";
        return null;
    }

    private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
