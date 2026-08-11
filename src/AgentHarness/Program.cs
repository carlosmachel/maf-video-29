#pragma warning disable MAAI001
#pragma warning disable OPENAI001

// ============================================================================
// DEMO 2 — Agent + Harness
//
// Mesma tarefa do Demo 1, mas agora o chat client é envolvido pelo Harness:
// ganhamos de graça uma todo-list, modo plan/execute, memória em arquivo e
// aprovação de ferramentas. A diferença que queremos que a plateia VEJA:
// o agente escreve um arquivo de verdade em ./output/relatorio.md.
// ============================================================================

using AgentHarness.Tools;
using Azure.AI.Projects;
using Azure.Identity;
using dotenv.net;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

DotEnv.Load();

var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT")
               ?? throw new InvalidOperationException("Defina FOUNDRY_PROJECT.");
var deploymentName = Environment.GetEnvironmentVariable("DEPLOYMENT_NAME") ?? "gpt-5-mini";

var credential = new AzureCliCredential();

AIProjectClient client = new(new Uri(endpoint), credential);

// Pasta real, visível no disco — é isso que aparece durante a demo.
var outputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output");
Directory.CreateDirectory(outputDir);

AIAgent agent = client.GetProjectOpenAIClient()
.GetResponsesClient()
.AsIChatClient()
.AsHarnessAgent(new HarnessAgentOptions
{
    Name = "AnalistaDeVendas",
    ChatOptions = new ChatOptions
    {
        ModelId = deploymentName,
        Instructions = "Voce e um analista de vendas. Use as ferramentas disponiveis " +
                        "para responder com numeros reais. Nunca invente valores.",
        Tools =
        [
            AIFunctionFactory.Create(SalesTools.GetRevenue),
            AIFunctionFactory.Create(SalesTools.GetTopProducts),
            AIFunctionFactory.Create(SalesTools.CompareRevenue),
        ],
    },
    // Sem web search: nao precisamos e evita dependencia de rede durante a demo.
    DisableWebSearch = true,
    // Trocamos o file access padrao (sandbox interno) por um apontando pra uma
    // pasta real no disco, com auto-aprovacao para o publico ver o resultado
    // sem precisar confirmar cada escrita ao vivo.
    FileAccessStore = new FileSystemAgentFileStore(outputDir),
    FileAccessProviderOptions = new FileAccessProviderOptions
    {
        DisableReadOnlyToolApproval = true,
        DisableWriteToolApproval = true,
    },
});

// A sessão carrega o estado do harness (plano, todos, histórico) entre turnos.
AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine("=== Agent + Harness ===");
Console.WriteLine($"Arquivos vao aparecer em: {Path.GetFullPath(outputDir)}");
Console.WriteLine("Sugestao de prompt:");
Console.WriteLine("  Compare a receita de outubro e novembro de 2025, explique o que mudou nos");
Console.WriteLine("  top produtos, escreva um plano de acao para o proximo mes e salve tudo em");
Console.WriteLine("  relatorio.md.");
Console.WriteLine();
Console.WriteLine("Digite 'exit' para sair.");

while (true)
{
    Console.Write("\n> ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    // Streama a saida do turno enquanto o harness planeja, chama ferramentas
    // (incluindo file_access_write) e trabalha a lista de todos.
    await foreach (var update in agent.RunStreamingAsync(input, session))
    {
        Console.Write(update);
    }

    Console.WriteLine();
}
