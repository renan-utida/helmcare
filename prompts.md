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
 
- 08/11 - Commits manuais: "Chat, vou mandar o pedido da Etapa 1 para ele. Não quero que ele faça commits por conta própria, pode deixar que eu faço manualmente aqui."
- 08/11 - Pedidos ao Claude Code (etapas 1 a 4): solução em camadas, domínio com a máquina de estados da Ocorrencia, EF Core com a primeira migration e testes MSTest, seguindo o api/CLAUDE.md.
- 08/11 - Resumo para a central: "Chat, veja se está tudo certo para mandarmos para o Chat principal tudo o que fizemos?"
- 12/11 - Indicador de impacto (central): "MUDANÇA DE CONTRATO aprovada como proposta. Mantenha o cálculo isolado no método único, como você propôs, e cubra com testes MSTest. No README da API, explique a regra em duas linhas, porque é uma boa decisão de projeto para mostrar ao professor."
- 14/11 - Integração com o Serviço de Risco: "Chat, voltando para a API. Já estou na feature/api-integracao-risco, criada a partir da main atualizada. Escopo desta rodada: fecha a tarefa API C# chamando o Serviço de Risco, cujo entregável é o índice calculado de ponta a ponta. Como sempre: primeiro o plano da rodada, depois o pedido para o Claude Code seguindo o api/CLAUDE.md."
- 14/11 - Pedido ao Claude Code (Serviço de Risco): cliente HTTP com reserva na versão simulada, simulada alinhada ao modelo real, GET /trechos-criticos e a evidência com o serviço no ar e fora.
- 15/11 - SOS escalonado: "Chat, vamos para a tarefa de Quinta (12/11), API: SOS escalonado (camada 1, camada 2 após 60 s sem aceite, aceite e cronômetro). Como sempre: mande a branch no começo, depois o plano da etapa, e só então o pedido para o Claude Code seguindo o api/CLAUDE.md. Pode quebrar em rodadas se ficar grande."
- 15/11 - Pedidos ao Claude Code (SOS escalonado, rodadas 1 e 2): rede de voluntários, escalonamento em segundo plano, aceite, cancelar e encerrar, comando refeito no SOS repetido e aviso simulado ao contato, com o SOS publicado no HiveMQ.
- 15/11 - Revisão da rodada: "Chat, segue a rodada 1, veja se ele fez tudo certo e conforme esperado?"
- 15/11 - Print no README: "Quer adicionar esses prints (ou um desses) no README de C# já, chat? Só que trocaríamos o nome e criaríamos uma pasta evidencias/ para jogar ele(s)."
- 16/11 - Identidade e Cofre: "Nova etapa: Identidade e Cofre. Decisão nova da central: o entregador passa a ter login com senha na própria API. Isto é MUDANÇA DE CONTRATO. Proponha o desenho no plano, com as rotas, os payloads e os nomes dos campos, que a central publica a 1.15 antes do merge."
- 17/11 - Pedido da rodada 1: "A central publicou o contrato 1.15, já commitado como o primeiro commit da feature/api-identidade. Pode montar o pedido da rodada 1 para o Claude Code."
- 17/11 - Pedidos ao Claude Code (identidade): Identity com token bearer, login, renovação e sessão, autorização em todas as rotas, pedidos do app e a migration Identidade, e depois o ponta a ponta com um SOS real.
- 17/11 - Ambiente para o Claude Code: "Tem que deixar algo rodando ou nada rodando para ele executar?"
- 17/11 - Cofre médico: "Pode abrir a rodada 2, o Cofre. Se algo do contrato não fechar, MUDANÇA DE CONTRATO antes de codar. A rodada 3 (Open-Meteo e GET /clima) fica fora por enquanto. Tenho uma dúvida: é fácil implementação? Ou algo que mexeria muito no todo?"
- 17/11 - Pedidos ao Claude Code (Cofre): login do socorrista, janela de acesso, auditoria, página web e a migration Cofre, e depois o ponta a ponta com o print da página no celular.
- 17/11 - Ajuste do critério: "Se rodada/etapa estiver se referindo a algo no APP, sem problema, mas se for algo da nossa rotina de código aqui, aí acho que precisava remover."
- 17/11 - Print do Cofre: "Nomeei como cofre-pagina-app e cofre-pagina-web. Vê se você acha bom ter esse web, ou só o app (que é um mobile)."
- 17/11 - Pedido ao Claude Code (clima): previsão real pela Open-Meteo no turno com reserva e previsão simulada forçada, GET /clima com cache de 10 min e 503, e o ponta a ponta com a internet real.
- 17/11 - Turno de teste preso: "Me passe um comando único de psql pelo docker exec, no banco de desenvolvimento helmcare_api, que mostre antes quantos turnos abertos essa conta tem, feche só esse turno, com o fim igual ao início, sem apagar nada, e confira depois que a conta ficou sem turno aberto."
 
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

- 06/11 - Leitura dos materiais: "Vou te mandar os materiais do semestre passado e deste semestre de Physical Computing, incluindo os checkpoints. Por enquanto, só leia e confirme que entendeu."
- 06/11 - Resumo de estudo: "Monte um resumo do que vimos no geral nas aulas e nos checkpoints, para a gente se preparar para a Global Solution."
- 07/11 - Critérios e plano: "Este é o enunciado da GS e o projeto HelmCare. Mapeie os critérios da disciplina e proponha o plano do circuito, os componentes, os pinos e as etapas, seguindo a Ficha e o Contrato."
- 07/11 - Etapa 1, circuito e sensores: "Pode mandar o diagram.json, o config.h, o sketch.ino de leitura dos sensores, o libraries.txt e todos os comandos de git de uma vez."
- 08/11 - Calibração do impacto: "Os eixos do MPU6050 no Wokwi vão só de -2 g a 2 g. Me explique o Inc, a temperatura e qual limiar de impacto faz sentido para a simulação."
- 08/11 - README da etapa 1: "Atualize o iot/README.md com o circuito, os pinos, como rodar e o lado do slide switch (fio preto = capacete na cabeça)."
- 09/11 - Etapa 2, máquina de estados e MQTT: "O Contrato chegou à versão 1.6. Vamos para a etapa 2: a máquina de estados, os eventos, os comandos e o reenvio do SOS por MQTT."
- 10/11 - MQTT caindo: "As etapas do teste passaram, mas o MQTT cai. Pensei em client.setKeepAlive(60). Está certo? E não entendi como testar a queda."
- 10/11 - Prints das evidências: "Mandei o vídeo dos testes. Consegue montar os prints necessários e me mandar? E está tudo certo agora?"
- 10/11 - Tutorial dos testes: "Mantenha o código anterior do relógio e me mande um tutorial completo de como eu testo cada situação, de maneira bem descritiva."
- 11/11 - Qualidade dos prints: "Gravei os testes 1, 2, 3A e 3B. Consegue melhorar a qualidade das imagens?"
- 11/11 - README final: "Atualize o README de forma completa e intuitiva, com os prints, um sumário no começo, um link para voltar ao topo no fim de cada seção e o meu nome e a disciplina numa tabela."
- 12/11 - Horários e desempenho: "Explique no README que os horários estão em UTC, confira se o OLED é redesenhado sem necessidade e meça o desempenho da simulação antes e depois dos ajustes."
- 12/11 - Resultado das medições: "Este é o resultado do teste de desempenho. Atualize o README e me mande os arquivos alterados e os comandos de git de uma vez."
- 19/11 - Roteiro e plano B: "Monte o roteiro do vídeo de até 4 minutos, o checklist de gravação e um plano B pelo HiveMQ caso a API não suba."
- 19/11 - Entrega: "Tudo cumprido do que foi solicitado? Monte o .txt do entregável e a mensagem para a Central."
 
## Testing, Compliance & Quality Assurance
 
- 04/11 - Contexto da disciplina: "Preciso de ajuda com a GS de Testing, Compliance and QA do Prof. Salatiel. Vou te mandar os materiais do Prof. Thiago, os checkpoints e as aulas do Salatiel. Por enquanto, só leia e confirme que entendeu."
- 06/11 - Enunciado e plano: "Este é o enunciado da GS e o projeto HelmCare. Mapeie o que a disciplina pede (Features e Test Cases no Azure) e proponha o plano, seguindo a Ficha e o Contrato."
- 06/11 - Projeto no Azure: "Como crio o projeto HelmCare no Azure DevOps com processo Scrum e adiciono o professor como Admin?"
- 07/11 - Estrutura do backlog: "Proponha os Épicos, as Features e os PBIs do HelmCare no padrão do material do professor, com critérios de aceite."
- 08/11 - Cadastro do backlog: "Habilitei o nível de Épicos. Me guie para cadastrar os Épicos, as Features e os PBIs no Azure Boards, com título, descrição e critérios de aceite."
- 16/11 - Test Cases: "Monte os Test Cases funcionais, positivos e negativos, caixa-preta, com objetivo, preparação, entradas, saídas esperadas e passos, cobrindo cada PBI."
- 17/11 - Ajuste ao Contrato: "O Contrato foi para a versão 1.17. Liste as correções necessárias no backlog e nos Test Cases do Azure e me diga como aplicá-las."
- 18/11 - Test Plan e rastreabilidade: "Como organizo os Test Cases no Azure Test Plans, com a suite estática, as suites por PBI e o vínculo Tested By?"
- 20/11 - Relatório em PDF: "Monte o briefing para o Claude Code gerar o PDF de entrega com os prints das Features e dos Test Cases e o link do Azure, e confira se cobre toda a rubrica."
- 20/11 - Roteiro do pitch: "Monte o roteiro falado do pitch de até 4 minutos, no método Observar, Escutar, Definir, Organizar, Construir e Contar, com o texto para eu gravar."
- 21/11 - Montagem do pitch: "Quero um pitch dinâmico e cinematográfico, com transições, imagens de impacto, dados na tela, o símbolo da marca no canto e uma animação final. Escreva as mensagens para o Claude Code montar o vídeo."
- 21/11 - Revisão do vídeo: "Assisti ao v1 e anotei os ajustes: logo com o nome na abertura e no fechamento, linhas do diagrama na borda das caixas, selo de imagem ilustrativa menor e 2 segundos a mais no final."
- 21/11 - Publicação no YouTube: "Gere o título, a descrição com os créditos, as tags, os capítulos e a miniatura do vídeo."
- 21/11 - README de QA e entrega: "Atualize o README de QA, o link do pitch no relatório e no README raiz, regenere o PDF e organize a pasta docs/pitch."