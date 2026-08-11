// ============================================================================
// DEMO 3 — Loop Engineering
//
// O Harness te dá um loop pronto e opinativo. Aqui fazemos o oposto: pegamos
// um agent qualquer (nem precisa ser harness) e envolvemos com um LoopAgent
// + um LoopEvaluator que A GENTE projeta. Em vez de confiar no bom senso do
// modelo pra saber quando parou, um "juiz" (outro chat client) avalia a
// resposta contra critérios explícitos e manda de volta com feedback até
// passar — ou até bater o limite de segurança (MaxIterations).
// ============================================================================

using Azure.AI.Projects;
using Azure.Identity;
using dotenv.net;
using LoopEngineering.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
#pragma warning disable MAAI001

DotEnv.Load();

var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT")
               ?? throw new InvalidOperationException("Defina FOUNDRY_PROJECT.");
var deploymentName = Environment.GetEnvironmentVariable("DEPLOYMENT_NAME") ?? "gpt-5-mini";

var credential = new AzureCliCredential();

AIProjectClient client = new(new Uri(endpoint), credential);

// O agente "de trabalho": o mesmo analista de vendas dos demos 1 e 2, sem harness.
AIAgent workerAgent = client
    .AsAIAgent(
        deploymentName,
        name: "AnalistaDeVendas",
        instructions: "Voce e um analista de vendas. Use as ferramentas disponiveis " +
                      "para responder com numeros reais. Nunca invente valores.",
        tools:
        [
            AIFunctionFactory.Create(SalesTools.GetRevenue),
            AIFunctionFactory.Create(SalesTools.GetTopProducts),
            AIFunctionFactory.Create(SalesTools.CompareRevenue),
        ]);

// O "juiz": um chat client separado, sem tools nem sessão, só pra avaliar.
IChatClient judgeClient = client.GetProjectOpenAIClient().GetChatClient(deploymentName).AsIChatClient();

const string feedbackAuthor = "juiz (feedback)";

string[] criteria =
[
    "A resposta deve conter uma tabela markdown comparando os dois meses.",
    "A resposta deve terminar com uma secao chamada 'Riscos' com pelo menos 2 riscos.",
    "A resposta deve incluir uma recomendacao de acao clara para o proximo mes.",
];

var judge = new AIJudgeLoopEvaluator(judgeClient, new AIJudgeLoopEvaluatorOptions
{
    Criteria = criteria,
});

AIAgent loopedAgent = new LoopAgent(workerAgent, judge, new LoopAgentOptions
{
    MaxIterations = 3, // trava de seguranca — loops julgados por IA custam tempo e dinheiro
    OnBehalfOfAuthorName = feedbackAuthor,
});

const string task =
    "Compare a receita de outubro e novembro de 2025 e recomende uma acao para dezembro.";

Console.WriteLine("=== Loop Engineering ===");
Console.WriteLine($"Tarefa: {task}");
Console.WriteLine("Criterios do juiz:");
foreach (var criterio in criteria)
{
    Console.WriteLine($"  - {criterio}");
}
Console.WriteLine();

int iteration = 0;
await foreach (var update in loopedAgent.RunStreamingAsync(task))
{
    if (update.AuthorName == feedbackAuthor)
    {
        iteration++;
        Console.WriteLine();
        Console.WriteLine($"──── juiz pediu ajustes — iniciando iteracao {iteration + 1} ────");
        Console.WriteLine($"Feedback: {update}");
        Console.WriteLine();
        continue;
    }

    Console.Write(update);
}

Console.WriteLine();
Console.WriteLine(iteration == 0
    ? "\n(o juiz aprovou de primeira — sem re-iteracao)"
    : $"\n(precisou de {iteration + 1} iteracoes para passar no criterio)");
