# Agent Dev Guide

## Objetivo

Este documento descreve como orquestrar um agente de código de alta performance de forma abstrata, independente de framework, com foco em implementação por outra LLM em C#. A meta é definir arquitetura, contratos, ciclo de execução, tool calling, segurança, observabilidade, gestão de contexto, paralelismo e critérios de qualidade sem prender a solução a um fornecedor específico.

---

## 1. Princípios do Orquestrador

### 1.1 Responsabilidades centrais

Um orquestrador de agente de código bem otimizado deve:

- transformar uma intenção do usuário em um plano executável;
- decidir quando raciocinar, quando agir e quando encerrar;
- selecionar ferramentas adequadas para cada subtarefa;
- controlar contexto, custos, latência e risco;
- validar resultados antes de responder;
- operar com rastreabilidade total.

### 1.2 Propriedades desejadas

A implementação deve priorizar:

- previsibilidade;
- idempotência quando possível;
- isolamento entre etapas;
- segurança por padrão;
- recuperação após erro;
- execução incremental;
- observabilidade fina;
- facilidade de extensão.

### 1.3 Modelo mental

Pense no agente como um sistema de controle em loop:

1. receber estado atual;
2. interpretar objetivo;
3. decidir próxima ação ótima;
4. executar ação;
5. observar resultado;
6. atualizar estado;
7. repetir até concluir ou bloquear.

Esse loop deve ser explícito no design, nunca implícito ou espalhado entre componentes sem contrato claro.

---

## 2. Arquitetura em camadas

### 2.1 Camadas recomendadas

Uma arquitetura robusta pode ser organizada assim:

1. **Interface de entrada**
   - CLI, API, gRPC, fila, IDE extension ou webhook.
2. **Camada de sessão**
   - identidade da execução, permissões, diretório de trabalho, limites, políticas.
3. **Camada de planejamento**
   - decompõe objetivo em passos, avalia dependências e risco.
4. **Camada de decisão**
   - escolhe entre responder, chamar ferramenta, pedir confirmação ou encerrar.
5. **Camada de execução de ferramentas**
   - adaptadores para filesystem, shell, busca, web, VCS, compilação, testes, LSP.
6. **Camada de memória e contexto**
   - histórico, resumos, fatos persistidos, estado intermediário, cache semântico.
7. **Camada de validação**
   - testes, lint, typecheck, verificações estruturais, políticas de segurança.
8. **Camada de observabilidade**
   - traces, métricas, eventos, custos, latência, taxa de erro, auditoria.

### 2.2 Separação de responsabilidades

A LLM não deve ser a única fonte de verdade do fluxo. Ela deve propor ações dentro de restrições impostas por código determinístico. Em outras palavras:

- a LLM decide semanticamente;
- o orquestrador governa operacionalmente.

Isso reduz alucinação operacional, melhora segurança e facilita testes automatizados.

---

## 3. Modelo de execução do agente

### 3.1 Loop principal

O loop principal deve seguir uma máquina de estados explícita. Estados comuns:

- `Idle`
- `Planning`
- `AwaitingModel`
- `AwaitingToolExecution`
- `AwaitingUserApproval`
- `Validating`
- `Summarizing`
- `Completed`
- `Failed`
- `Blocked`
- `Cancelled`

### 3.2 Ciclo ideal

1. Normalizar a solicitação.
2. Carregar políticas, memória e contexto do projeto.
3. Determinar se a tarefa exige plano formal.
4. Montar prompt estruturado para decisão.
5. Invocar modelo.
6. Interpretar saída em formato estruturado.
7. Se houver tool calls, validar cada uma.
8. Executar ferramentas.
9. Registrar resultados e reduzir contexto quando necessário.
10. Repetir até atingir critério de parada.
11. Rodar validações finais.
12. Emitir resposta final e artefatos.

### 3.3 Critérios de parada

O agente só deve encerrar quando pelo menos uma condição for verdadeira:

- objetivo foi satisfeito e validado;
- tarefa ficou externamente bloqueada;
- política negou continuidade;
- orçamento de custo, tempo ou tokens foi atingido;
- usuário cancelou;
- risco excedeu limite configurado.

Critérios de parada devem ser codificados, não deixados ao “feeling” do modelo.

---

## 4. Contratos estruturados entre LLM e runtime

### 4.1 Por que estruturar tudo

Uma LLM produz texto probabilístico. Um runtime confiável precisa de estruturas determinísticas. Portanto, a camada de integração deve exigir saídas fortemente tipadas para:

- decisões;
- planos;
- pedidos de ferramenta;
- respostas finais;
- relatórios de validação;
- erros recuperáveis e não recuperáveis.

### 4.2 Tipos conceituais importantes

Defina contratos equivalentes a estes conceitos:

- `AgentRequest`
- `AgentSession`
- `ModelTurn`
- `ToolCall`
- `ToolResult`
- `ExecutionStep`
- `PlanItem`
- `ValidationResult`
- `SafetyDecision`
- `CompletionReport`

### 4.3 Recomendação para C#

Para outra LLM implementar isso em C#, o desenho abstrato favorece:

- `record` para mensagens imutáveis;
- `enum` para estados e tipos de evento;
- interfaces para adaptadores (`IModelClient`, `IToolExecutor`, `IContextStore`, `IValidationRunner`);
- `CancellationToken` em todas as operações assíncronas;
- pipelines assíncronos com `Task` e, quando útil, `IAsyncEnumerable<T>` para streaming.

A modelagem precisa privilegiar tipos pequenos, explícitos e facilmente serializáveis em JSON.

---

## 5. Tool Calling

### 5.1 Conceito

Tool calling é o mecanismo pelo qual a LLM pede ao runtime que execute ações externas. Em um agente de código, isso normalmente inclui:

- leitura e escrita de arquivos;
- edição pontual;
- busca por conteúdo;
- listagem de diretórios;
- execução de comandos;
- execução de testes;
- consultas web;
- busca por símbolos e referências;
- operações de Git;
- chamadas a serviços externos controlados.

### 5.2 Separar intenção de execução

O modelo nunca deve executar diretamente. Ele apenas emite uma intenção estruturada como:

- nome da ferramenta;
- argumentos validados;
- motivo da chamada;
- grau de confiança;
- prioridade;
- impacto esperado.

O runtime:

- valida schema;
- verifica política;
- aplica limites;
- executa;
- normaliza saída;
- devolve resultado à LLM.

### 5.3 Requisitos do contrato de ferramenta

Cada ferramenta deve ter:

- identificador estável;
- descrição curta e objetiva;
- schema de entrada;
- schema de saída;
- política de segurança;
- timeout;
- política de retry;
- indicador de idempotência;
- custo estimado;
- capacidade de streaming ou não.

### 5.4 Formato recomendado de ToolCall

Abstratamente, um `ToolCall` deve conter:

- `ToolName`
- `CallId`
- `Arguments`
- `ReasoningSummary`
- `RiskLevel`
- `TimeoutBudget`
- `RequiresApproval`
- `CorrelationId`

### 5.5 Normalização de resultados

Um `ToolResult` ideal contém:

- sucesso/falha;
- saída primária;
- stderr ou diagnósticos;
- metadados;
- duração;
- código de saída;
- indicadores de truncamento;
- artefatos gerados;
- classificação do erro.

### 5.6 Tool registry

Tenha um registro central de ferramentas com lookup por nome e capacidades. Esse registry deve suportar:

- descoberta dinâmica;
- versionamento;
- feature flags;
- políticas por ambiente;
- métricas por ferramenta;
- fallback ou substituição de implementação.

### 5.7 Estratégia de seleção de ferramenta

A LLM tende a escolher melhor quando recebe:

- descrição precisa das ferramentas;
- exemplos positivos e negativos;
- limitações de cada uma;
- ordem de preferência;
- heurísticas de quando usar busca, leitura, edição, shell, LSP ou web.

Isso reduz loops improdutivos e chamadas desnecessárias.

---

## 6. Planejamento e decomposição de tarefas

### 6.1 Quando planejar

Nem toda tarefa precisa de plano explícito. Um bom orquestrador detecta se a demanda é:

- trivial;
- multi-etapas;
- de alto risco;
- de alto custo;
- ambígua;
- dependente de validação.

Para tarefas complexas, o agente deve gerar um plano estruturado antes de agir.

### 6.2 Propriedades de um bom plano

Cada item do plano deve ter:

- objetivo claro;
- pré-condições;
- dependências;
- resultado esperado;
- critério de verificação;
- status (`pending`, `in_progress`, `completed`, `blocked`, `skipped`).

### 6.3 Planejamento adaptativo

O plano não pode ser rígido. Após cada tool result, o agente deve poder:

- refinar o próximo passo;
- dividir um item em subtarefas;
- remover etapas irrelevantes;
- inserir validações extras;
- encerrar cedo se o objetivo já tiver sido satisfeito.

### 6.4 Evitar overplanning

Planejamento excessivo aumenta latência e custo. A regra prática é:

- planejar mais em tarefas caras ou perigosas;
- planejar menos em operações locais, pequenas e reversíveis.

---

## 7. Gestão de contexto

### 7.1 Problema central

Em agentes de código, contexto ruim gera:

- alucinação estrutural;
- edições em arquivo errado;
- repetição de buscas;
- perda de restrições do usuário;
- desperdício de tokens.

### 7.2 Fontes de contexto

O orquestrador deve compor contexto a partir de:

- prompt do usuário;
- instruções do sistema;
- políticas do ambiente;
- histórico recente;
- memória persistida;
- estado do plano;
- resultados recentes de ferramentas;
- resumo do repositório;
- arquivos abertos ou relevantes;
- erros recentes e tentativas anteriores.

### 7.3 Estratégia de janelas

Use contexto em camadas:

1. **hot context**
   - dados essenciais do turno atual.
2. **warm context**
   - resumos e fatos persistentes úteis.
3. **cold context**
   - histórico arquivado e recuperável sob demanda.

### 7.4 Redução de contexto

O sistema deve resumir automaticamente:

- turnos antigos;
- outputs extensos de ferramentas;
- logs repetitivos;
- resultados já consolidados.

A compressão deve preservar:

- decisões tomadas;
- arquivos alterados;
- comandos executados;
- falhas relevantes;
- próximos passos válidos.

### 7.5 Context engineering

O agente deve receber apenas o necessário para a próxima decisão. Mais contexto nem sempre melhora qualidade; muitas vezes piora foco. Um bom runtime filtra, deduplica e prioriza informação.

---

## 8. Memória

### 8.1 Tipos de memória

Separar memória por função ajuda muito:

- **memória de sessão**: fatos válidos só para a execução atual;
- **memória de projeto**: comandos, convenções, padrões, caminhos frequentes;
- **memória de usuário**: preferências persistentes;
- **memória operacional**: histórico de decisões e tentativas;
- **cache de artefatos**: snippets, resumos, embeddings, índices.

### 8.2 O que vale persistir

Persistir apenas o que gera valor recorrente:

- comandos de build, test e lint;
- convenções de nomenclatura;
- restrições de segurança;
- topologia do projeto;
- arquivos ou módulos críticos;
- dicas de recuperação de erro.

### 8.3 O que não vale persistir

Evite persistir:

- outputs volumosos sem reutilização;
- segredos;
- estados transitórios sem valor futuro;
- dados derivados facilmente recomputáveis;
- deduções incertas apresentadas como fato.

### 8.4 Memória como dado auditável

Toda memória persistida deve ser:

- versionável;
- invalidável;
- atribuível a uma origem;
- fácil de inspecionar;
- fácil de sobrescrever por fatos mais recentes.

---

## 9. Edição de código e segurança operacional

### 9.1 Regras para edição confiável

Um agente de código seguro deve:

- ler antes de editar;
- editar com correspondência exata;
- preservar indentação e estilo local;
- evitar mudanças colaterais fora do escopo;
- validar logo após cada modificação.

### 9.2 Estratégias de edição

As três estratégias clássicas são:

1. **substituição exata com contexto**
   - ótima para mudanças pequenas e seguras;
2. **reescrita total de arquivo**
   - útil quando a transformação é ampla;
3. **edição estruturada por AST**
   - ideal quando existe parser confiável.

### 9.3 Política recomendada

Use a estratégia mais restritiva que ainda resolva a tarefa:

- primeiro, edição localizada;
- depois, bloco maior;
- por último, reescrita completa.

### 9.4 Guardrails para shell e filesystem

Ferramentas com impacto alto precisam de:

- allowlist de comandos;
- denylist explícita para operações perigosas;
- sandbox por workspace;
- timeouts;
- limite de output;
- confirmação do usuário quando necessário;
- trilha de auditoria.

---

## 10. Validação contínua

### 10.1 Validação não é opcional

Um agente de código sem validação vira um gerador de diffs plausíveis, mas não confiáveis. O orquestrador precisa validar durante e no fim.

### 10.2 Níveis de validação

Valide em camadas:

1. **sintática**
   - parse, compilação, schema.
2. **semântica local**
   - testes focados, linter, typecheck.
3. **semântica ampliada**
   - testes de integração, smoke tests.
4. **política**
   - segurança, licenças, conformidade.
5. **qualidade de entrega**
   - diff coerente, resposta final consistente.

### 10.3 Estratégia ótima

Depois de cada mudança relevante:

- rodar validação mínima específica;
- em seguida ampliar a cobertura se o risco justificar.

Isso reduz custo comparado a rodar a suíte completa a cada passo, sem perder segurança.

### 10.4 Validação orientada a hipótese

Se o agente acredita ter corrigido um bug específico, a primeira validação deve provar ou refutar essa hipótese com o menor custo possível.

---

## 11. Tratamento de erros e recuperação

### 11.1 Classificação de erros

Classifique erros em categorias como:

- erro do modelo;
- erro de schema;
- erro de ferramenta;
- erro de permissão;
- erro transitório externo;
- erro de política;
- erro de validação;
- bloqueio por falta de contexto;
- erro de orquestração.

### 11.2 Estratégia de recuperação

Para cada classe, defina ação padrão:

- retry com backoff;
- reformular prompt;
- trocar ferramenta;
- reduzir escopo;
- pedir aprovação;
- fazer fallback de modelo;
- encerrar com bloqueio explícito.

### 11.3 Não repetir loops inúteis

O runtime deve detectar padrões como:

- mesma ferramenta chamada com argumentos equivalentes;
- mesma falha recorrente;
- plano sem progresso real;
- alternância improdutiva entre leitura e busca.

Quando isso ocorrer, o sistema deve forçar replanning ou encerrar com diagnóstico claro.

---

## 12. Paralelismo e concorrência

### 12.1 Onde paralelizar

Paralelismo ajuda muito em:

- buscas independentes em arquivos;
- leitura de múltiplos artefatos;
- coleta de diagnósticos independentes;
- consultas a índices;
- execuções de agentes subordinados especializados.

### 12.2 Onde evitar

Evite paralelizar quando houver:

- dependência entre passos;
- escrita no mesmo recurso;
- risco de corrida em contexto compartilhado;
- validação que exige ordem causal.

### 12.3 Estratégia de subagentes

Subagentes funcionam bem quando cada um recebe:

- objetivo único;
- escopo delimitado;
- budget próprio;
- formato de saída obrigatório;
- nenhuma expectativa de memória compartilhada implícita.

O orquestrador principal deve agir como supervisor, consolidando os relatórios.

### 12.4 Concorrência em C#

Para implementação por outra LLM em C#, a arquitetura deve prever:

- `Task.WhenAll` para operações independentes;
- semáforos para limitar concorrência de IO e processos;
- canais ou filas assíncronas para eventos;
- proteção para estado compartilhado;
- cancelamento cooperativo com `CancellationToken`.

---

## 13. Otimização de custo e latência

### 13.1 Leis práticas

Custo e latência geralmente explodem por:

- contexto excessivo;
- tool calls desnecessárias;
- retries cegos;
- validação ampla demais cedo demais;
- uso do modelo mais caro para tudo.

### 13.2 Estratégias úteis

- rotear tarefas por classe para modelos diferentes;
- usar modelos menores para triagem, busca e classificação;
- reservar modelos mais fortes para planejamento, síntese e decisões difíceis;
- resumir outputs longos antes de reencaminhar à LLM;
- cachear resultados determinísticos;
- abortar cedo quando hipótese falhou claramente.

### 13.3 Budgeting

Toda sessão deve ter budgets explícitos:

- tokens de input;
- tokens de output;
- tempo total;
- chamadas de ferramenta;
- custo monetário estimado;
- número máximo de iterações.

### 13.4 Degradação graciosa

Quando o orçamento apertar, o agente deve conseguir:

- reduzir contexto;
- diminuir nível de detalhamento;
- trocar para validação mais específica;
- suspender subtarefas não essenciais;
- reportar limitações de forma objetiva.

---

## 14. Observabilidade

### 14.1 Eventos que devem existir

O sistema deve emitir eventos estruturados para:

- início e fim de sessão;
- turnos do modelo;
- tool calls;
- resultados de ferramenta;
- mudanças de plano;
- validações;
- bloqueios;
- erros;
- custos e duração;
- resposta final.

### 14.2 Rastreamento

Cada execução precisa de IDs correlacionáveis:

- `SessionId`
- `TurnId`
- `StepId`
- `ToolCallId`
- `CorrelationId`
- `ParentSpanId`

### 14.3 Métricas importantes

Meça ao menos:

- latência por turno;
- latência por ferramenta;
- taxa de sucesso por ferramenta;
- taxa de retry;
- número médio de iterações por tarefa;
- custo por sessão;
- tokens por etapa;
- taxa de bloqueio;
- taxa de validação bem-sucedida;
- tempo até primeira ação útil.

### 14.4 Logs versus eventos

Prefira eventos estruturados a logs livres. Logs textuais ajudam em depuração humana, mas eventos tipados permitem dashboards, alertas, replay e análise de qualidade.

---

## 15. Políticas de segurança e governança

### 15.1 Controle por política

Toda execução deve passar por um policy engine independente da LLM. Esse engine decide:

- o que pode ser lido;
- o que pode ser escrito;
- quais comandos podem rodar;
- quais domínios externos podem ser acessados;
- quando aprovação humana é obrigatória;
- como lidar com segredos e PII.

### 15.2 Aprovação humana

Exija aprovação explícita para ações como:

- comando destrutivo;
- acesso fora do workspace;
- modificação em massa;
- rede externa sensível;
- operações com credenciais;
- mudanças em arquivos críticos de produção.

### 15.3 Redação e sanitização

A saída de ferramentas deve ser filtrada para evitar:

- vazamento de segredos;
- expansão de logs irrelevantes;
- prompt injection vindo de arquivos ou web;
- conteúdo hostil que altere o comportamento do agente.

### 15.4 Prompt injection

Um agente de código moderno precisa tratar dados lidos do mundo externo como não confiáveis. Texto vindo de arquivos, web, tickets, logs ou commits não deve alterar políticas internas nem instruções prioritárias.

---

## 16. Estratégia de prompts do sistema

### 16.1 O que o prompt do sistema deve fazer

O prompt-base deve:

- definir prioridade entre instruções;
- descrever o fluxo operacional esperado;
- listar limites e políticas;
- orientar seleção de ferramentas;
- impor critérios de qualidade;
- especificar formato de saída.

### 16.2 O que evitar

Evite prompts-base:

- excessivamente verbosos sem ganho operacional;
- contraditórios;
- vagos sobre quando usar ferramentas;
- dependentes de linguagem humana ambígua para regras críticas.

### 16.3 Prompting modular

Em vez de um prompt monolítico, componha:

- instruções globais;
- instruções do ambiente;
- instruções da tarefa;
- contexto relevante;
- catálogo de ferramentas;
- estado do plano;
- resultados recentes.

Isso torna a orquestração mais auditável e adaptável.

---

## 17. Estratégia de modelos

### 17.1 Roteamento por capacidade

Uma arquitetura otimizada pode usar modelos distintos para:

- classificação inicial;
- planejamento;
- execução geral;
- sumarização;
- revisão final;
- subagentes especializados.

### 17.2 Seleção dinâmica

O roteador deve considerar:

- custo;
- latência;
- janela de contexto;
- qualidade em tool calling;
- robustez a JSON estruturado;
- taxa histórica de sucesso por tipo de tarefa.

### 17.3 Fallbacks

Sempre tenha fallback quando possível:

- se um modelo falhar em JSON estruturado, tentar outro mais confiável;
- se um modelo for lento, usar outro para subtarefas menores;
- se um provedor estiver indisponível, degradar com transparência.

---

## 18. Testabilidade do orquestrador

### 18.1 O que testar

A implementação deve ser testável sem depender de uma LLM real em todos os casos. Crie testes para:

- transições da máquina de estados;
- policy engine;
- normalização de tool calls;
- retries e timeouts;
- budget enforcement;
- redução de contexto;
- paralelismo controlado;
- replay de sessões.

### 18.2 Simulação

Tenha dublês para:

- cliente de modelo;
- executor de ferramenta;
- armazenamento de contexto;
- relógio;
- gerador de IDs;
- camada de observabilidade.

### 18.3 Replay determinístico

Sessões reais devem poder ser reexecutadas em modo replay com respostas gravadas. Isso é valioso para regressão, benchmark e depuração.

---

## 19. Formato de eventos e streaming

### 19.1 Streaming como cidadão de primeira classe

O agente deve suportar streaming desde o início do design. Não trate streaming como detalhe de UI. Ele afeta:

- percepção de latência;
- cancelamento;
- progresso;
- aprovação humana;
- telemetria;
- composição com subagentes.

### 19.2 Tipos de evento sugeridos

Uma API de streaming pode emitir eventos como:

- `text_delta`
- `thinking_summary`
- `tool_call_requested`
- `tool_call_started`
- `tool_call_completed`
- `validation_started`
- `validation_completed`
- `plan_updated`
- `warning`
- `blocked`
- `completed`
- `failed`

### 19.3 Benefício arquitetural

Se a própria engine já trabalha em eventos, fica mais simples expor CLI, gRPC, WebSocket, SSE e integrações IDE sem duplicar a lógica central.

---

## 20. Padrão de implementação abstrato para C#

### 20.1 Componentes conceituais

Uma LLM implementando isso em C# deve estruturar a solução em componentes próximos destes papéis:

- `AgentOrchestrator`
- `SessionManager`
- `PromptBuilder`
- `ModelRouter`
- `ModelClient`
- `ToolRegistry`
- `ToolExecutor`
- `PolicyEngine`
- `ContextAssembler`
- `MemoryStore`
- `ValidationCoordinator`
- `EventStreamPublisher`
- `ExecutionRecorder`

### 20.2 Regras de design

- preferir composição a herança;
- manter contratos pequenos;
- isolar integração externa em adaptadores;
- separar domínio de infraestrutura;
- tornar serialização explícita;
- evitar estado global implícito.

### 20.3 Pipeline sugerido

Um pipeline abstrato em C# pode seguir:

1. criar sessão;
2. montar contexto;
3. escolher modelo;
4. solicitar próximo passo ao modelo;
5. validar output estruturado;
6. executar ferramenta ou ação interna;
7. atualizar estado e memória;
8. validar progresso;
9. repetir;
10. consolidar resultado final.

### 20.4 Interfaces mínimas úteis

A implementação final deve prever interfaces equivalentes a:

- uma interface para modelos;
- uma interface para ferramentas;
- uma interface para autorização/política;
- uma interface para armazenamento de contexto e memória;
- uma interface para validadores;
- uma interface para publicação de eventos.

Isso facilita testes, troca de provedores e evolução do sistema.

---

## 21. Anti-padrões

Evite estes erros comuns:

- deixar a LLM controlar diretamente o runtime;
- misturar prompt, regra de negócio e política de segurança no mesmo lugar;
- serializar contexto sem estratégia de poda;
- usar texto livre para comandos que deveriam ser estruturados;
- não distinguir erro transitório de erro permanente;
- não registrar a cadeia causal de decisões;
- permitir que outputs externos reescrevam instruções do sistema;
- validar apenas no final;
- não impor budgets;
- executar ações perigosas sem política explícita.

---

## 22. Checklist de um agent de código otimizado

Um agent maduro deve conseguir:

- entender tarefas abertas e específicas;
- localizar contexto relevante rapidamente;
- decompor trabalho em passos objetivos;
- escolher ferramentas com parcimônia;
- operar com outputs estruturados;
- editar código com segurança;
- validar incrementalmente;
- recuperar-se de falhas comuns;
- controlar custo e latência;
- registrar tudo para auditoria e replay;
- aplicar políticas fora da LLM;
- escalar para subagentes quando necessário.

---

## 23. Resumo executivo

A melhor forma de orquestrar um agente de código não é pedir que a LLM “faça tudo”, e sim colocá-la dentro de um runtime rigoroso. Esse runtime deve ter máquina de estados, contratos tipados, tool calling validado, política de segurança independente, gestão ativa de contexto, validação contínua, observabilidade completa e budgets explícitos.

Para uma implementação em C#, o caminho mais sólido é desenhar um núcleo orientado a interfaces, eventos e tipos imutáveis, com forte separação entre decisão semântica da LLM e execução determinística do runtime. Essa combinação produz um agente mais rápido, mais seguro, mais barato de operar e muito mais confiável em produção.
