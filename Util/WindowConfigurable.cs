public class WindowConfigurable
{
    public string Shortcut { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ProgramPath { get; set; } = string.Empty;

    public override string ToString()
    {
        return Shortcut;
    }
}