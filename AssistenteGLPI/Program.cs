using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;

class Program
{
    // URL da API do GLPI vem do ambiente (ex.: https://seu-glpi/apirest.php), nunca fixa no código
    private static readonly string GlpiUrl = System.Environment.GetEnvironmentVariable("GLPI_URL")
        ?? throw new InvalidOperationException("Defina a variável de ambiente GLPI_URL (ex.: https://seu-glpi/apirest.php).");
    // Correção 1: Uso explícito do System.Environment
    private static readonly string AppToken = System.Environment.GetEnvironmentVariable("GLPI_APP_TOKEN") ?? "coloque a api token do GLPI aqui";
    private static readonly string UserToken = System.Environment.GetEnvironmentVariable("GLPI_USER_TOKEN") ?? "coloque a api token do seu usuário GLPI aqui.";

    static async Task Main(string[] args)
    {
        // Correção 2: Uso explícito do System.Environment
        if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("GEMINI_API_KEY")))
        {
            Console.Write("Digite sua GEMINI_API_KEY: ");
            var apiKey = Console.ReadLine();
            System.Environment.SetEnvironmentVariable("GEMINI_API_KEY", apiKey);
        }

        var client = new Client();

        var consultarGlpiTool = new FunctionDeclaration
        {
            Name = "ConsultarGlpi",
            Description = "Consulta recursos no sistema GLPI (Ticket, Computer, User, Printer, Software, etc).",
            Parameters = new Schema
            {
                Type = Google.GenAI.Types.Type.Object,
                Properties = new Dictionary<string, Schema>
                {
                    ["itemType"] = new Schema { Type = Google.GenAI.Types.Type.String, Description = "Tipo do item no GLPI (ex: Ticket, Computer, User)." },
                    ["itemId"] = new Schema { Type = Google.GenAI.Types.Type.Integer, Description = "ID específico do item (opcional)." },
                    ["buscaTexto"] = new Schema { Type = Google.GenAI.Types.Type.String, Description = "Termo para busca/filtro (opcional)." }
                },
                Required = new List<string> { "itemType" }
            }
        };

        var config = new GenerateContentConfig
        {
            Tools = new List<Tool> { new Tool { FunctionDeclarations = new List<FunctionDeclaration> { consultarGlpiTool } } },
            SystemInstruction = new Content
            {
                Parts = new List<Part>
                {
                    new Part { Text = "Você é um assistente completo de TI integrado ao GLPI. Sempre que o usuário perguntar sobre dados do sistema, use a função ConsultarGlpi. Para perguntas gerais, responda diretamente." }
                }
            }
        };

        Console.Clear();
        Console.WriteLine("=============================================");
        Console.WriteLine("   ASSISTENTE GEMINI + GLPI (.NET 8)");
        Console.WriteLine("=============================================");
        Console.WriteLine("Digite qualquer pergunta sobre o GLPI ou geral (ou 'sair' para encerrar):\n");

        while (true)
        {
            Console.Write("\nVocê: ");
            string? pergunta = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(pergunta) || pergunta.ToLower() == "sair")
                break;

            try
            {
                var response = await client.Models.GenerateContentAsync(
                    model: "gemini-3.6-flash",
                    contents: pergunta,
                    config: config
                );

                if (response.FunctionCalls != null && response.FunctionCalls.Count > 0)
                {
                    foreach (var call in response.FunctionCalls)
                    {
                        Console.WriteLine($"  [Buscando dados no GLPI...]");

                        var argsJson = JsonSerializer.Serialize(call.Args);
                        var argsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(argsJson);

                        string itemType = argsDict?.ContainsKey("itemType") == true ? argsDict["itemType"]?.ToString()! : "Ticket";
                        int? itemId = argsDict?.ContainsKey("itemId") == true && int.TryParse(argsDict["itemId"]?.ToString(), out int parsedId) ? parsedId : null;
                        string? busca = argsDict?.ContainsKey("buscaTexto") == true ? argsDict["buscaTexto"]?.ToString() : null;

                        string resultadoJson = await ExecutarConsultaGlpiAsync(itemType, itemId, busca);

                        var responseData = new Dictionary<string, object>
                        {
                            ["data"] = resultadoJson
                        };

                        // Correção 3: Reutilizando o Content original para preservar a thought_signature
                        var contentsHistory = new List<Content>
                        {
                            new Content { Parts = new List<Part> { new Part { Text = pergunta } } },
                            response.Candidates[0].Content, 
                            new Content 
                            { 
                                Parts = new List<Part> 
                                { 
                                    new Part 
                                    { 
                                        FunctionResponse = new FunctionResponse { Name = call.Name, Response = responseData } 
                                    } 
                                } 
                            }
                        };

                        var finalResponse = await client.Models.GenerateContentAsync(
                            model: "gemini-3.6-flash",
                            contents: contentsHistory
                        );

                        Console.WriteLine($"\nGemini: {finalResponse.Text}");
                    }
                }
                else
                {
                    Console.WriteLine($"\nGemini: {response.Text}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nErro: {ex.Message}");
            }
        }
    }

    private static async Task<string> ExecutarConsultaGlpiAsync(string itemType, int? itemId, string? buscaTexto)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("App-Token", AppToken);
        http.DefaultRequestHeaders.Add("Authorization", $"user_token {UserToken}");

        var initResp = await http.GetAsync($"{GlpiUrl}/initSession");
        if (!initResp.IsSuccessStatusCode)
            throw new Exception("Falha ao iniciar sessão no GLPI.");

        var sessionJson = await initResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(sessionJson);
        string sessionToken = doc.RootElement.GetProperty("session_token").GetString()!;

        http.DefaultRequestHeaders.Remove("Authorization");
        http.DefaultRequestHeaders.Add("Session-Token", sessionToken);

        try
        {
            string endpoint = itemId.HasValue
                ? $"{GlpiUrl}/{itemType}/{itemId}"
                : $"{GlpiUrl}/{itemType}?range=0-15&order=DESC";

            if (!string.IsNullOrEmpty(buscaTexto) && !itemId.HasValue)
            {
                endpoint += $"&searchText[name]={Uri.EscapeDataString(buscaTexto)}";
            }

            var dataResp = await http.GetAsync(endpoint);
            if (!dataResp.IsSuccessStatusCode)
                throw new Exception("Falha ao consultar dados no GLPI.");

            return await dataResp.Content.ReadAsStringAsync();
        }
        finally
        {
            await http.GetAsync($"{GlpiUrl}/killSession");
        }
    }
}