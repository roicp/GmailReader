using System.Globalization;

namespace GmailReader;

/// <summary>
/// Formatação da saída no terminal.
/// </summary>
public static class ConsoleRenderer
{
    public static void RenderMessages(IReadOnlyList<EmailMessageInfo> messages, CommandLineOptions options)
    {
        Console.WriteLine();

        if (messages.Count == 0)
        {
            Console.WriteLine($"Nenhuma mensagem encontrada em '{options.Folder}' com os filtros informados.");
            return;
        }

        Console.WriteLine($"{messages.Count} mensagem(ns) em '{options.Folder}' (da mais recente para a mais antiga):");

        var position = 1;
        foreach (var message in messages)
        {
            Console.WriteLine();
            Console.WriteLine(new string('-', 72));
            Console.WriteLine($"[{position++}] UID {message.Uid}  {(message.IsRead ? "lida" : "NÃO LIDA")}");
            Console.WriteLine($"Data....: {message.Date.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture)}");
            Console.WriteLine($"De......: {message.From}");
            Console.WriteLine($"Para....: {message.To}");
            Console.WriteLine($"Assunto.: {message.Subject}");
            Console.WriteLine($"Tamanho.: {FormatSize(message.Size)}");

            if (message.Attachments.Count > 0)
            {
                Console.WriteLine($"Anexos..: {string.Join(", ", message.Attachments)}");
            }

            foreach (var path in message.SavedAttachments)
            {
                Console.WriteLine($"Salvo em: {path}");
            }

            if (!string.IsNullOrWhiteSpace(message.Preview))
            {
                Console.WriteLine();
                Console.WriteLine(message.Preview);
            }
        }

        Console.WriteLine();
        Console.WriteLine(new string('-', 72));
    }

    public static void RenderFolders(IReadOnlyList<string> folders)
    {
        Console.WriteLine();
        Console.WriteLine($"{folders.Count} pasta(s) encontradas:");

        foreach (var folder in folders)
        {
            Console.WriteLine($"  {folder}");
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "desconhecido";
        }

        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.CurrentCulture, $"{value:0.#} {units[unit]}");
    }
}
