using NewHarian.Application.Abstractions;
using NewHarian.Domain.Enums;
using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P1")]
public sealed class DealerRegistrationJourneyTests(E2EFixture fx)
{
    [E2EFact]
    public async Task Company_registers_with_license_and_shipping_address_then_sales_manager_approves()
    {
        var suffix = E2EDealerData.Suffix();
        var taxCode = E2EDealerData.CompanyTaxCode();
        var businessName = $"Công ty TNHH E2E {suffix}";

        await using (var guest = await fx.NewGuestContextAsync())
        {
            await fx.RunAsync(guest, "Company_registers", async page =>
            {
                var register = new DealerRegisterPage(page);
                await register.GotoAsync();
                await register.ChooseTypeAsync("Company");
                await Expect(register.Form.Locator("[name=HouseholdRegistrationNo]")).ToBeHiddenAsync();
                await register.FillAsync("BusinessName", businessName);
                await register.FillAsync("TaxCode", taxCode);
                await register.HeadOfficeAsync(E2EDealerData.ProvinceCode, E2EDealerData.CommuneCode, "12 Đường Trụ Sở");
                await register.AttachLicenseAsync("gpkd.pdf", "%PDF-1.4 e2e business license"u8.ToArray(), "application/pdf");
                await register.FillAsync("RepresentativeName", "Nguyễn Văn Đại Diện");
                await register.FillAsync("RepresentativeTitle", "Giám đốc");
                await register.FillAsync("FullName", "Trần Liên Hệ");
                await register.FillAsync("Phone", "0912345678");
                await register.FillAsync("Email", $"dk-{suffix}@e2e.local");
                await register.SeparateShippingAsync(E2EDealerData.ProvinceCode, E2EDealerData.CommuneCode, "99 Kho Nhận Hàng");
                await register.FillAsync("InvoiceEmail", $"hoadon-{suffix}@e2e.local");
                await register.ContinueAsync();

                await Expect(register.Confirm).ToContainTextAsync(businessName);
                await Expect(register.Confirm).ToContainTextAsync(taxCode);
                await Expect(register.Confirm).ToContainTextAsync("gpkd.pdf");
                await Expect(register.Confirm).ToContainTextAsync("99 Kho Nhận Hàng");
                await register.SendAsync();
            });
        }

        var stored = await E2EDealerData.DealerByTaxCodeAsync(fx.Services, taxCode);
        Assert.NotNull(stored);
        Assert.Equal((DealerStatus.Pending, DealerBusinessType.Company, false), (stored!.Status, stored.BusinessType, stored.ShipToHeadOffice));
        Assert.Equal("99 Kho Nhận Hàng", stored.ShippingAddress);
        Assert.NotNull(stored.BusinessLicense);

        await using var manager = await fx.NewRoleContextAsync(AppRoles.SalesManager);
        await fx.RunAsync(manager, "Sales_manager_approves_dealer", async page =>
        {
            var dealers = new AdminDealersPage(page);
            await dealers.SearchAsync(taxCode);
            await Expect(dealers.Row(taxCode)).ToContainTextAsync("Chờ duyệt");
            await dealers.OpenDetailAsync(taxCode);
            await Expect(dealers.Modal.Locator("[data-license-link]")).ToHaveTextAsync("gpkd.pdf");
            await Expect(dealers.Modal).ToContainTextAsync("99 Kho Nhận Hàng");

            await dealers.Action("approve").ClickAsync();
            await dealers.Toasts.ExpectSuccessAsync("Đã duyệt đại lý và gửi mã đại lý qua email.");
            await Expect(dealers.Row(taxCode)).ToContainTextAsync("Đã duyệt");
        });
    }

    [E2EFact]
    public async Task Household_registers_without_license_using_owner_citizen_id()
    {
        var suffix = E2EDealerData.Suffix();
        var taxCode = "0" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        var businessName = $"Hộ kinh doanh E2E {suffix}";

        await using (var guest = await fx.NewGuestContextAsync())
        {
            await fx.RunAsync(guest, nameof(Household_registers_without_license_using_owner_citizen_id), async page =>
            {
                var register = new DealerRegisterPage(page);
                await register.GotoAsync();
                await register.ChooseTypeAsync("Household");
                await Expect(register.Form.Locator("[name=HouseholdRegistrationNo]")).ToBeVisibleAsync();
                await Expect(register.Form.Locator("[name=RepresentativeTitle]")).ToBeHiddenAsync();
                await Expect(register.Form.Locator("[name=CitizenId]")).ToHaveAttributeAsync("required", "");

                await register.FillAsync("BusinessName", businessName);
                await register.FillAsync("TaxCode", taxCode);
                await register.FillAsync("HouseholdRegistrationNo", "01A8012345");
                await register.HeadOfficeAsync(E2EDealerData.ProvinceCode, E2EDealerData.CommuneCode, "5 Ngõ Hộ Kinh Doanh");
                await register.FillAsync("RepresentativeName", "Phạm Chủ Hộ");
                await register.FillAsync("CitizenId", taxCode);
                await register.FillAsync("FullName", "Phạm Chủ Hộ");
                await register.FillAsync("Phone", "0987654321");
                await register.FillAsync("Email", $"hkd-{suffix}@e2e.local");
                await register.ContinueAsync();

                await Expect(register.Confirm).ToContainTextAsync(businessName);
                await Expect(register.Confirm).ToContainTextAsync("01A8012345");
                await register.SendAsync();
            });
        }

        var stored = await E2EDealerData.DealerByTaxCodeAsync(fx.Services, taxCode);
        Assert.NotNull(stored);
        Assert.Equal(DealerBusinessType.Household, stored!.BusinessType);
        Assert.Equal("01A8012345", stored.HouseholdRegistrationNo);
        Assert.Null(stored.RepresentativeTitle);
        Assert.Null(stored.BusinessLicenseMediaFileId);
        Assert.True(stored.ShipToHeadOffice);
    }

    [E2EFact]
    public async Task Sales_manager_adds_dealer_in_modal_then_edits_it()
    {
        var suffix = E2EDealerData.Suffix();
        var taxCode = E2EDealerData.CompanyTaxCode();
        var businessName = $"Công ty Admin E2E {suffix}";

        await using var manager = await fx.NewRoleContextAsync(AppRoles.SalesManager);
        await fx.RunAsync(manager, nameof(Sales_manager_adds_dealer_in_modal_then_edits_it), async page =>
        {
            var dealers = new AdminDealersPage(page);
            await page.GotoAsync("/Admin/Dealers");
            await page.Locator("#btn-add-dealer").ClickAsync();
            var form = dealers.Modal.Locator("#dealer-form");
            await Expect(form).ToBeVisibleAsync();

            await form.Locator("[name=BusinessName]").FillAsync(businessName);
            await form.Locator("[name=TaxCode]").FillAsync("123");
            await form.Locator("[name=RepresentativeName]").FillAsync("Nguyễn Văn Đại");
            await form.Locator("[name=RepresentativeTitle]").FillAsync("Giám đốc");
            await form.Locator("[name=FullName]").FillAsync("Lê Liên Hệ");
            await form.Locator("[name=Phone]").FillAsync("0912345678");
            await form.Locator("[name=Email]").FillAsync($"admin-{suffix}@e2e.local");
            await form.Locator("select[name=ProvinceCode]").SelectOptionAsync(E2EDealerData.ProvinceCode);
            await form.Locator("select[name=CommuneCode]").SelectOptionAsync(E2EDealerData.CommuneCode);
            await form.Locator("[name=Address]").FillAsync("12 Đường Admin");
            await form.GetByRole(AriaRole.Button, new() { Name = "Thêm", Exact = true }).ClickAsync();
            await dealers.Toasts.ExpectErrorAsync("MST doanh nghiệp phải gồm 10 chữ số");

            await dealers.Modal.Locator("#dealer-form [name=TaxCode]").FillAsync(taxCode);
            await dealers.Modal.Locator("#dealer-form").GetByRole(AriaRole.Button, new() { Name = "Thêm", Exact = true }).ClickAsync();
            await dealers.Toasts.ExpectSuccessAsync("Đã thêm đại lý và gửi mã đại lý qua email.");

            await dealers.SearchAsync(taxCode);
            await Expect(dealers.Row(taxCode)).ToContainTextAsync("Đã duyệt");
            await dealers.OpenDetailAsync(taxCode);
            await Expect(dealers.Modal.Locator("[data-field=accessCode]")).ToContainTextAsync("HRN-••••-••••-");
            await dealers.Action("edit").ClickAsync();
            await Expect(dealers.Modal.Locator("#dealer-form [name=BusinessName]")).ToHaveValueAsync(businessName);
            await dealers.Modal.Locator("#dealer-form [name=BusinessName]").FillAsync(businessName + " (sửa)");
            await dealers.Modal.Locator("#dealer-form").GetByRole(AriaRole.Button, new() { Name = "Lưu", Exact = true }).ClickAsync();
            await dealers.Toasts.ExpectSuccessAsync("Đã cập nhật đại lý.");
            await Expect(dealers.Row(taxCode)).ToContainTextAsync(businessName + " (sửa)");
        });
    }
}
