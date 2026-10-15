using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NewHarian.Web.ModelBinding;

/// <summary>
/// <c>&lt;input type="number"&gt;</c> always posts "22.5", but the default request culture is vi where "."
/// is the group separator (22.5 would bind as 225). Values shaped like a number input with 1-2 decimals are
/// parsed invariantly; anything else falls back to the culture-aware default binder.
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
        if (result != ValueProviderResult.None && TryParseNumberInput(result.FirstValue, out var value))
        {
            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);
            bindingContext.Result = ModelBindingResult.Success(value);
            return Task.CompletedTask;
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
