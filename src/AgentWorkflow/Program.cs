// ============================================================================
// DEMO 4 — Workflow (multi-agente)
//
// As 3 demos anteriores giram em torno de UM agente, com camadas crescentes
// de controle (Agent -> Harness -> Loop). Aqui trocamos de eixo: em vez de um
// agente só, um Workflow do Microsoft Agent Framework conecta VÁRIOS agentes
// num grafo. Dois especialistas (financeiro e produto) rodam em PARALELO
// sobre a mesma pergunta (fan-out) e um terceiro nó agrega as duas respostas
// e pede pra um agente Redator sintetizar tudo num relatório único (fan-in).
// ============================================================================

using AgentWorkflow.Tools;
using Azure.AI.Projects;
using Azure.Identity;
using dotenv.net;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

DotEnv.Load();

var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT")
               ?? throw new InvalidOperationException("Defina FOUNDRY_PROJECT.");
var deploymentName = Environment.GetEnvironmentVariable("DEPLOYMENT_NAME") ?? "gpt-5-mini";


var credential = new AzureCliCredential();

AIProjectClient client = new(new Uri(endpoint), credential);

// Dois especialistas que respondem a MESMA pergunta em paralelo, cada um só
// com as tools do seu domínio.
AIAgent analistaFinanceiro = client.AsAIAgent(deploymentName,
    name: "AnalistaFinanceiro",
    instructions: "Voce e um analista financeiro. Use as ferramentas disponiveis para falar de receita " +
                  "com numeros reais. Nunca invente valores. Foque so em receita, nao fale de produtos.",
    tools:
    [
        AIFunctionFactory.Create(SalesTools.GetRevenue),
        AIFunctionFactory.Create(SalesTools.CompareRevenue),
    ]);

AIAgent analistaDeProduto = client.AsAIAgent(deploymentName,
    name: "AnalistaDeProduto",
    instructions: "Voce e um analista de produto. Use as ferramentas disponiveis para falar dos produtos " +
                  "mais vendidos com dados reais. Nunca invente valores. Foque so em produtos, nao fale de receita.",
    tools:
    [
        AIFunctionFactory.Create(SalesTools.GetTopProducts),
    ]);

// O Redator não entra no fan-out: ele só é chamado pelo nó de agregação,
// depois que os dois especialistas terminam.
AIAgent redatorAgent = client.AsAIAgent(deploymentName,
    name: "Redator",
    instructions: "Voce recebe analises prontas de outros especialistas (financeiro e de produto) e escreve " +
                  "um relatorio unico em markdown: um titulo, um resumo executivo, uma tabela comparativa e " +
                  "uma recomendacao. Nao invente numeros - use so o que os especialistas informaram.");

Coordenador coordenador = new();
Redator redator = new(redatorAgent);

Workflow workflow = new WorkflowBuilder(coordenador)
    .AddFanOutEdge(coordenador, [analistaFinanceiro, analistaDeProduto])
    .AddFanInBarrierEdge([analistaFinanceiro, analistaDeProduto], redator)
    .WithOutputFrom(redator)
    .Build();

Console.WriteLine("=== Workflow: dois especialistas em paralelo + Redator ===");
Console.WriteLine("Grafo (Mermaid):");
Console.WriteLine(workflow.ToMermaidString());
Console.WriteLine();

const string pergunta =
    "Compare a receita de outubro e novembro de 2025 e diga quais foram os top produtos em cada mes.";

Console.WriteLine($"Pergunta: {pergunta}\n");

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, pergunta);

string? autorAtual = null;
string? relatorioFinal = null;

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case AgentResponseUpdateEvent update:
            if (update.ExecutorId != autorAtual)
            {
                Console.WriteLine();
                Console.WriteLine($"──── {update.ExecutorId} ────");
                autorAtual = update.ExecutorId;
            }
            Console.Write(update.Update.Text);
            break;

        case WorkflowOutputEvent output:
            relatorioFinal = output.Data?.ToString();
            break;

        case WorkflowErrorEvent error:
            Console.WriteLine($"\nErro no workflow: {error.Exception}");
            break;
    }
}

Console.WriteLine();

if (relatorioFinal is not null)
{
    var outputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output");
    Directory.CreateDirectory(outputDir);
    var path = Path.Combine(outputDir, "relatorio-workflow.md");
    File.WriteAllText(path, relatorioFinal);

    Console.WriteLine();
    Console.WriteLine("=== Relatorio final (gerado pelo Redator dentro do workflow) ===");
    Console.WriteLine(relatorioFinal);
    Console.WriteLine($"\nRelatorio salvo em: {Path.GetFullPath(path)}");
}

// ============================================================================
// Executores customizados do grafo.
//
// Coordenador: ponto de entrada do workflow. Recebe a pergunta (string) e a
// transmite (fan-out) pros dois especialistas junto com o "sinal de partida"
// (TurnToken) — sem isso os agentes ficariam só acumulando mensagens, sem
// nunca processar.
//
// Redator: nó de fan-in. Só dispara quando os DOIS especialistas já
// responderam (a barreira de fan-in junta as duas respostas numa lista só).
// Ele chama o agente Redator com as duas análises e devolve o relatório como
// saída do workflow.
// ============================================================================

[SendsMessage(typeof(ChatMessage))]
[SendsMessage(typeof(TurnToken))]
internal sealed class Coordenador() : Executor<string>("Coordenador")
{
    [MessageHandler]
    public override async ValueTask HandleAsync(
        string pergunta, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await context.SendMessageAsync(new ChatMessage(ChatRole.User, pergunta), cancellationToken: cancellationToken);
        await context.SendMessageAsync(new TurnToken(emitEvents: true), cancellationToken: cancellationToken);
    }
}

[YieldsOutput(typeof(string))]
internal sealed class Redator(AIAgent redatorAgent) : Executor<List<ChatMessage>>("Redator")
{
    public override async ValueTask HandleAsync(
        List<ChatMessage> respostas, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var analises = string.Join("\n\n", respostas
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => $"### {m.AuthorName}\n{m.Text}"));

        var relatorio = await redatorAgent.RunAsync(
            $"Aqui estao as analises dos especialistas:\n\n{analises}\n\nEscreva o relatorio final.");

        await context.YieldOutputAsync(relatorio.Text, cancellationToken);
    }
}
