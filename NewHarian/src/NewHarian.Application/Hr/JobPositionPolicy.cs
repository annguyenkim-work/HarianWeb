namespace NewHarian.Application.Hr;

public readonly record struct JobPositionChoice(int Id, string Name, bool IsActive, int SortOrder);

public static class JobPositionPolicy
{
    public const int NameMax = 100;
    public const string NameRequired = "Tên vị trí bắt buộc.";
    public const string NameTooLong = "Tên vị trí quá dài.";
    public const string NameDuplicate = "Tên vị trí đã có.";
    public const string Missing = "Vị trí không tồn tại.";
    public const string Inactive = "Vị trí này đang tắt.";

    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return NameRequired;
        if (name.Trim().Length > NameMax) return NameTooLong;
        return null;
    }

    public static bool IsDuplicate(string name, IEnumerable<(int Id, string Name)> existing, int? selfId)
    {
        var key = name.Trim();
        return existing.Any(x => x.Id != selfId
            && string.Equals(x.Name.Trim(), key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Active positions, plus the one already on a profile even when it has been turned off.</summary>
    public static IReadOnlyList<JobPositionChoice> Choices(IEnumerable<JobPositionChoice> all, int? includeId)
        => all.Where(p => p.IsActive || p.Id == includeId)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToList();

    public static string? ValidateAssignment(bool exists, bool isActive, int? postedId, int? currentId)
    {
        if (postedId is null) return null;
        if (!exists) return Missing;
        if (!isActive && postedId != currentId) return Inactive;
        return null;
    }
}
