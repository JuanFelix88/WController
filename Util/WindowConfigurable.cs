public enum MatchMode : byte
{
    Path = 0,
    Regex = 1
}

public class WindowConfigurable
{
    public string Shortcut { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ProgramPath { get; set; } = string.Empty;
    public MatchMode MatchMode { get; set; } = MatchMode.Path;
    public string RegexPattern { get; set; } = string.Empty;

    private System.Text.RegularExpressions.Regex? _compiledRegex;

    public System.Text.RegularExpressions.Regex? CompiledRegex
    {
        get
        {
            if (MatchMode != MatchMode.Regex || string.IsNullOrEmpty(RegexPattern))
                return null;

            if (_compiledRegex is null)
            {
                try
                {
                    _compiledRegex = new System.Text.RegularExpressions.Regex(
                        RegexPattern,
                        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                catch
                {
                    return null;
                }
            }
            return _compiledRegex;
        }
    }

    public void InvalidateRegex()
    {
        _compiledRegex = null;
    }

    public bool Matches(string windowTitle, string programPath)
    {
        if (MatchMode == MatchMode.Regex)
        {
            var regex = CompiledRegex;
            return regex is not null && regex.IsMatch(windowTitle);
        }
        return ProgramPath == programPath;
    }

    public override string ToString()
    {
        return Shortcut;
    }
}