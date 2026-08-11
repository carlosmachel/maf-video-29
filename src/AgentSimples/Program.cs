// ============================================================================
// DEMO 1 — Agent simples
//
// Um AIAgent puro: modelo + duas function tools. O loop de tool-calling
// (chamar a tool, devolver o resultado pro modelo, repetir) é feito pelo
// framework, mas é só isso — sem plano, sem memória entre execuções, sem
// controle de qualidade. Cada RunAsync começa do zero.
// ============================================================================
using AgentSimples.Tools;
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

AIAgent agent = client.AsAIAgent(deploymentName,
        name: "AnalistaDeVendas",
        instructions: "Voce e um analista de vendas. Use as ferramentas disponiveis " +
                      "para responder com numeros reais. Nunca invente valores.",
        tools:
        [
            AIFunctionFactory.Create(SalesTools.GetRevenue),
            AIFunctionFactory.Create(SalesTools.GetTopProducts),
            AIFunctionFactory.Create(SalesTools.CompareRevenue),
        ]);

// --- Pergunta 1: single-shot, o caso ideal para um agent simples ----------
Console.WriteLine("=== Pergunta simples ===");
Console.WriteLine("Qual foi a receita de outubro de 2025 e quais os top 3 produtos do mes?");

Console.WriteLine(await agent.RunAsync("Qual foi a receita de outubro de 2025 e quais os top 3 produtos do mes?"));

// --- Pergunta 2: multi-step, mostra o limite do agent simples --------------
// Ele ainda responde (tool-calling funciona bem aqui), mas repare:
//   - nao existe um plano visivel, nem lista de tarefas
//   - nada fica gravado em disco — se voce pedir "salve isso num arquivo",
//     ele nao tem essa ferramenta
//   - a cada novo agent.RunAsync(), a conversa comeca do zero (sem sessão)
Console.WriteLine("\n=== Pergunta multi-step (sem plano, sem memoria, sem arquivo) ===");
Console.WriteLine(await agent.RunAsync(
    "Compare a receita de outubro e novembro de 2025, explique o que mudou nos " +
    "top produtos, escreva um plano de acao para o proximo mes e salve tudo em relatorio.md."));
