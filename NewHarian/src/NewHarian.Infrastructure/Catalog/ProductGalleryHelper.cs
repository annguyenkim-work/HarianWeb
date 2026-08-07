namespace NewHarian.Infrastructure.Catalog;

public static class ProductGalleryHelper
{
    public static List<string> BuildGallerySlides(string? mainImagePath, IEnumerable<string?> variantImagePaths)
    {
        var slides = new List<string>();
        if (!string.IsNullOrWhiteSpace(mainImagePath))
            slides.Add(mainImagePath);

        foreach (var path in variantImagePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (!slides.Contains(path))
                slides.Add(path);
        }

        return slides;
    }

    public static int ResolveSlideIndex(List<string> slides, string? mainImagePath, string? variantImagePath)
    {
        if (!string.IsNullOrWhiteSpace(variantImagePath))
        {
            var idx = slides.IndexOf(variantImagePath);
            if (idx >= 0) return idx;
        }

        if (!string.IsNullOrWhiteSpace(mainImagePath))
        {
            var mainIdx = slides.IndexOf(mainImagePath);
            if (mainIdx >= 0) return mainIdx;
        }

        return slides.Count > 0 ? 0 : 0;
    }
}
