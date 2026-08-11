# Da conversa ao workflow: evoluindo agentes com o Microsoft Agent Framework (.NET)

Quatro demos em C#/.NET sobre o mesmo cenário de negócio (um "Analista de
Vendas"), cada uma adicionando uma camada diferente de controle — até trocar
de eixo na última demo: em vez de UM agente com mais controle, vários agentes
trabalhando juntos num **workflow**.

| Camada | O que adiciona | Demo |
|---|---|---|
| Agent | Modelo + tools + instruções. Tool-calling em uma tacada. | [`src/AgentSimples`](src/AgentSimples) |
| Harness | Plano, todo-list, memória em arquivo, aprovação de ferramentas, telemetria — de graça. | [`src/AgentHarness`](src/AgentHarness) |
| Loop engineering | Você projeta o critério de parada, em vez de confiar no bom senso do modelo. | [`src/LoopEngineering`](src/LoopEngineering) |
| Workflow | Vários agentes especialistas conectados num grafo (fan-out/fan-in), em vez de um agente só. | [`src/AgentWorkflow`](src/AgentWorkflow) |

Todas as demos usam o **Azure AI Foundry** via `Azure.AI.Projects`
(`AIProjectClient`) — não o client de Azure OpenAI direto.

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Um projeto no [Azure AI Foundry](https://ai.azure.com/) com um modelo já
  implantado (ex.: `gpt-5-mini`)
- Um app registration no Entra ID com permissão de acesso ao projeto
  (autenticação via `ClientSecretCredential`)

## Configuração

Cada demo tem seu próprio `.env.sample`. Copie para `.env` e preencha:

```bash
cp src/AgentSimples/.env.sample src/AgentSimples/.env
```

```env
FOUNDRY_PROJECT=https://<seu-projeto>.services.ai.azure.com/api/projects/<projeto>
DEPLOYMENT_NAME=gpt-5-mini
TENANT_ID=<tenant id do Entra ID>
CLIENT_ID=<client id do app registration>
CLIENT_SECRET=<client secret do app registration>
```

Repita para `src/AgentHarness/.env`, `src/LoopEngineering/.env` e
`src/AgentWorkflow/.env` (mesmas variáveis). O `.env` nunca é commitado (está
no `.gitignore`).

## Demo 1 — Agent simples

Um `AIAgent` puro: modelo + duas function tools. Funciona bem para pergunta
única — mas não tem plano visível, não escreve arquivo e não tem memória
entre execuções. Mostra o limite do agente "cru".

```bash
cd src/AgentSimples
dotnet run
```

## Demo 2 — Agent + Harness

O mesmo agente de negócio, envolvido pelo Harness: ganha de graça uma
todo-list, modo plano/execução, memória em arquivo e aprovação de
ferramentas. É o motor por trás de assistentes de código autônomos — o
agente escreve um `relatorio.md` de verdade em disco.

```bash
cd src/AgentHarness
dotnet run
```

> Armadilha comum: se você trocar o client para `GetResponsesClient()` e
> esquecer `ChatOptions.ModelId`, o streaming falha com
> `Missing required parameter: 'model'`.

## Demo 3 — Loop Engineering

Em vez de confiar no "bom senso" do modelo pra saber quando parou, um
`LoopAgent` envolve o mesmo agente com um `AIJudgeLoopEvaluator`: um juiz
avalia a resposta contra critérios explícitos (tabela markdown, seção de
riscos, recomendação clara) e manda de volta com feedback até passar — ou
até bater o teto de segurança (`MaxIterations`).

```bash
cd src/LoopEngineering
dotnet run
```

## Demo 4 — Workflow (multi-agente)

Aqui a demo muda de eixo: em vez de um agente só ganhando camadas de
controle, um `Workflow` do Agent Framework conecta **vários** agentes num
grafo. Dois especialistas — `AnalistaFinanceiro` e `AnalistaDeProduto` —
respondem a mesma pergunta em **paralelo** (fan-out); um terceiro nó espera
os dois terminarem (fan-in) e pede pro agente `Redator` sintetizar as duas
análises num relatório único, que também é salvo em disco.

```bash
cd src/AgentWorkflow
dotnet run
```

O programa imprime o grafo do workflow em formato Mermaid antes de rodar —
cole a saída em https://mermaid.live para visualizar o fan-out/fan-in.

## Estrutura

```
src/
  AgentSimples/       # Demo 1
  AgentHarness/        # Demo 2
  LoopEngineering/     # Demo 3
  AgentWorkflow/        # Demo 4
slides/                 # slides do vídeo
videos/                 # gravações das 4 demos rodando
```

## Licença

[MIT](LICENSE)
