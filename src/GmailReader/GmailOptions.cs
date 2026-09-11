namespace GmailReader;

/// <summary>
/// Dados de conexão com o servidor IMAP do Gmail.
/// </summary>
public sealed class GmailOptions
{
    /// <summary>Endereço completo da conta (ex.: fulano@gmail.com).</summary>
    public string User { get; set; } = string.Empty;

    /// <summary>Senha de app de 16 caracteres gerada na Conta do Google.</summary>
    public string AppPassword { get; set; } = string.Empty;

    public string Host { get; set; } = "imap.gmail.com";

    public int Port { get; set; } = 993;

    /// <summary>
    /// O Google exibe a senha de app em quatro blocos separados por espaço.
    /// O servidor espera os 16 caracteres sem separadores.
    /// </summary>
    public string NormalizedPassword =>
        new(AppPassword.Where(c => !char.IsWhiteSpace(c)).ToArray());

    /// <summary>Retorna a mensagem do primeiro problema encontrado, ou <c>null</c> se estiver tudo certo.</summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(User))
        {
            return "Conta não informada. Use --user, a variável GMAIL_USER ou a chave Gmail:User em appsettings.Local.json.";
        }

        if (!User.Contains('@'))
        {
            return $"'{User}' não é um endereço de e-mail válido. Informe o endereço completo, incluindo o domínio.";
        }

        if (string.IsNullOrWhiteSpace(AppPassword))
        {
            return "Senha de app não informada. Use a variável GMAIL_APP_PASSWORD ou a chave Gmail:AppPassword em appsettings.Local.json.";
        }

        if (NormalizedPassword.Length != 16)
        {
            return "A senha de app do Google tem 16 caracteres. Confira se você não colou a senha normal da conta " +
                   "(veja docs/CONFIGURACAO-GMAIL.md).";
        }

        if (Port is < 1 or > 65535)
        {
            return $"Porta inválida: {Port}.";
        }

        return null;
    }
}
