namespace DeskDuck.Models;

public class DuckConfig
{
    public double Speed { get; set; } = 2.0;
    public int MinWaitSeconds { get; set; } = 5;
    public int MaxWaitSeconds { get; set; } = 15;
}
