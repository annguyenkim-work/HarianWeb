namespace NewHarian.Web.Tests;

/// <summary>Guest pages that were plain forms now render the paper-kit markers: split contact, empty state, confirmation note.</summary>
public class GuestPaperUiTests : IClassFixture<NewHarianWebApplicationFactory>
{
    private readonly NewHarianWebApplicationFactory _factory;

    public GuestPaperUiTests(NewHarianWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Contact_page_splits_company_facts_and_form()
    {
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/contact");

        Assert.Contains("contact-split", html);
        Assert.Contains("contact-aside", html);
        Assert.Contains("form-stack", html);
        Assert.Contains("name=\"Message\"", html);
    }

    [Fact]
    public async Task Empty_cart_offers_one_way_back_to_products()
    {
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/cart");

        Assert.Contains("empty-state", html);
        Assert.Contains("btn-primary", html);
        Assert.Contains("/Products", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task News_and_careers_lists_render_an_empty_state_or_cards()
    {
        var client = _factory.CreateClient();
        var news = await client.GetStringAsync("/news");
        var careers = await client.GetStringAsync("/careers");

        Assert.True(news.Contains("empty-state", StringComparison.Ordinal) || news.Contains("post-card", StringComparison.Ordinal));
        Assert.True(careers.Contains("empty-state", StringComparison.Ordinal) || careers.Contains("post-card", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/contact/thank-you")]
    [InlineData("/careers/thank-you")]
    [InlineData("/dealers/thank-you")]
    public async Task Thank_you_pages_show_a_received_note(string path)
    {
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync(path);

        Assert.Contains("note-ok", html);
        Assert.Contains("content-main--sheet", html);
        Assert.Contains("btn-primary", html);
    }

    [Fact]
    public async Task Guest_stylesheet_keeps_paper_field_and_table_rules_off_admin()
    {
        var client = _factory.CreateClient();
        var css = await client.GetStringAsync("/css/site.css");

        Assert.Contains("background: #faf9f6", css);
        Assert.Contains(".note-ok", css);
        Assert.Contains(".empty-state", css);
        Assert.Contains(".content-main--sheet", css);
        Assert.Contains(".field-hint", css);
        Assert.Contains(".action-row", css);
        Assert.Contains("outline: 2px solid var(--guest-gold)", css);
        Assert.Contains(".contact-split", css);
        Assert.Contains("body:not(.admin-body) .data-table thead th", css);
        Assert.Contains("body:not(.admin-body) .form-stack", css);
        Assert.Contains("body:not(.admin-body) .radio-row:has(input:checked)", css);
        Assert.Contains(".validation-summary-errors", css);
        Assert.Contains("--guest-navy: #0b132b", css);
        Assert.Contains("body:not(.admin-body) .btn-primary", css);
        Assert.Contains("Plus Jakarta Sans", css);
        Assert.DoesNotContain("rgba(0, 0, 0, 0.92)", css);
    }

    [Fact]
    public async Task Dealer_register_uses_shared_field_hint_and_plain_fieldset()
    {
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/dealers/register");

        Assert.Contains("field-hint", html);
        Assert.Contains("fieldset-plain", html);
        Assert.DoesNotContain("style=\"display:block;margin-top:0.25rem\"", html);
    }

    [Fact]
    public async Task Guest_layout_loads_the_kit_typeface()
    {
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/");

        Assert.Contains("Plus+Jakarta+Sans", html);
        Assert.Contains("JetBrains+Mono", html);
        Assert.Contains("site-header", html);
        Assert.Contains("site-footer", html);
        Assert.True(
            html.Contains("empty-state", StringComparison.Ordinal) || html.Contains("card-grid", StringComparison.Ordinal),
            "Home should show an empty state or category and product cards.");
    }
}
