using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NewHarian.Application.Formatting;

namespace NewHarian.Web.ModelBinding;

/// <summary>
/// Binds decimals from: (1) HTML number inputs like "22.5", (2) VND UI strings like "1.500.000 VNĐ",
/// else the culture-aware default binder. Avoids vi culture treating "." as a thousands separator for "22.5".
/// </summary>
public sealed partial class NumberInputDecimalBinder(IModelBinder fallback) : IModelBinder
{
    [GeneratedRegex(@"^-?\d+\.\d{1,2}$")]
    private static partial Regex NumberInputRegex();

    public static bool TryParseNumberInput(string? raw, out decimal value)
    {
        value = 0;
        var v = raw?.Trim();
        return !string.IsNullOrEmpty(v) && NumberInputRegex().IsMatch(v)
               && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (result != ValueProviderResult.None)
        {
            var raw = result.FirstValue;
            if (TryParseNumberInput(raw, out var value) || MoneyFormat.TryParse(raw, out value))
            {
                bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);
                bindingContext.Result = ModelBindingResult.Success(value);
                return Task.CompletedTask;
            }
        }
        return fallback.BindModelAsync(bindingContext);
    }
}

public sealed class NumberInputDecimalBinderProvider(IModelBinderProvider inner) : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = context.Metadata.UnderlyingOrModelType;
        if (type != typeof(decimal)) return null;
        var fallback = inner.GetBinder(context);
        return fallback is null ? null : new NumberInputDecimalBinder(fallback);
    }
}
