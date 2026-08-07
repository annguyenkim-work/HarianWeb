namespace NewHarian.Infrastructure.Logging;

public sealed class AppLoggingOptions
{
    public const string SectionName = "AppLogging";

    public int RetainInformationDays { get; set; } = 10;
    public int RetainWarningErrorDays { get; set; } = 90;
}
