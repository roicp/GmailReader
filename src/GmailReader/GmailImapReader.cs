using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace GmailReader;

/// <summary>
/// Encapsula a conversa IMAP com o Gmail: conexão, busca e leitura das mensagens.
/// </summary>
public sealed partial class GmailImapReader : IAsyncDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);

    private readonly GmailOptions _options;
    private readonly ImapClient _client = new();

    public GmailImapReader(GmailOptions options) => _options = options;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        // Sem isso, uma porta 993 bloqueada por firewall deixa o console pendurado.
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(ConnectTimeout);

        try
        {
            // SslOnConnect = TLS implícito, que é o esperado na porta 993.
            await _client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.SslOnConnect, attempt.Token);
            await _client.AuthenticateAsync(_options.User, _options.NormalizedPassword, attempt.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Não houve resposta de {_options.Host}:{_options.Port} em {ConnectTimeout.TotalSeconds:0} segundos. " +
                "Verifique a conexão e se a saída TCP na porta 993 está liberada no firewall ou proxy.");
        }
    }

    /// <summary>Lista o nome completo de todas as pastas da conta.</summary>
    public async Task<IReadOnlyList<string>> ListFoldersAsync(CancellationToken cancellationToken)
    {
        var names = new List<string>();

        foreach (var personalNamespace in _client.PersonalNamespaces)
        {
            var root = _client.GetFolder(personalNamespace);
            await CollectFoldersAsync(root, names, cancellationToken);
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private static async Task CollectFoldersAsync(IMailFolder folder, List<string> names, CancellationToken cancellationToken)
    {
        foreach (var child in await folder.GetSubfoldersAsync(false, cancellationToken))
        {
            if (child.Attributes.HasFlag(FolderAttributes.NonExistent))
            {
                continue;
            }

            names.Add(child.FullName);

            if (!child.Attributes.HasFlag(FolderAttributes.NoInferiors))
            {
                await CollectFoldersAsync(child, names, cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyList<EmailMessageInfo>> ReadAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        var folder = await ResolveFolderAsync(options.Folder, cancellationToken);

        // Abrir em ReadOnly evita que a simples leitura marque as mensagens como lidas.
        var access = options.MarkAsRead ? FolderAccess.ReadWrite : FolderAccess.ReadOnly;
        await folder.OpenAsync(access, cancellationToken);

        var uids = await folder.SearchAsync(BuildQuery(options), cancellationToken);

        // A busca devolve os UIDs em ordem crescente; os últimos são os mais recentes.
        var selected = uids.OrderBy(uid => uid.Id).TakeLast(options.Count).ToList();
        if (selected.Count == 0)
        {
            return Array.Empty<EmailMessageInfo>();
        }

        const MessageSummaryItems Items = MessageSummaryItems.UniqueId
                                          | MessageSummaryItems.Envelope
                                          | MessageSummaryItems.Flags
                                          | MessageSummaryItems.Size
                                          | MessageSummaryItems.InternalDate
                                          | MessageSummaryItems.BodyStructure;

        var summaries = await folder.FetchAsync(selected, Items, cancellationToken);

        var messages = new List<EmailMessageInfo>(summaries.Count);
        foreach (var summary in summaries.OrderByDescending(summary => summary.Date))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var attachments = summary.Attachments
                .Select(attachment => attachment.FileName ?? attachment.ContentType.Name ?? "(anexo sem nome)")
                .ToList();

            IReadOnlyList<string> saved = Array.Empty<string>();
            if (options.AttachmentsDirectory is { Length: > 0 } directory)
            {
                saved = await SaveAttachmentsAsync(folder, summary.UniqueId, directory, cancellationToken);
            }

            messages.Add(new EmailMessageInfo
            {
                Uid = summary.UniqueId.Id,
                Date = summary.Date,
                From = Describe(summary.Envelope?.From),
                To = Describe(summary.Envelope?.To),
                Subject = string.IsNullOrWhiteSpace(summary.Envelope?.Subject) ? "(sem assunto)" : summary.Envelope!.Subject,
                IsRead = summary.Flags?.HasFlag(MessageFlags.Seen) ?? false,
                Size = summary.Size ?? 0,
                Attachments = attachments,
                SavedAttachments = saved,
                Preview = await GetPreviewAsync(folder, summary, options.PreviewLength, cancellationToken),
            });

            if (options.MarkAsRead)
            {
                await folder.AddFlagsAsync(summary.UniqueId, MessageFlags.Seen, silent: true, cancellationToken);
            }
        }

        await folder.CloseAsync(false, cancellationToken);
        return messages;
    }

    private async Task<IMailFolder> ResolveFolderAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Equals("INBOX", StringComparison.OrdinalIgnoreCase))
        {
            return _client.Inbox;
        }

        try
        {
            return await _client.GetFolderAsync(name, cancellationToken);
        }
        catch (FolderNotFoundException)
        {
            throw new InvalidOperationException(
                $"A pasta '{name}' não existe nesta conta. Rode novamente com --list-folders para ver os nomes disponíveis.");
        }
    }

    private static SearchQuery BuildQuery(CommandLineOptions options)
    {
        var query = options.OnlyUnread ? SearchQuery.NotSeen : SearchQuery.All;

        if (options.Since is { } since)
        {
            // O comando SINCE do IMAP ja inclui as mensagens do proprio dia informado.
            query = query.And(SearchQuery.DeliveredAfter(since));
        }

        if (!string.IsNullOrWhiteSpace(options.FromContains))
        {
            query = query.And(SearchQuery.FromContains(options.FromContains));
        }

        if (!string.IsNullOrWhiteSpace(options.SubjectContains))
        {
            query = query.And(SearchQuery.SubjectContains(options.SubjectContains));
        }

        return query;
    }

    private static async Task<string?> GetPreviewAsync(
        IMailFolder folder,
        IMessageSummary summary,
        int maxLength,
        CancellationToken cancellationToken)
    {
        if (maxLength <= 0)
        {
            return null;
        }

        string? text = null;

        if (summary.TextBody is { } textPart)
        {
            var entity = await folder.GetBodyPartAsync(summary.UniqueId, textPart, cancellationToken);
            text = (entity as TextPart)?.Text;
        }
        else if (summary.HtmlBody is { } htmlPart)
        {
            var entity = await folder.GetBodyPartAsync(summary.UniqueId, htmlPart, cancellationToken);
            var html = (entity as TextPart)?.Text;
            text = html is null ? null : HtmlToPlainText(html);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var flattened = WhitespaceRegex().Replace(text, " ").Trim();
        return flattened.Length <= maxLength ? flattened : flattened[..maxLength] + "...";
    }

    /// <summary>
    /// Conversão simples de HTML para texto, suficiente para a prévia exibida no terminal.
    /// </summary>
    private static string HtmlToPlainText(string html)
    {
        var withoutInvisibleParts = ScriptAndStyleRegex().Replace(html, " ");
        var withoutTags = HtmlTagRegex().Replace(withoutInvisibleParts, " ");
        return WebUtility.HtmlDecode(withoutTags);
    }

    private static async Task<IReadOnlyList<string>> SaveAttachmentsAsync(
        IMailFolder folder,
        UniqueId uid,
        string directory,
        CancellationToken cancellationToken)
    {
        var message = await folder.GetMessageAsync(uid, cancellationToken);
        var saved = new List<string>();

        foreach (var attachment in message.Attachments)
        {
            Directory.CreateDirectory(directory);

            var name = attachment is MessagePart
                ? attachment.ContentDisposition?.FileName ?? "mensagem-anexada.eml"
                : (attachment as MimePart)?.FileName ?? "anexo.bin";

            var path = BuildUniquePath(directory, $"{uid.Id}-{Sanitize(name)}");

            await using (var stream = File.Create(path))
            {
                if (attachment is MessagePart { Message: { } embedded })
                {
                    await embedded.WriteToAsync(stream, cancellationToken);
                }
                else if (attachment is MimePart { Content: { } content })
                {
                    await content.DecodeToAsync(stream, cancellationToken);
                }
            }

            saved.Add(path);
        }

        return saved;
    }

    private static string Sanitize(string fileName)
    {
        var builder = new StringBuilder(fileName.Length);
        var invalid = Path.GetInvalidFileNameChars();

        foreach (var character in fileName)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        var sanitized = builder.ToString().Trim().Trim('.');
        return sanitized.Length == 0 ? "anexo.bin" : sanitized;
    }

    private static string BuildUniquePath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            return path;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var counter = 1; ; counter++)
        {
            var candidate = Path.Combine(directory, $"{name} ({counter}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static string Describe(InternetAddressList? addresses)
    {
        if (addresses is null || addresses.Count == 0)
        {
            return "(desconhecido)";
        }

        return string.Join(", ", addresses.Select(address => address switch
        {
            MailboxAddress mailbox when !string.IsNullOrWhiteSpace(mailbox.Name) => $"{mailbox.Name} <{mailbox.Address}>",
            MailboxAddress mailbox => mailbox.Address,
            _ => address.ToString(),
        }));
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(true);
        }

        _client.Dispose();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptAndStyleRegex();
}
