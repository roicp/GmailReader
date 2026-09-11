# GmailReader

Console em C# (.NET 8) que lê os e-mails recebidos em uma conta do Gmail via
**IMAP**, usando a biblioteca [MailKit](https://github.com/jstedfast/MailKit).

> **Antes de rodar:** a conta do Google precisa ser liberada para acesso IMAP.
> O passo a passo completo está em **[docs/CONFIGURACAO-GMAIL.md](docs/CONFIGURACAO-GMAIL.md)**.

## Recursos

- Lista as mensagens mais recentes de qualquer pasta/rótulo da conta.
- Filtra por não lidas, data, remetente e assunto (a busca roda no servidor).
- Mostra remetente, destinatário, assunto, data, tamanho, anexos e um trecho do corpo
  (converte HTML para texto quando não há parte em texto puro).
- Salva os anexos em disco (`--save-attachments`).
- Abre a pasta em modo somente leitura: ler **não** marca as mensagens como lidas,
  a menos que você peça com `--mark-seen`.
- Credenciais fora do código: variáveis de ambiente, `dotnet user-secrets` ou
  `appsettings.Local.json`.

## Pré-requisitos

- [.NET SDK 8.0](https://dotnet.microsoft.com/download) ou superior.
- Conta do Gmail com IMAP ativado e uma senha de app (veja o guia de configuração).

## Como executar

```bash
# 1. Configure as credenciais (detalhes no guia)
export GMAIL_USER="fulano@gmail.com"
export GMAIL_APP_PASSWORD="abcdefghijklmnop"

# 2. Restaure e rode
dotnet restore
dotnet run --project src/GmailReader -- --count 10
```

No Windows (PowerShell), troque `export` por `$env:GMAIL_USER = "..."`.

## Exemplos

```bash
# 20 mensagens mais recentes da caixa de entrada
dotnet run --project src/GmailReader -- -n 20

# Somente as não lidas, sem mostrar o corpo
dotnet run --project src/GmailReader -- --unread --preview 0

# Recebidas a partir de 01/09/2026 com "nota fiscal" no assunto
dotnet run --project src/GmailReader -- --since 2026-09-01 --subject "nota fiscal"

# De um remetente específico, salvando os anexos
dotnet run --project src/GmailReader -- --from contato@empresa.com --save-attachments ./anexos

# Ver o nome exato das pastas/rótulos (varia com o idioma da conta)
dotnet run --project src/GmailReader -- --list-folders

# Ler outra pasta e marcar como lidas as mensagens exibidas
dotnet run --project src/GmailReader -- --folder "[Gmail]/Spam" --mark-seen
```

## Opções

| Opção | Descrição | Padrão |
| --- | --- | --- |
| `-f`, `--folder <nome>` | Pasta a ser lida | `INBOX` |
| `-n`, `--count <n>` | Quantidade de mensagens mais recentes | `10` |
| `-u`, `--unread` | Somente mensagens não lidas | desligado |
| `--since <data>` | A partir da data (`yyyy-MM-dd` ou `dd/MM/yyyy`) | sem filtro |
| `--from <texto>` | Filtra pelo remetente | sem filtro |
| `--subject <texto>` | Filtra pelo assunto | sem filtro |
| `--preview <n>` | Caracteres do corpo exibidos (`0` desliga) | `200` |
| `--save-attachments <pasta>` | Salva os anexos na pasta informada | desligado |
| `--mark-seen` | Marca como lidas as mensagens exibidas | desligado |
| `--list-folders` | Lista as pastas da conta e encerra | — |
| `--user <e-mail>` | Conta a ser usada (sobrepõe a configuração) | — |
| `-h`, `--help` | Ajuda | — |

Códigos de saída: `0` sucesso, `2` argumento/configuração inválida,
`3` falha de autenticação, `4` falha de comunicação, `5` pasta inexistente,
`130` cancelado com Ctrl+C.

## Estrutura

```
GmailReader.sln
src/GmailReader/
├── Program.cs               Entrada, configuração e tratamento de erros
├── GmailImapReader.cs       Conexão IMAP, busca, leitura e anexos (MailKit)
├── CommandLineOptions.cs    Parsing dos argumentos
├── GmailOptions.cs          Credenciais e validação
├── EmailMessageInfo.cs      Modelo de saída
├── ConsoleRenderer.cs       Formatação no terminal
└── appsettings.json         Configuração sem segredos
docs/CONFIGURACAO-GMAIL.md   Passo a passo da liberação na Conta do Google
```

## Segurança

`appsettings.Local.json` está no `.gitignore`. Nunca faça commit de senhas de
app — elas dão acesso total à caixa de e-mail e podem ser revogadas a qualquer
momento em <https://myaccount.google.com/apppasswords>.
