namespace GmailReader;

/// <summary>
/// Representação enxuta de uma mensagem, já pronta para exibição.
/// </summary>
public sealed record EmailMessageInfo
{
    public required uint Uid { get; init; }

    public required DateTimeOffset Date { get; init; }

    public required string From { get; init; }

    public required string To { get; init; }

    public required string Subject { get; init; }

    public required bool IsRead { get; init; }

    public required long Size { get; init; }

    public IReadOnlyList<string> Attachments { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> SavedAttachments { get; init; } = Array.Empty<string>();

    public string? Preview { get; init; }
}
