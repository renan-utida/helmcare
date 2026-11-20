# Registro de uso de IA

A IA (Claude, da Anthropic) foi usada como **copiloto** para aprender, planejar e revisar, como permitido nos combinados da Global Solution. O planejamento geral do projeto (tema, Ficha, cronograma e contrato de integração) também contou com apoio de IA. As decisões são do integrante, e cada linha do repositório pode ser explicada por ele.

**Formato:** data - objetivo: prompt resumido
 
Exemplo: `08/11 - Domínio da API: "Crie as entidades do domínio seguindo o Contrato de Integração"`

---

## Arquitetura Orientada a Serviços (SOA) e Web Services
 
- 14/11 - Plano do serviço: "Monte o plano do Serviço de Risco da SOA com a rubrica do PDF, o Contrato de Integração e o modelo de README que o professor aprovou no cantina-soa."
- 14/11 - Guia do Claude Code: "Monte o servico-risco/CLAUDE.md com as decisões do Contrato 1.10, ajustado para Windows, Git Bash e segredos."
- 14/11 - Rodada 1: "Rodada 1 do servico-risco: esqueleto, domínio do índice com testes unitários, previsor XGBoost e leitura dos artefatos do modelo."
- 14/11 - Rodada 2: "Rodada 2: banco com SQLAlchemy, migrações Alembic, carga dos trechos críticos, serviços de aplicação, rotas, erros em problem+json, saúde, OpenAPI e testes."
- 14/11 - Degradação: "Se o modelo ou o banco falharem no startup, o serviço sobe mesmo assim e responde 503 só nas rotas afetadas, e a API em C# usa a versão simulada."
- 14/11 - Explicação do índice: "A explicação sai em uma frase, na ordem chuva, histórico do horário e calor, sem dizer que a chuva causa sinistros."
- 14/11 - Regras de git: "Commits e PRs sem nenhuma linha de atribuição, e mande todos os comandos de git de uma vez, incluindo PR e merge."
- 14/11 - Integração com a API: "Revise a chamada da API em C# ao Serviço de Risco, com tempo limite de 3 s e reserva simulada, e atualize o contrato para a versão 1.12."
- 14/11 - Rodada 3: "Rodada 3: README final, evidências (pytest com cobertura, Alembic, banco, chamada real, Swagger e integração com a API) e auditoria item por item contra a rubrica do PDF."
- 15/11 - Linhas sem cobertura: "Explique no README as linhas que ficaram sem cobertura e cubra as que não exigem mexer no código."
- 15/11 - Prints do Swagger: "Explique melhor o que fazer para tirar os prints 09 e 10 do Swagger."
- 15/11 - Fechamento da SOA: "Veja se ficou tudo certo, se concluímos a SOA, e monte a mensagem para o chat principal."
- 19/11 - Conferência final: "Conferência final antes da entrega: ajustar o README para a previsão real da Open-Meteo, o logo do HelmCare e o Contrato 1.17."
 
## Cybersecurity
 
- 19/11 - Plano do módulo: "Monte o plano do módulo de Cybersecurity da GS com as etapas e o que cobre cada ponto da rubrica do professor: motor de PII, Taint Analysis, IA na remediação e painel Risco Cidadão."
- 19/11 - Regras de escrita: "Textos, comentários e documentos sem travessões. Português do Brasil."
- 19/11 - Limites do Claude Code: "O Claude Code executa seguindo o cyber/CLAUDE.md e nunca faz git add, commit, push nem troca de branch, nunca abre nem imprime .env ou segredos."
- 19/11 - Fluxo de git: "Mande todos os comandos de git de uma vez, incluindo PR, merge e a próxima branch."
- 19/11 - Commits sem atribuição: "Commits sem nenhuma linha de atribuição (nada de Co-Authored-By ou menção ao Claude), nem nos commits nem nas PRs."
- 19/11 - Prazo: "Lembre que hoje é o dia da entrega: dimensione o plano para caber hoje, com o essencial primeiro e os extras marcados."
- 19/11 - Etapa 1, motor de PII: "Etapa 1 em cyber/: motor de parsing de PII para CPF, CNH, RG, telefone, e-mail, saúde, localização e senhas, com validação dos dígitos de CPF e CNH, máscaras do Contrato e testes."
- 19/11 - Etapa 2, Taint Analysis: "Etapa 2 em cyber/: Taint Analysis com marcação na fonte, política deny-by-default, modos AUDITORIA e PROTECAO, rastro explicável sem o valor original e cenários C1 a C11."
- 19/11 - Etapa 3, IA e Risco Cidadão: "Etapa 3 em cyber/: tradução dos eventos por LLM em mensagem simples para o cidadão, com reserva por tabela, cache e disjuntor, e o índice Risco Cidadão de 0 a 100 com semáforo."
- 19/11 - Etapa 4, API e painel: "Etapa 4 em cyber/: API FastAPI com interceptador de entrada e painel executivo em HTML, CSS e JS puros mostrando semáforo, mensagem e rastro."
- 19/11 - Roteiro dos prints: "Me dê tudo detalhado de onde tenho que estar para tirar os prints e detalhes, para eu tirar os prints certos."
- 19/11 - Ações do vídeo: "Quero que você me explique detalhadamente as ações que são para eu tomar em cada trecho, não precisa alterar nada de ação, nem fala, só as ações mais detalhadas para eu realizar as ações."
- 19/11 - Evidências no repositório: "Vamos pedir para ele pegar os prints e o vídeo da pasta Downloads, renomear e jogar dentro de evidencias; o vídeo terá só legendas, sem narração, igual ao que fiz no Mobile."
- 19/11 - Etapa 6, relatório e ZIP: "Etapa 6 em cyber/: auditoria do protótipo, relatório em PDF com as figuras das evidências e ZIP da entrega."
- 19/11 - Organização das evidências: "Em evidencias, jogaria os png dentro de uma pasta chamada prints, e o video dentro de uma pasta chamada video. Nas documentações queria que ele usasse a marca do nosso projeto."
- 19/11 - Sumário e links: "No README raiz de Cyber tinha que ter um Sumário, e o link do YouTube pode ficar lá em cima junto com o link do repo."
- 19/11 - Texto da entrega: "Ele não montou um txt com o meu nome e o link do youtube. Consegue montar?"
- 19/11 - Fechamento com a central: "Monte a mensagem completa para a central, para atualizar ela."
 
## C# Software Development
 
*(a preencher)*
 
## Inteligência Artificial & Machine Learning
 
- 07/11 - Kickoff do notebook: "Cada item tem Markdown explicando antes do código e uma discussão depois dos outputs, sem travessões. Estime o tempo das etapas pesadas, salve os resultados para retomar de onde parou e confira o relatório e as figuras contra os outputs"
- 07/11 - Rubrica e roteiro: "Leia o PDF da atividade, monte o mapa da rubrica e proponha os itens do notebook"; aprovei os 13 itens com o alvo, o ano fora do modelo e a regra de feriados
- 08/11 - Discussões com números reais: "Seguem os outputs deste item: escreva a discussão só com os números que saíram"
- 08/11 - Validação do alvo: "Confira o alvo contra os números oficiais de mortes no trânsito da capital em 2025"
- 14/11 - Itens 6 a 9: "Divisão temporal, linhas de base, grade de 27 combinações com dobras anuais só no treino e análise de overfitting"
- 14/11 - Decisão da central: "O modelo exportado usa só o calendário, justificado por robustez e parcimônia, já que o tempo não melhorou a validação"
- 14/11 - Item 10: "Grade do calendário lado a lado com a de 7 variáveis, os 4 artefatos do Contrato, o P95 calculado dos dois jeitos, os trechos críticos, o teste do exemplo do Contrato e a função hora a hora do turno"
- 14/11 - Ajustes de figura: "Corrija a legenda do 9.1 e ponha o mapa do OpenStreetMap ao fundo dos trechos críticos"
- 19/11 - Item 11: "Leitura para o cidadão e conferência das coordenadas da SP 270, com a regra de suspeita definida antes de olhar os dados"
- 19/11 - Fechamento: "Relatório, READMEs, requirements e roteiro do vídeo"
 
## Mobile Development & IoT

- 15/11 - Guia da pasta: "Monte um CLAUDE.md para a pasta mobile com a stack, as regras do contrato, a acessibilidade e os cuidados com dados pessoais"
- 15/11 - Base do app: "Crie o projeto Expo SDK 58 com Expo Router e TypeScript, os tipos do contrato, o cliente Axios e o SQLite com migrações"
- 15/11 - Tema e marca: "Monte um tema acessível com as cores da marca, conferido no contraste WCAG, e use o logo nas telas"
- 15/11 - Turno e alerta: "Faça o início do turno com o Índice de Risco, o alerta de queda com contagem e a ocorrência da vítima com Ligar 192"
- 16/11 - Rede e histórico: "Implemente a rede de voluntários com chamados e aceite e o histórico offline no SQLite"
- 16/11 - Acessibilidade: "Revise todas as telas: área de toque, leitor de tela, contraste e fonte grande"
- 17/11 - Login: "Adapte o app ao login da API: e-mail e senha, até 3 contas no aparelho e tokens no SecureStore"
- 18/11 - Ficha e Cofre: "Mostre a ficha médica, o QR Code do capacete e quem acessou a ficha"
- 18/11 - Experiência: "Melhore a aba Início com o clima, a contagem circular, as notificações locais e as paletas de aparência"
- 18/11 - Revisão: "Revise o visual das telas e corrija a notificação no Expo Go"
- 18/11 - Documentação: "Escreva o README do mobile com a Ficha, a arquitetura, os endpoints e como rodar"
- 19/11 - Vídeo: "Coloque legendas curtas no vídeo do app e monte a descrição e a miniatura do YouTube"
 
## Operating Systems
 
*(a preencher)*
 
## Physical Computing: IoT & IoB
 
*(a preencher)*
 
## Testing, Compliance & Quality Assurance
 
*(a preencher)*