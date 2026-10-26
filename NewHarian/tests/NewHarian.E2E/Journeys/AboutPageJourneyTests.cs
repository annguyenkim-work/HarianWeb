using NewHarian.E2E.Fixtures;

namespace NewHarian.E2E.Journeys;

[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
[Trait("Priority", "P1")]
public sealed class AboutPageJourneyTests(E2EFixture fx)
{
    [E2EFact]
    public async Task Guest_opens_about_from_nav_without_dropdown_and_sees_sections()
    {
        await using var context = await fx.NewGuestContextAsync();
        await fx.RunAsync(context, nameof(Guest_opens_about_from_nav_without_dropdown_and_sees_sections), async page =>
        {
            await page.GotoAsync("/");
            var aboutNav = page.Locator("nav.site-nav a[href='/about']");
            await Expect(aboutNav).ToBeVisibleAsync();
            await Expect(page.Locator("nav.site-nav .nav-dropdown__btn")).ToHaveCountAsync(0);
            await aboutNav.ClickAsync();
            await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*/about/?$"));
            await Expect(page.GetByText("Hồ sơ công ty")).ToBeVisibleAsync();
            await Expect(page.GetByText("Triết lý")).ToBeVisibleAsync();
            await Expect(page.GetByText("Cam kết chất lượng")).ToBeVisibleAsync();
            await Expect(page.GetByText("Hồ sơ năng lực")).ToBeVisibleAsync();
        });
    }

    [E2EFact]
    public async Task Legacy_company_url_lands_on_about()
    {
        await using var context = await fx.NewGuestContextAsync();
        await fx.RunAsync(context, nameof(Legacy_company_url_lands_on_about), async page =>
        {
            await page.GotoAsync("/company");
            await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*/about/?$"));
            await Expect(page.GetByText("Hồ sơ công ty")).ToBeVisibleAsync();
        });
    }
}
