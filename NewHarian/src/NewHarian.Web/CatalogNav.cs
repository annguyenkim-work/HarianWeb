using NewHarian.Application.Catalog;
using NewHarian.Domain.Enums;

namespace NewHarian.Web;

/// <summary>Maps CatalogKind enum ids to guest controllers / URL hubs.</summary>
public static class CatalogNav
{
    public const string ProductsController = "Products";
    public const string ServicesController = "Services";

    public static string ControllerName(CatalogKind kind) =>
        kind == CatalogKind.Service ? ServicesController : ProductsController;

    public static bool IsService(CatalogKind kind) => kind == CatalogKind.Service;

    /// <summary>
    /// Hub for a category card: services-only → Services, goods-only → Products,
    /// mixed → whichever has more published items (tie → Products).
    /// </summary>
    public static string CategoryHubController(CategoryCardDto category)
    {
        if (category.ServiceCount > 0 && category.PhysicalCount == 0)
            return ServicesController;
        if (category.PhysicalCount > 0 && category.ServiceCount == 0)
            return ProductsController;
        return category.ServiceCount > category.PhysicalCount
            ? ServicesController
            : ProductsController;
    }
}
