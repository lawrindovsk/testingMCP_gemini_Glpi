# Assistente GLPI com IA (Gemini)

Aplicação de console em **C# / .NET 8** que conecta o **Google Gemini** à **API REST do GLPI**. Você pergunta em linguagem natural ("quais chamados abertos citam impressora?") e o modelo decide, via *function calling*, quando consultar o GLPI, buscando tickets, computadores, usuários, impressoras e outros itens, para responder com os dados reais.

Projeto de estudo do conceito de **MCP / ferramentas para LLMs** aplicado ao dia a dia de suporte e ITSM.

## Como funciona
1. O programa declara para o Gemini uma ferramenta `ConsultarGlpi(itemType, itemId?, buscaTexto?)`.
2. A cada pergunta, o Gemini pode pedir para chamar essa ferramenta.
3. O programa abre uma sessão no GLPI (`initSession`), consulta o endpoint pedido e fecha a sessão (`killSession`).
4. O resultado volta para o Gemini, que monta a resposta final.

## Tecnologias
- C# / .NET 8 (console)
- [Google.GenAI](https://www.nuget.org/packages/Google.GenAI) (Gemini, function calling)
- `HttpClient` + `System.Text.Json` (API REST do GLPI)

## Como executar
Pré-requisitos: .NET 8 SDK, uma chave do Gemini e tokens da API do GLPI (App-Token e User-Token).

```powershell
# Windows (PowerShell)
$env:GLPI_URL = "https://seu-glpi/apirest.php"
$env:GLPI_APP_TOKEN = "..."
$env:GLPI_USER_TOKEN = "..."
$env:GEMINI_API_KEY = "..."   # opcional: se faltar, o programa pergunta
dotnet run --project AssistenteGLPI
```

```bash
# Linux/macOS
export GLPI_URL="https://seu-glpi/apirest.php" GLPI_APP_TOKEN="..." GLPI_USER_TOKEN="..." GEMINI_API_KEY="..."
dotnet run --project AssistenteGLPI
```

> Nenhuma URL ou credencial fica no código: tudo vem de variáveis de ambiente.

---

**EN:** C#/.NET 8 console app that connects Google Gemini (function calling) to the GLPI REST API, so you can ask about tickets and assets in natural language. Set `GLPI_URL`, `GLPI_APP_TOKEN`, `GLPI_USER_TOKEN` and `GEMINI_API_KEY`, then run `dotnet run --project AssistenteGLPI`.
