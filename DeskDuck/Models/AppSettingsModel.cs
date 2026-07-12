namespace DeskDuck.Models;

public class AppSettingsModel
{
    public PublishersSection Publishers { get; set; } = new();
    public GeneralSection General { get; set; } = new();
}
