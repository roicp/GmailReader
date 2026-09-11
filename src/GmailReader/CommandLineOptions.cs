using System.Globalization;

namespace GmailReader;

/// <summary>
/// Argumentos aceitos pelo console.
/// </summary>
public sealed class CommandLineOptions
{
    public string Folder { get; private set; } = "INBOX";

    public int Count { get; private set; } = 10;

    public bool OnlyUnread { get; private set; }

    public DateTime? Since { get; private set; }

    public string? FromContains { get; private set; }

    public string? SubjectContains { get; private set; }

    public int PreviewLength { get; private set; } = 200;

    public string? AttachmentsDirectory { get; private set; }

    public bool MarkAsRead { get; private set; }

    public bool ListFolders { get; private set; }

    public string? User { get; private set; }

    public bool ShowHelp { get; private set; }

    /// <exception cref="FormatException">Quando um argumento é desconhecido ou tem valor inválido.</exception>
    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];

            switch (argument)
            {
                case "-h" or "--help" or "-?" or "/?":
                    options.ShowHelp = true;
                    break;

                case "-f" or "--folder":
                    options.Folder = NextValue(args, ref i, argument);
                    break;

                case "-n" or "--count":
                    options.Count = ParsePositiveInt(NextValue(args, ref i, argument), argument);
                    break;

                case "-u" or "--unread":
                    options.OnlyUnread = true;
                    break;

                case "--since":
                    options.Since = ParseDate(NextValue(args, ref i, argument), argument);
                    break;

                case "--from":
                    options.FromContains = NextValue(args, ref i, argument);
                    break;

                case "--subject":
                    options.SubjectContains = NextValue(args, ref i, argument);
                    break;

                case "--preview":
                    options.PreviewLength = ParseNonNegativeInt(NextValue(args, ref i, argument), argument);
                    break;

                case "--save-attachments":
                    options.AttachmentsDirectory = NextValue(args, ref i, argument);
                    break;

                case "--mark-seen":
                    options.MarkAsRead = true;
                    break;

                case "--list-folders":
                    options.ListFolders = true;
                    break;

                case "--user":
                    options.User = NextValue(args, ref i, argument);
                    break;

                default:
                    throw new FormatException($"argumento desconhecido '{argument}'.");
            }
        }

        return options;
    }

    private static string NextValue(string[] args, ref int index, string argument)
    {
        if (index + 1 >= args.Length)
        {
            throw new FormatException($"o argumento '{argument}' exige um valor.");
        }

        return args[++index];
    }

    private static int ParsePositiveInt(string value, string argument)
    {
        var parsed = ParseNonNegativeInt(value, argument);
        if (parsed == 0)
        {
            throw new FormatException($"o valor de '{argument}' deve ser maior que zero.");
        }

        return parsed;
    }

    private static int ParseNonNegativeInt(string value, string argument)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            throw new FormatException($"'{value}' não é um número válido para '{argument}'.");
        }

        return parsed;
    }

    private static DateTime ParseDate(string value, string argument)
    {
        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy"];
        if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw new FormatException($"'{value}' não é uma data válida para '{argument}'. Use yyyy-MM-dd ou dd/MM/yyyy.");
        }

        return parsed;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            GmailReader - lê e-mails de uma conta do Gmail via IMAP.

            Uso:
              dotnet run --project src/GmailReader -- [opções]

            Opções:
              -f, --folder <nome>        Pasta a ser lida (padrão: INBOX). Ex.: "[Gmail]/Spam"
              -n, --count <n>            Quantidade de mensagens mais recentes (padrão: 10)
              -u, --unread               Traz somente as mensagens não lidas
                  --since <data>         Somente mensagens entregues a partir da data (yyyy-MM-dd)
                  --from <texto>         Filtra pelo remetente
                  --subject <texto>      Filtra pelo assunto
                  --preview <n>          Caracteres do corpo exibidos (padrão: 200; 0 desliga)
                  --save-attachments <p> Salva os anexos na pasta informada
                  --mark-seen            Marca como lidas as mensagens exibidas
                  --list-folders         Lista as pastas da conta e encerra
                  --user <e-mail>        Conta a ser usada (sobrepõe a configuração)
              -h, --help                 Mostra esta ajuda

            Credenciais (nesta ordem de prioridade):
              1. Variáveis de ambiente GMAIL_USER e GMAIL_APP_PASSWORD
              2. dotnet user-secrets (chaves Gmail:User e Gmail:AppPassword)
              3. appsettings.Local.json / appsettings.json

            A liberação do acesso na Conta do Google está descrita em docs/CONFIGURACAO-GMAIL.md.
            """);
    }
}
