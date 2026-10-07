using System.Text.RegularExpressions;
using NewHarian.Application.Abstractions;
using NewHarian.E2E.Fixtures;
using NewHarian.E2E.Pages;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P0")]
public sealed class RbacJourneyTests(E2EFixture fx)
{
    [E2EFact]
    public async Task Hr_staff_sees_applications_but_not_orders()
    {
        await using var context = await fx.NewRoleContextAsync(AppRoles.HrStaff);
        await fx.RunAsync(context, nameof(Hr_staff_sees_applications_but_not_orders), async page =>
        {
            await page.GotoAsync("/admin");
            var shell = new AdminShell(page);
            await Expect(shell.SidebarLink("Hồ sơ ứng tuyển")).ToHaveCountAsync(1);
            await Expect(shell.SidebarLink("Đơn hàng")).ToHaveCountAsync(0);
            await Expect(shell.SidebarLink("Kho")).ToHaveCountAsync(0);

            await shell.SidebarLink("Hồ sơ ứng tuyển").EvaluateAsync("a => a.click()");
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Hồ sơ tuyển dụng", Level = 1 })).ToBeVisibleAsync();

            await page.GotoAsync("/Admin/Orders");
            await Expect(page).ToHaveURLAsync(new Regex("/admin/access-denied", RegexOptions.IgnoreCase));
        });
    }

    [E2EFact]
    public async Task Warehouse_manager_sees_inventory_and_inventory_settings()
    {
        await using var context = await fx.NewRoleContextAsync(AppRoles.WarehouseManager);
        await fx.RunAsync(context, nameof(Warehouse_manager_sees_inventory_and_inventory_settings), async page =>
        {
            await page.GotoAsync("/admin");
            var shell = new AdminShell(page);
            await Expect(shell.SidebarLinkTo("/Admin/Inventory")).ToHaveCountAsync(1);
            await Expect(shell.SidebarLinkTo("/Admin/InventorySettings")).ToHaveCountAsync(1);
            await Expect(shell.SidebarLink("Hồ sơ ứng tuyển")).ToHaveCountAsync(0);
            await Expect(shell.SidebarLink("Users")).ToHaveCountAsync(0);

            await page.GotoAsync("/admin/InventorySettings");
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Cài đặt kho", Level = 1 })).ToBeVisibleAsync();
        });
    }

    [E2EFact]
    public async Task Forbidden_modal_fetch_shows_403_toast_instead_of_access_denied_page()
    {
        await using var context = await fx.NewRoleContextAsync(AppRoles.WarehouseStaff);
        await fx.RunAsync(context, nameof(Forbidden_modal_fetch_shows_403_toast_instead_of_access_denied_page), async page =>
        {
            await page.GotoAsync("/Admin/Inventory");
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Kho", Level = 1 })).ToBeVisibleAsync();

            // Same path a modal takes: fetch the form partial. Warehouse Staff lacks Inventory.ManageLocations.
            var outcome = await page.EvaluateAsync<string>("""
                async () => {
                  try {
                    const res = await fetch('/Admin/Inventory/EditLocation');
                    return 'resolved:' + res.status;
                  } catch (e) {
                    return 'rejected:' + e.message;
                  }
                }
                """);

            await new AdminToasts(page).ExpectErrorAsync("Bạn không có quyền thực hiện thao tác này.");
            Assert.Equal("rejected:Forbidden", outcome);
            await Expect(page).ToHaveURLAsync(new Regex("/Admin/Inventory$", RegexOptions.IgnoreCase));
        });
    }
}
