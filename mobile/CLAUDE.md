# CLAUDE.md - App do entregador do HelmCare (Mobile)

Este arquivo orienta o Claude Code quando ele trabalha na pasta `mobile/` do monorepo `helmcare`. Leia tudo antes de qualquer etapa.

## Contexto

- Global Solution 2026.2 da FIAP, disciplina **Mobile Development & IoT** (Prof. Anderson da Silva Nascimento). Aluno: Renan Dias Utida (RM 558540, turma 3ESPW). Ele faz a GS sozinho.
- O app é a porta de entrada do HelmCare para o entregador de moto: cadastro, ficha médica, Índice de Risco do Turno, alerta de calor, alerta de queda com cancelamento, rede de voluntários, ocorrência com cronômetro e histórico offline.
- Enunciado: app funcional em Expo, com React Native, TypeScript e Expo Router, que consome a API, salva dados em banco e é simples e acessível para quem não é especialista.
- Critérios (todos com o mesmo peso): telas e navegação, consumo de dados, banco de dados, acessibilidade, aderência ao problema e documentação.

## Papéis

- **Renan:** decide, revisa e faz os commits, o push, a PR e o merge.
- **Claude Code (você):** cria e edita arquivos dentro de `mobile/`, instala pacotes, roda as verificações e mostra a saída real. Não faz commits.
- **Nunca faça:** `git add`, `git commit`, push, PR, merge, troca de branch, rebase, reset ou stash. Não altere a API, o Serviço de Risco, o banco ou os containers. Nunca abra `.env`, user-secrets ou arquivos com senha.

## Fonte da verdade

1. `docs/contrato-integracao.md` (versão 1.17), principalmente as seções 2, 3, 4, 5, 7 e 9.
2. A Ficha no `README.md` da raiz (seção "Mobile Development & IoT (/mobile)").
3. Este arquivo.

Nunca renomeie campos, enums, rotas ou payloads do contrato. O app usa exatamente os nomes do JSON. Se algo do contrato parecer impossível ou contraditório, ou se a API responder diferente do contrato, pare e me avise com o título **MUDANÇA DE CONTRATO** e o motivo, sem improvisar.

## Escopo de escrita

- Só dentro de `mobile/`.
- Nunca altere outras pastas, o `README.md` da raiz, o `prompts.md`, o `.gitignore`, o `.editorconfig` ou o `.gitattributes` da raiz.
- Arquivos temporários, scripts de teste e saídas do `expo export` ficam fora do repositório (por exemplo, na pasta temporária do sistema).

## Stack e versões

| Item | Versão ou regra |
|---|---|
| Expo | SDK 58 (`expo` 58.0.x), React Native 0.88, TypeScript em modo `strict` |
| Navegação | `expo-router` (rotas em `app/`, `experiments.typedRoutes` ligado) |
| HTTP | `axios`, com uma instância única em `src/servicos/api.ts` |
| Banco | `expo-sqlite` (`SQLiteProvider` e `useSQLiteContext`) |
| Ícones | `@expo/vector-icons` |
| Logo e símbolo (SVG) | `react-native-svg` e, só no desenvolvimento, `react-native-svg-transformer` (configurado no `metro.config.js`) |
| Contas e tokens do login da API | `expo-secure-store` (`src/servicos/armazenamentoSeguro.ts`) |
| QR Code do capacete | `react-native-qrcode-svg` (usa o `react-native-svg`), só na tela `app/qr-capacete.tsx` |
| Notificações locais | `expo-notifications` (`src/servicos/notificacoes.ts`), canal Android "alertas" de alta importância; só locais (o push remoto não funciona no Expo Go) |
| Node | 22.13 ou mais novo |

- Instale pacotes sempre com `npx expo install <pacote>`, para receber a versão compatível com o SDK.
- Nenhuma biblioteca fora desta tabela sem me perguntar antes (nada de TanStack Query, react-hook-form, zod, AsyncStorage, Redux, UI kits ou expo-location).
- O alvo é Android no Expo Go. A web não é alvo.

## Estrutura

```
mobile/
├── CLAUDE.md, README.md, app.json, package.json, tsconfig.json, eslint.config.js, .env.example
├── app/                       rotas do Expo Router (só telas e layouts)
│   ├── _layout.tsx            pilha raiz e provedores (SQLite, Tema, Sessao, Turno e Chamados)
│   ├── index.tsx              abertura com o logo; restaura a sessão e vai para o login ou as abas
│   ├── login.tsx              e-mail e senha (entrar, adicionar conta ou entrar de novo)
│   ├── cadastro.tsx           3 passos (dados e acesso, contato, capacete), com o login logo depois
│   ├── (abas)/_layout.tsx     5 abas: Turno, Chamados, Histórico, Perfil e Ajustes;
│   │                          no cabeçalho, só o botão do menu da conta
│   ├── (abas)/turno.tsx       turno, Índice de Risco, estado do capacete, calor e trechos
│   ├── (abas)/chamados.tsx    rede de voluntários: disponibilidade, posição e chamados
│   ├── (abas)/historico.tsx   histórico offline (só SQLite) com o resumo do indicador
│   ├── (abas)/perfil.tsx      dados mascarados, capacete, ficha, contatos e rede
│   ├── (abas)/ajustes.tsx     conexão com a API, aparência, atalho para as contas e a versão
│   ├── ficha-medica.tsx       ficha médica: leitura pelo GET e edição (só em memória)
│   ├── contato.tsx            adicionar ou editar um contato de emergência (parâmetro contatoId)
│   ├── contas.tsx, telefone.tsx, acessos-ficha.tsx   modais (contas do aparelho, troca de
│   │                          telefone e quem acessou a ficha no Cofre)
│   ├── qr-capacete.tsx        QR Code do capacete, logo depois do POST /dispositivos
│   ├── alerta.tsx             alerta de queda em tela cheia, sem gesto de fechar
│   └── ocorrencia/[id].tsx    o SOS, para a vítima ou o voluntário (parâmetro papel)
└── src/
    ├── config/        constantes.ts (seção 5 do contrato) e urlApi.ts
    ├── tipos/         contrato.ts (DTOs e enums das seções 4 e 7)
    ├── servicos/      api.ts (instância única, Authorization e renovação), erros.ts,
    │                  armazenamentoSeguro.ts (contas e tokens), autenticacaoServico.ts,
    │                  cofreServico.ts (auditoria do Cofre),
    │                  testeSessaoDev.ts (só __DEV__) e um serviço por recurso (entregador,
    │                  dispositivo, turno, trecho, voluntario, ocorrencia e saude)
    ├── banco/         migracoes.ts e um repositório por tabela (sem tabela de perfil desde a migração 3)
    ├── contextos/     Sessao (contas e sessão), Tema, Turno (alerta de queda) e Chamados (rede)
    ├── hooks/         usePolling, useContagemRegressiva, useCronometro, useOcorrencia,
    │                  useHistorico, useSessaoDaConta, useTrechosCriticos e useAberturaMinima
    ├── componentes/   componentes reutilizáveis; da rede e da ocorrência: Chave,
    │                  ParticiparRede, CartaoChamado, CartaoCronometro, BotaoLigar192
    │                  e MenuConta (painel lateral da conta); do login: RegrasSenha,
    │                  SessaoIndisponivel e FaixaSessaoTerminada
    ├── tema/          cores, tipografia e espaçamentos
    └── utils/         validacao, datas, dadosSimulados, posicoesSimuladas,
                       mensagensErro, repetirEmConflito, memoriaDoFluxo e navegacao
```

- Telas não chamam o Axios nem o SQLite direto: usam serviços, repositórios, hooks e contextos.
- Serviços não conhecem a interface. Repositórios não conhecem a API.

## Comandos (sempre a partir da pasta `mobile/`)

```shell
npm run typecheck        # tsc --noEmit
npm run lint             # expo lint
npx expo-doctor
npx expo export --platform android --output-dir <pasta temporária fora do repositório>
npx expo start           # o Renan escaneia o QR Code no Expo Go
```

## Convenções de código

- TypeScript sem `any` e sem `@ts-ignore`. Evite `as`; quando for inevitável, comente o motivo.
- Componentes de função com hooks. Todo `useEffect` com assinatura, temporizador ou polling devolve a função de limpeza.
- Nomes do domínio em português sem acento (`Ocorrencia`, `fichaMedica`, `turnoServico`). Termos do framework continuam em inglês (`useEffect`, `Stack`, `StyleSheet`).
- Enums do contrato como uniões de strings em `src/tipos/contrato.ts` (`'SOS_CAMADA_1' | 'SOS_CAMADA_2' | ...`).
- Comentários em português, com JSDoc curto em toda função, hook, componente e tipo exportado, explicando o propósito e a regra do contrato que ele cumpre. Dentro do código, comente as decisões que não são óbvias: o Renan precisa saber explicar cada linha.
- **Proibido usar travessão (U+2014) ou meio-traço (U+2013)** em código, comentários, textos da interface, commits e documentação. Use vírgula, dois-pontos ou parênteses.
- Cores, fontes e espaçamentos só pelo tema (`useTema()`), nunca escritos direto nas telas.
- Textos da interface em português simples, frases curtas, sem jargão técnico.

## Regras do contrato no app

- **Só a API principal.** O app nunca chama o Serviço de Risco nem o broker MQTT.
- **Datas:** envie ISO 8601 em UTC (`new Date().toISOString()`); mostre no horário do aparelho.
- **Erros:** todos chegam em ProblemDetails. O app decide pelo status HTTP, nunca pelo texto do `title` ou do `detail`. `src/servicos/erros.ts` converte o erro do Axios num `ErroApi` com o tipo (`validacao` 400, `nao-autorizado` 401, `proibido` 403, `nao-encontrado` 404, `conflito` 409, `muitas-tentativas` 429, `indisponivel` 503, `sem-conexao`, `desconhecido`), e cada tela escolhe a própria mensagem para cada tipo. O `ErroApi` nunca leva a configuração da requisição (que tem o Authorization) nem os corpos.
- **Autenticação (1.15):** toda rota que não é pública leva `Authorization: Bearer <accessToken>` da conta que fez a requisição (o interceptor grava essa conta na configuração, para a renovação e a repetição usarem sempre a mesma, mesmo com troca de conta no meio). No primeiro 401, uma renovação só por conta de cada vez (`POST /entregadores/renovar-token`; as chamadas que levam 401 juntas esperam a mesma) e a repetição da chamada uma vez. A sessão só termina com o 401 da renovação: os tokens dessa conta são apagados e o SessaoContext decide (no alerta e na ocorrência, a faixa "Entre de novo" e o 192 visível; nas outras telas, o login). Sem conexão, 429 ou 5xx na renovação, a conta continua salva. 401, 403, 404, 409 e 429 pelo status, nunca pelo texto (o login errado tem sempre o mesmo texto).
- **Contas no aparelho:** até `MAXIMO_CONTAS_APARELHO` (3) contas logadas, no `expo-secure-store`, com troca rápida sem senha. Chaves: `helmcare.contas` (só a lista de ids), `helmcare.contaAtiva` e `helmcare.<entregadorId>.acesso` e `.renovacao` (uma chave por token, pelo limite de cerca de 2 KB por valor; os tokens têm 710 caracteres). A identidade é o token, não o SQLite. A sessão (nome, voluntário, capacete, turno e ocorrência ativos) vem do `GET /entregadores/sessao` e fica só em memória; sem conexão, o app abre com a conta salva, sem nome, e nunca apaga a conta por falta de rede. Sair apaga só os tokens dessa conta (o histórico local fica). Sem rota de logout no contrato, o refreshToken continua válido no servidor até vencer.
- **409 no cancelar e no encerrar da ocorrência:** pode ser conflito com o escalonamento. Repita a chamada até 3 vezes, com uma pausa curta. Se continuar, releia a ocorrência e mostre o estado atual.
- **503 nas rotas do turno** (iniciar com proteção, encerrar e cancelar o alerta): o comando não chegou ao capacete e nada foi gravado. Peça para tentar de novo.
- **`POST /turnos`:** tempo limite próprio de `TEMPO_LIMITE_TURNO_MS` (15 s; o contrato pede pelo menos 10 s, porque a rota pode levar cerca de 6 s com a Open-Meteo e o Serviço de Risco lentos), com o carregamento na tela.
- **Turno da sessão:** com `turnoAtivoId`, o app assume esse turno; se o SQLite tem outro turno aberto, ele é encerrado no histórico (a API manda). O `GET /turnos/{id}/risco` não traz os horários: um turno que o SQLite não tem fica só em memória, sem horários, e entra no histórico pelo 200 do encerrar. Com `ocorrenciaAtivaId`, o app abre a ocorrência como vítima.
- **`GET /clima`:** fora do polling (ao abrir a aba Início e ao puxar para atualizar), comparando o número e escondendo o cartão no 503.
- **Polling:** a cada `INTERVALO_POLLING_APP_S` (5 s), com `setTimeout` encadeado (a próxima consulta só começa depois que a anterior termina), pausado com o app em segundo plano (`AppState`) e cancelado ao desmontar. A única exceção é o alerta do turno com a proteção ligada, que continua com o app minimizado (`continuarEmSegundoPlano`), para a queda virar notificação. É melhor esforço: o Android pode pausar ou encerrar o app em segundo plano, e aí as consultas param até ele voltar para a frente. Os chamados e a ocorrência continuam pausando.
- **Notificações:** só locais, sem nenhum dado pessoal no texto (seção 9). A permissão é pedida ao começar o primeiro turno, com uma frase antes explicando o motivo; negar não muda nada no app. Com o app aberto, a notificação também aparece no topo. Tocar abre a tela certa (alerta, ocorrência, chamados ou turno). As notificações de chamado novo, de voluntário a caminho e de ocorrência encerrada dependem de pollings que pausam em segundo plano: aparecem com o app aberto.
- **Contagem do alerta:** conte os 15 s (`CONTAGEM_CANCELAMENTO_S`) no relógio do app, a partir da primeira leitura com `estadoCapacete` = `CONTAGEM`. O `contagemTerminaEm` aparece só como informação. Quando a contagem do app zerar e o estado ainda for `CONTAGEM`, mostre "Enviando SOS..." até o estado mudar. O botão de cancelar fica habilitado enquanto o estado for `CONTAGEM`: quem decide é a API (202 ou 409).
- **Calor:** sugira pausa quando o `indiceCalorC` for maior ou igual a `ALERTA_CALOR_C` (32).
- **Cronômetro da ocorrência:** use o `cronometro.segundosDesdeQueda` de cada consulta e some 1 por segundo no relógio do app até a próxima. Com `parado` verdadeiro, pare. A meta é `META_TEMPO_ACEITE_S` (120).
- **Localização:** antes do aceite, o voluntário só vê a localização aproximada e a `distanciaKm`. A exata só aparece quando a resposta traz `localizacaoExata` verdadeiro.
- **Ficha médica:** o `GET /entregadores/{id}` só traz `possuiFichaMedica`; a ficha completa vem do `GET /entregadores/{id}/ficha-medica` (404 sem ficha) e da resposta do `PUT`, as duas com `no-store`. Ela fica só em memória, na tela, e some ao sair.
- **E-mail:** sempre mascarado, como a API devolve (`r***@exemplo.com`).
- **Capacete:** o `qrToken` e a `urlCofre` só existem na resposta do `POST /dispositivos`, e o QR do capacete só aparece logo depois dela. Não guarde nenhum dos dois. A `urlCofre` vem relativa ("/cofre/{qrToken}"): o app completa com a origem da URL da API em uso (`completarCaminhoDaApi`, em `src/config/urlApi.ts`), e o endereço completo segue as regras do `qrToken` (só em memória, pela memória do fluxo, nunca na rota).
- **Contatos:** o `PUT` de um contato pede o corpo inteiro, e a API só devolve o telefone mascarado: o formulário de edição começa com o telefone vazio. O `DELETE` responde 204.
- **Ocorrência da vítima:** depois do aceite, mostre só a `distanciaVoluntarioKm` ("Voluntário a 0,6 km"), nunca a posição do voluntário. Com `encerradaAutomaticamente`, o resumo e o histórico dizem "Encerrada automaticamente depois de 2 horas" (texto montado com `TEMPO_ENCERRAMENTO_AUTOMATICO_MIN`).
- **Pilha de navegação:** ao entrar (login ou cadastro) e ao ir para o login (sair, fim da sessão), use `comecarPilhaEm` (`src/utils/navegacao.ts`), para o voltar do Android não reabrir o login nem as abas de uma conta que saiu.

## Banco local (SQLite)

- Migrações em `src/banco/migracoes.ts`, controladas por `PRAGMA user_version`, executadas no `onInit` do `SQLiteProvider`. Nunca altere uma migração já publicada: crie a próxima.
- Tabelas: `preferencia` (tema e URL manual), `turno`, `alerta`, `ocorrencia` e `trecho_cache` (o pedido de cada etapa detalha as colunas). A tabela `perfil` saiu na migração 3: a identidade é o token. O histórico é ligado só pelo `entregador_id` (o alerta pelo `turno_id`).
- **Nunca grave no SQLite:** nome (nem simulado), e-mail, CPF, CNH, telefones, contatos de emergência, ficha médica, localização de pessoas (da vítima, da ocorrência e do voluntário), senha, `accessToken`, `refreshToken`, `qrToken` e `urlCofre`. As coordenadas dos trechos críticos são dado público e podem ficar no cache.
- Consultas sempre com parâmetros (`?`), nunca com texto concatenado.

## Dados pessoais

- Todos os dados de cadastro são simulados (`src/utils/dadosSimulados.ts` gera nome, CPF, CNH e telefones fictícios).
- Nunca use `console.log` com dados pessoais, ficha médica, coordenadas ou corpos de requisição e resposta. Logs, quando existirem, levam só ids e status HTTP.
- As telas mostram os dados mascarados que a API devolve, sem desmascarar.
- **Credenciais (seção 9):** a senha, o `accessToken`, o `refreshToken`, o `qrToken` e a `urlCofre` nunca vão para o SQLite, para log, para URL do app nem para parâmetro de rota. Os tokens ficam só no `expo-secure-store` (`src/servicos/armazenamentoSeguro.ts`) e só saem dele para o cabeçalho `Authorization` e para o corpo da renovação. A senha só existe no estado da tela de login e do cadastro e some depois de cada tentativa.
- Dado que não pode ir na rota (por exemplo, o e-mail que preenche o login depois do cadastro) passa pela memória do fluxo (`src/utils/memoriaDoFluxo.ts`), lido uma vez e apagado.
- No secure-store, só ids e tokens: nada de nome, e-mail ou senha.

## Acessibilidade (critério da nota)

- Texto escala com a fonte do sistema: nunca `allowFontScaling={false}` e nada de altura fixa em contêiner de texto.
- Área de toque de pelo menos 48 x 48 dp em todo elemento clicável; ações principais com botões grandes.
- Contraste mínimo de 4,5:1 (texto normal) nos temas claro e escuro, que seguem o sistema (`useColorScheme`).
- A cor nunca é a única informação: a faixa de risco e os estados levam texto e ícone.
- Todo elemento interativo tem `accessibilityRole` e `accessibilityLabel`, e `accessibilityHint` ou `accessibilityState` quando ajudam. Ícones decorativos ficam fora do leitor de tela.
- Mudanças importantes (contagem do alerta, novo chamado, socorro a caminho) são anunciadas com `AccessibilityInfo.announceForAccessibility` ou `accessibilityLiveRegion`, e o alerta vibra (`Vibration`).
- Sem seletor de data: escolhas em botões grandes.

## Rede

- `src/config/urlApi.ts` resolve a URL base, nesta ordem: a URL salva na aba Ajustes; `EXPO_PUBLIC_API_URL`; o IP do computador pelo `hostUri` do Metro (`Constants.expoConfig?.hostUri`), montando `http://<ip>:5000/api/v1`; e, por último, `http://10.0.2.2:5000/api/v1` (emulador Android).
- `GET /api/v1/saude` responde texto (`Healthy`), não JSON.
- Para testar as rotas a partir do computador, use `http://localhost:5000/api/v1` com `curl`. Quem sobe a API e o Serviço de Risco é o Renan.

## Verificação de cada etapa

1. `npm run typecheck` e `npm run lint` sem erros e sem avisos.
2. `npx expo-doctor` sem problemas.
3. `npx expo export --platform android` concluído, com saída fora do repositório.
4. Quando a etapa usa a API, confira com `curl` os formatos reais das respostas usadas e compare com os tipos de `src/tipos/contrato.ts`.
5. Diga com honestidade o que não deu para testar (por exemplo, a tela no celular, que só o Renan vê).

Conferência de higiene:

```shell
git status --short --untracked-files=all mobile | grep -E '(^|/)(node_modules|\.expo|dist|web-build)/|(^|/)\.env$|\.(db|jks|keystore)$' || echo "ok: higiene"
```

## Git

- Trabalhe na branch atual e deixe as alterações sem commit.
- No fim de cada etapa, sugira os commits: para cada um, a mensagem em Conventional Commits, em português, com escopo `mobile` (`feat(mobile): ...`, `chore(mobile): ...`, `docs(mobile): ...`, `fix(mobile): ...`), e a lista exata de arquivos.
- As mensagens de commit sugeridas e os textos de PR não levam nenhuma linha de atribuição nem menção a ferramentas de IA (nada de Co-Authored-By ou Claude-Session).

## Ao terminar cada etapa, mostre

1. Arquivos criados ou alterados, com uma linha sobre cada um.
2. A saída real, sem resumir, das linhas finais de `npm run typecheck`, `npm run lint`, `npx expo-doctor` e `npx expo export`.
3. As chamadas `curl` feitas e o status de cada uma (sem colar dados pessoais).
4. `git status --short`.
5. A saída da conferência de higiene.
6. Os commits sugeridos (mensagem e arquivos de cada um).
7. O roteiro de teste no celular para o Renan (passos e o resultado esperado de cada um).
8. Qualquer decisão que não estava no pedido ou desvio do pedido, com o motivo.
