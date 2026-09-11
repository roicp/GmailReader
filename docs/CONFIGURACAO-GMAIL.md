# Passo a passo: liberar o acesso IMAP da conta do Gmail

O Google **não aceita mais a senha normal da conta** em conexões IMAP. Desde
maio/2022 (contas pessoais) e janeiro/2025 (Google Workspace), a opção "Apps
menos seguros" foi removida e restaram dois caminhos:

| Caminho | Quando usar |
| --- | --- |
| **Senha de app** (usada por este console) | Aplicação sua, rodando na sua própria conta. Simples: 16 caracteres no lugar da senha. |
| **OAuth 2.0 / XOAUTH2** | Aplicação distribuída a terceiros, ou conta que não pode usar senha de app. Exige app registrado no Google Cloud. |

Os cinco passos abaixo cobrem o caminho da senha de app.

---

## Passo 1 — Ativar o IMAP no Gmail

1. Abra o Gmail no navegador: <https://mail.google.com>.
2. Clique na **engrenagem** (canto superior direito) e em **Ver todas as configurações**.
3. Vá até a aba **Encaminhamento e POP/IMAP**.
4. Na seção **Acesso IMAP**, marque **Ativar IMAP**.
5. Clique em **Salvar alterações** no rodapé da página.

> **Google Workspace (conta corporativa):** se a seção aparecer bloqueada, o
> administrador precisa liberar em *Admin Console → Apps → Google Workspace →
> Gmail → Acesso do usuário final → POP e IMAP*. Sem isso, nenhuma senha de app
> vai funcionar.

## Passo 2 — Ativar a verificação em duas etapas

A senha de app **só existe** em contas com verificação em duas etapas ativa.

1. Acesse <https://myaccount.google.com/security>.
2. Em **Como fazer login no Google**, clique em **Verificação em duas etapas**.
3. Siga o assistente (confirmação por celular, app autenticador ou chave de segurança).
4. Confirme que o status final é **Ativada**.

## Passo 3 — Gerar a senha de app

1. Acesse <https://myaccount.google.com/apppasswords>.
   - Se o link não abrir direto, vá em *Conta do Google → Segurança →
     Verificação em duas etapas* e role até **Senhas de app**. Também dá para
     digitar "senhas de app" na busca da Conta do Google.
2. Em **Nome do app**, escreva algo que identifique o uso, por exemplo `GmailReader`.
3. Clique em **Criar**.
4. O Google mostra **16 caracteres em quatro blocos**, algo como `abcd efgh ijkl mnop`.
   Copie agora: essa senha **não é exibida novamente**.
   - Os espaços são só visuais. O console remove os espaços automaticamente,
     então tanto faz colar com ou sem eles.
5. Guarde em um gerenciador de senhas. Para revogar depois, volte nessa mesma
   tela e clique na lixeira ao lado do nome.

> **A opção "Senhas de app" não aparece?** As causas mais comuns são:
> verificação em duas etapas desativada (Passo 2); conta inscrita no *Programa
> de Proteção Avançada*; conta Workspace cujo administrador desabilitou senhas
> de app; ou conta gerenciada por uma escola/empresa com login federado (SSO).
> Nesses casos, o caminho é o OAuth 2.0.

## Passo 4 — Informar as credenciais ao console

Escolha **uma** das três formas. A ordem de prioridade é a mesma da lista.

### a) Variáveis de ambiente (recomendado)

```bash
# Linux / macOS
export GMAIL_USER="fulano@gmail.com"
export GMAIL_APP_PASSWORD="abcdefghijklmnop"
```

```powershell
# Windows (PowerShell) - somente na sessão atual
$env:GMAIL_USER = "fulano@gmail.com"
$env:GMAIL_APP_PASSWORD = "abcdefghijklmnop"
```

### b) dotnet user-secrets (não encosta no repositório)

```bash
cd src/GmailReader
dotnet user-secrets init
dotnet user-secrets set "Gmail:User" "fulano@gmail.com"
dotnet user-secrets set "Gmail:AppPassword" "abcdefghijklmnop"
```

### c) appsettings.Local.json

Copie `src/GmailReader/appsettings.Local.json.example` para
`src/GmailReader/appsettings.Local.json` e preencha:

```json
{
  "Gmail": {
    "User": "fulano@gmail.com",
    "AppPassword": "abcdefghijklmnop",
    "Host": "imap.gmail.com",
    "Port": 993
  }
}
```

O arquivo `appsettings.Local.json` está no `.gitignore` justamente para não ser
versionado. **Não** coloque a senha no `appsettings.json`.

Se nada for configurado, o console pergunta a senha no terminal, sem eco.

## Passo 5 — Testar

```bash
dotnet run --project src/GmailReader -- --count 5
```

Saída esperada: "Autenticado com sucesso." seguida das cinco mensagens mais
recentes da caixa de entrada.

---

## Parâmetros do servidor

| Item | Valor |
| --- | --- |
| Servidor | `imap.gmail.com` |
| Porta | `993` |
| Segurança | SSL/TLS implícito (`SslOnConnect`) |
| Usuário | endereço completo, incluindo `@gmail.com` |
| Senha | os 16 caracteres da senha de app |

## Erros comuns

| Mensagem | Causa provável | Solução |
| --- | --- | --- |
| `AUTHENTICATIONFAILED: Invalid credentials (Failure)` | Senha da conta em vez da senha de app, ou senha de app revogada | Refaça o Passo 3 |
| `[ALERT] Application-specific password required` | Verificação em duas etapas ativa e senha comum sendo usada | Passo 3 |
| `[ALERT] Please log in via your web browser` | Bloqueio de segurança do Google | Faça login pelo navegador, confirme o alerta de segurança e tente de novo |
| `IMAP access is disabled for your domain` | IMAP desligado no Gmail ou no Workspace | Passo 1 |
| `Too many simultaneous connections` | Gmail limita 15 conexões IMAP simultâneas por conta | Encerre outros clientes; o console já desconecta ao terminar |
| `A pasta 'X' não existe nesta conta` | Nome da pasta depende do idioma da conta | Rode com `--list-folders` e use o nome exato |
| `Não houve resposta de imap.gmail.com:993 em 30 segundos` | Rede, proxy ou firewall bloqueando a porta 993 | Libere a saída TCP 993 |

## Limites do Gmail via IMAP

- **2.500 MB/dia** de download e 500 MB/dia de upload por conta.
- **15 conexões IMAP simultâneas** por conta.
- Contas com muitos rótulos: a pasta `[Gmail]/Todos os e-mails` contém a
  mensagem uma única vez, enquanto os rótulos são "pastas" apontando para ela.

## Alternativa: OAuth 2.0 (XOAUTH2)

Se a conta não puder usar senha de app, o fluxo é:

1. Criar um projeto em <https://console.cloud.google.com>.
2. Habilitar a **Gmail API**.
3. Configurar a **tela de consentimento OAuth** e adicionar o escopo
   `https://mail.google.com/` (é o escopo que o IMAP exige).
4. Criar credenciais do tipo **ID do cliente OAuth → App para computador**.
5. Obter o *access token* com a biblioteca `Google.Apis.Auth` e autenticar com
   `SaslMechanismOAuth2` em vez de usuário/senha:

```csharp
var oauth2 = new SaslMechanismOAuth2(userEmail, accessToken);
await client.AuthenticateAsync(oauth2, cancellationToken);
```

O restante do código deste repositório (busca, leitura, anexos) continua igual —
só a autenticação muda.

## Segurança

- Trate a senha de app como senha: ela dá acesso total à caixa de e-mail.
- Uma senha de app por aplicação, para poder revogar isoladamente.
- Nunca faça commit de credenciais; use variáveis de ambiente ou user-secrets.
- Revogue em <https://myaccount.google.com/apppasswords> assim que não precisar mais.
