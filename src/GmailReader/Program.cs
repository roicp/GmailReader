using System.Net.Sockets;
using System.Text;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Configuration;

namespace GmailReader;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        CommandLineOptions options;
        try
        {
            options = CommandLineOptions.Parse(args);
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine($"Erro nos argumentos: {ex.Message}");
            Console.Error.WriteLine("Use --help para ver as opções disponíveis.");
            return 2;
        }

        if (options.ShowHelp)
        {
            CommandLineOptions.PrintUsage();
            return 0;
        }

        var settings = LoadSettings(options);

        if (string.IsNullOrWhiteSpace(settings.AppPassword))
        {
            settings.AppPassword = PromptForPassword(settings.User);
        }

        if (settings.Validate() is { } problem)
        {
            Console.Error.WriteLine($"Configuração incompleta: {problem}");
            Console.Error.WriteLine("O passo a passo está em docs/CONFIGURACAO-GMAIL.md.");
            return 2;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        await using var reader = new GmailImapReader(settings);

        try
        {
            Console.WriteLine($"Conectando em {settings.Host}:{settings.Port} como {settings.User}...");
            await reader.ConnectAsync(cancellation.Token);
            Console.WriteLine("Autenticado com sucesso.");

            if (options.ListFolders)
            {
                ConsoleRenderer.RenderFolders(await reader.ListFoldersAsync(cancellation.Token));
                return 0;
            }

            ConsoleRenderer.RenderMessages(await reader.ReadAsync(options, cancellation.Token), options);
            return 0;
        }
        catch (AuthenticationException ex)
        {
            Console.Error.WriteLine($"Falha na autenticação: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Verifique se:");
            Console.Error.WriteLine("  1. a verificação em duas etapas está ativa na Conta do Google;");
            Console.Error.WriteLine("  2. você está usando uma senha de app de 16 caracteres, e não a senha da conta;");
            Console.Error.WriteLine("  3. o IMAP está habilitado em Gmail > Configurações > Encaminhamento e POP/IMAP.");
            Console.Error.WriteLine("Detalhes em docs/CONFIGURACAO-GMAIL.md.");
            return 3;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operação cancelada pelo usuário.");
            return 130;
        }
        catch (Exception ex) when (ex is SslHandshakeException or SocketException or ImapProtocolException or ImapCommandException or TimeoutException)
        {
            Console.Error.WriteLine($"Falha de comunicação com o servidor IMAP: {ex.Message}");
            return 4;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 5;
        }
    }

    /// <summary>
    /// Prioridade: argumentos da linha de comando &gt; variáveis de ambiente &gt; user-secrets &gt; appsettings.
    /// </summary>
    private static GmailOptions LoadSettings(CommandLineOptions options)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var settings = configuration.GetSection("Gmail").Get<GmailOptions>() ?? new GmailOptions();

        // Atalhos amigáveis, para quem não quer usar a sintaxe Gmail__User do provedor de ambiente.
        settings.User = FirstFilled(options.User, Environment.GetEnvironmentVariable("GMAIL_USER"), settings.User);
        settings.AppPassword = FirstFilled(Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD"), settings.AppPassword);

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            settings.Host = "imap.gmail.com";
        }

        if (settings.Port == 0)
        {
            settings.Port = 993;
        }

        return settings;
    }

    private static string FirstFilled(params string?[] candidates) =>
        candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    /// <summary>Leitura da senha sem eco, usada quando nada foi configurado.</summary>
    private static string PromptForPassword(string user)
    {
        if (Console.IsInputRedirected)
        {
            return string.Empty;
        }

        Console.Write($"Senha de app para {(string.IsNullOrWhiteSpace(user) ? "a conta" : user)}: ");

        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }
}
