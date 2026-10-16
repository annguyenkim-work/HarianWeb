using NewHarian.Application.Dealers;
using NewHarian.Domain.Enums;
using NewHarian.Infrastructure.Address;

namespace NewHarian.UnitTests.Application.Dealers;

[Trait("Category", "Unit")]
public class DealerProfileValidatorTests
{
    private static readonly Lazy<VietnamDivisionCatalog> SharedCatalog = new(() => new VietnamDivisionCatalog());
    private static VietnamDivisionCatalog Catalog => SharedCatalog.Value;

    private static DealerProfileInput Company()
    {
        var province = Catalog.Provinces.Single(p => p.Code == "01");
        return new DealerProfileInput
        {
            BusinessType = DealerBusinessType.Company,
            BusinessName = "Công ty TNHH Hoa Mai",
            TaxCode = "0101234567",
            RepresentativeName = "Nguyễn Văn A",
            RepresentativeTitle = "Giám đốc",
            FullName = "Trần Thị B",
            Phone = "0912345678",
            Email = "b@hoamai.vn",
            ProvinceCode = province.Code,
            CommuneCode = Catalog.CommunesFor(province.Code)[0].Code,
            Address = "12 Hàng Bài",
        };
    }

    private static DealerProfileInput Household()
    {
        var m = Company();
        m.BusinessType = DealerBusinessType.Household;
        m.BusinessName = "Hộ kinh doanh Hoa Mai";
        m.TaxCode = "001234567890";
        m.HouseholdRegistrationNo = "01A8012345";
        m.RepresentativeTitle = null;
        m.CitizenId = "001234567890";
        return m;
    }

    private static string? Check(DealerProfileInput m)
    {
        DealerProfileValidator.Normalize(m);
        return DealerProfileValidator.Validate(m, Catalog);
    }

    [Fact]
    public void Valid_company_without_citizen_id_passes()
    {
        Assert.Null(Check(Company()));
    }

    [Fact]
    public void Valid_household_passes()
    {
        Assert.Null(Check(Household()));
    }

    [Theory]
    [InlineData("BusinessName", "", "Vui lòng điền tên doanh nghiệp (2-300 ký tự).")]
    [InlineData("TaxCode", "12345", "MST doanh nghiệp phải gồm 10 chữ số (chi nhánh dạng 0101234567-001).")]
    [InlineData("RepresentativeName", "A", "Vui lòng điền người đại diện theo pháp luật (2-200 ký tự).")]
    [InlineData("RepresentativeTitle", "", "Vui lòng điền chức vụ người đại diện (2-100 ký tự).")]
    [InlineData("CitizenId", "123", "CCCD người đại diện phải gồm 9 hoặc 12 chữ số.")]
    [InlineData("FullName", "", "Vui lòng điền họ tên người liên hệ (2-200 ký tự).")]
    [InlineData("Phone", "abc", "Số điện thoại không hợp lệ.")]
    [InlineData("Email", "not-an-email", "Email không hợp lệ.")]
    [InlineData("InvoiceEmail", "bad@", "Email nhận hóa đơn không hợp lệ.")]
    public void Company_field_errors(string field, string value, string expected)
    {
        var m = Company();
        typeof(DealerProfileInput).GetProperty(field)!.SetValue(m, value);
        Assert.Equal(expected, Check(m));
    }

    [Theory]
    [InlineData("BusinessName", "", "Vui lòng điền tên hộ kinh doanh (2-300 ký tự).")]
    [InlineData("TaxCode", "0101234567-001", "MST hộ kinh doanh phải gồm 10 hoặc 12 chữ số.")]
    [InlineData("HouseholdRegistrationNo", "", "Vui lòng điền số GCN đăng ký hộ kinh doanh (tối đa 50 ký tự: chữ, số, - / .).")]
    [InlineData("HouseholdRegistrationNo", "01A<script>", "Vui lòng điền số GCN đăng ký hộ kinh doanh (tối đa 50 ký tự: chữ, số, - / .).")]
    [InlineData("RepresentativeName", "", "Vui lòng điền họ tên chủ hộ (2-200 ký tự).")]
    [InlineData("CitizenId", "", "CCCD chủ hộ phải gồm 9 hoặc 12 chữ số.")]
    public void Household_field_errors(string field, string value, string expected)
    {
        var m = Household();
        typeof(DealerProfileInput).GetProperty(field)!.SetValue(m, value);
        Assert.Equal(expected, Check(m));
    }

    [Fact]
    public void Missing_business_type_is_rejected()
    {
        var m = Company();
        m.BusinessType = 0;
        Assert.Equal("Vui lòng chọn loại hình kinh doanh.", Check(m));
    }

    [Fact]
    public void Head_office_address_error_is_prefixed()
    {
        var m = Company();
        m.Address = "12";
        Assert.StartsWith("Địa chỉ trụ sở: ", Check(m));
    }

    [Fact]
    public void Separate_shipping_address_is_required_when_not_shipping_to_head_office()
    {
        var m = Company();
        m.ShipToHeadOffice = false;
        Assert.StartsWith("Địa chỉ nhận hàng: ", Check(m));

        var ok = Company();
        ok.ShipToHeadOffice = false;
        ok.ShippingProvinceCode = ok.ProvinceCode;
        ok.ShippingCommuneCode = ok.CommuneCode;
        ok.ShippingAddress = "99 Kho Hàng";
        Assert.Null(Check(ok));
        Assert.Equal("99 Kho Hàng", ok.ShippingAddress);
    }

    [Fact]
    public void Message_over_2000_chars_is_rejected()
    {
        var m = Company();
        m.Message = new string('a', 2001);
        Assert.Equal("Ghi chú tối đa 2000 ký tự.", Check(m));
    }

    [Fact]
    public void Normalize_drops_fields_not_applicable_to_type_and_shipping()
    {
        var company = Company();
        company.HouseholdRegistrationNo = "01A8012345";
        company.ShippingAddress = "99 Kho";
        company.ShippingProvinceCode = "01";
        company.TaxCode = " 0101 234567 ";
        DealerProfileValidator.Normalize(company);
        Assert.Null(company.HouseholdRegistrationNo);
        Assert.Null(company.ShippingAddress);
        Assert.Null(company.ShippingProvinceCode);
        Assert.Equal("0101234567", company.TaxCode);

        var household = Household();
        household.RepresentativeTitle = "Chủ hộ";
        household.InvoiceEmail = "   ";
        DealerProfileValidator.Normalize(household);
        Assert.Null(household.RepresentativeTitle);
        Assert.Null(household.InvoiceEmail);
    }
}
