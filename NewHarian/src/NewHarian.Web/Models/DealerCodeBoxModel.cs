using NewHarian.Application.Dealers;

namespace NewHarian.Web.Models;

/// <param name="Dealer">Verified dealer of this checkout session, or null to show the code input.</param>
public record DealerCodeBoxModel(DealerOptionDto? Dealer, string ReturnUrl, string? Error);
