namespace TikTokOrderPrinter.Options;

public sealed class AppDataOptions
{
    public const string SectionName = "AppData";

    public string StateFilePath { get; set; } = "Data/runtime-state.json";
    public string PrintJobDirectory { get; set; } = "Data/print-jobs";
    public string TicketTemplatePath { get; set; } = "Data/ticket-template.txt";
}
