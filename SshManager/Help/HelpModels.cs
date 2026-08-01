namespace SshManager.Help;

public enum HelpBlockKind
{
    Heading,
    Paragraph,
    Bullets,
    Code,
    Note
}

public enum HelpLanguage
{
    English,
    Persian
}

public sealed class HelpBlock
{
    public HelpBlockKind Kind { get; init; }
    public string TextEn { get; init; } = string.Empty;
    public string TextFa { get; init; } = string.Empty;
    public IReadOnlyList<string> ItemsEn { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ItemsFa { get; init; } = Array.Empty<string>();
}

public sealed class HelpSection
{
    public required string Id { get; init; }
    public required string Icon { get; init; }
    public required string TitleEn { get; init; }
    public required string TitleFa { get; init; }
    public required IReadOnlyList<HelpBlock> Blocks { get; init; }
}
