# CLAUDE.md - API principal do HelmCare (C#)

Este arquivo orienta o Claude Code quando ele trabalha na pasta `api/` do monorepo `helmcare`. Leia tudo antes de qualquer etapa.

## Contexto

- Global Solution 2026.2 da FIAP, disciplina **C# Software Development** (Prof. Rafael Eduardo Feliciano Cassiolato). Aluno: Renan Dias Utida (RM 558540, turma 3ESPW).
- A API é o núcleo do HelmCare: ASP.NET Core (.NET 10), porta 5000, prefixo `/api/v1`, PostgreSQL (banco `helmcare_api`) com EF Core e migrations. Ela recebe os eventos do capacete por MQTT, orquestra o SOS escalonado, atende o app e serve a página do Cofre médico.
- Rubrica (10,0): POO 3,0 - Web API 2,5 - EF Core + LINQ 1,5 - Async/Await 1,0 - Qualidade do código 2,0.

## Papéis

- **Renan:** decide, revisa e faz os commits, o push, a PR e o merge.
- **Claude Code (você):** cria e edita arquivos dentro de `api/`, compila, roda testes e migrations e mostra a saída real. Não faz commits.
- **Nunca faça:** `git add`, `git commit`, push, PR, merge, troca de branch, rebase, reset, stash, `docker compose down -v`, `docker volume rm` ou `prune`. Nunca abra `.env`, user-secrets ou qualquer arquivo com senha.

## Fonte da verdade

1. `docs/contrato-integracao.md` (versão 1.17).
2. Este arquivo.

Nunca renomeie entidades, campos, enums, rotas, payloads ou tópicos MQTT do contrato. Se algo do contrato parecer impossível ou contraditório, pare e me avise, explicando o motivo, sem improvisar.

## Escopo de escrita

- Só dentro de `api/`.
- `.github/workflows/` somente quando o pedido disser explicitamente.
- Nunca altere outras pastas, o `README.md` da raiz, o `prompts.md`, o `.gitignore`, o `.editorconfig` ou o `.gitattributes` da raiz.

## Estrutura

```
api/
├── CLAUDE.md - README.md - HelmCare.slnx - global.json
├── Directory.Build.props - .editorconfig (root = true) - .gitattributes (eol=lf)
├── compose.yaml (só PostgreSQL) - .env.example - evidencias/
├── src/
│   ├── HelmCare.Dominio/      Comum/ - Entidades/ - Enums/ - ObjetosDeValor/
│   └── HelmCare.Api/          Controllers/ - Contratos/ - Servicos/ - Persistencia/ - Mensageria/
│                              Integracoes/ - Seguranca/ - Configuracao/ - wwwroot/cofre/
└── tests/
    └── HelmCare.Testes/       Dominio/ - Servicos/ - Mensageria/ - Seguranca/ - Configuracao/ - Fakes/
```

- `HelmCare.Dominio` não tem pacote NuGet além do próprio .NET.
- `HelmCare.Api` referencia `HelmCare.Dominio`.
- `HelmCare.Testes` referencia os dois.

## Comandos (sempre a partir da pasta `api/`)

```shell
dotnet restore HelmCare.slnx
dotnet build HelmCare.slnx
dotnet test HelmCare.slnx
dotnet tool restore
dotnet ef migrations add <Nome> --project src/HelmCare.Api --output-dir Persistencia/Migrations
dotnet ef database update --project src/HelmCare.Api
docker compose up -d
docker compose ps
```

## Pacotes e versões

Use estas versões (as mesmas dos templates do professor). Se uma versão não restaurar, pare e reporte; não troque por conta própria.

| Pacote | Versão |
|---|---|
| Microsoft.EntityFrameworkCore.Design, Microsoft.AspNetCore.OpenApi, Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.12 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.2 |
| MSTest | 4.4.1 |
| Microsoft.NET.Test.Sdk | 18.10.1 |
| StyleCop.Analyzers | 1.1.118 (já no Directory.Build.props) |

## Convenções de código

- `.editorconfig` e `Directory.Build.props` de `api/` seguem o template do professor. Não altere severidades nem desligue analisadores.
- **0 avisos e 0 erros no build**, sem exceção. Nunca suprima aviso com `#pragma`, `NoWarn` ou `[SuppressMessage]`; corrija a causa. O `GenerateDocumentationFile` fica ligado em todos os projetos, inclusive no de testes (desligá-lo gera o aviso `EnableGenerateDocumentationFile`, porque o IDE0005 depende dele no build).
- `ImplicitUsings` desligado: todo arquivo declara os seus usings, `System` primeiro, fora do namespace.
- Namespace por arquivo (`namespace X;`), chaves sempre, sem `this.`, campos privados `_camelCase`, `string.Empty` em vez de `""`, tipos explícitos para tipos nativos (`int`, `string`, `decimal`) e `var` só quando o tipo é evidente.
- Nomes do domínio em português sem acento (`Ocorrencia`, `FichaMedica`). Termos do framework continuam em inglês (`Controller`, `Program`).
- Comentário XML (`///`) em português em todo tipo e membro público, explicando o propósito e as regras (nos testes, uma frase curta sobre o que cada teste garante). Comentários dentro do código só onde a decisão não é óbvia; o Renan precisa saber explicar cada linha.
- **Proibido usar travessão (caractere Unicode U+2014) ou meio-traço (U+2013)** em código, comentários, mensagens, commits e documentação. Use vírgula, dois-pontos ou parênteses.
- Async de ponta a ponta, sempre com `CancellationToken`. Nunca `.Result`, `.Wait()` ou `Thread.Sleep`. `AnyAsync` em vez de `CountAsync() > 0`.
- Datas e horas em `DateTime` UTC (`Kind = Utc`), obtidas de `TimeProvider` injetado. Nunca `DateTime.Now` ou `DateTime.UtcNow` direto no domínio ou nos serviços; o domínio recebe o instante como parâmetro.
- Controllers finos: nada de `AppDbContext` no controller. DTOs de entrada com DataAnnotations. Entidade nunca é entrada nem saída da API. Erros em ProblemDetails.
- JSON em camelCase, ids em GUID, datas em ISO 8601 UTC.
- Nada de scaffolding `WeatherForecast`.

## Domínio (`HelmCare.Dominio`)

- Toda entidade herda de `Entidade` (abstrata, `Id` do tipo `Guid`). Setters privados, construtor sem parâmetros privado (para o EF) e criação por construtor público ou fábrica estática que valida. Coleções expostas como `IReadOnlyCollection<T>` com lista privada.
- Exceções: `DominioException` (abstrata) e as derivadas `ValidacaoDominioException` (vira 400), `TransicaoInvalidaException` (vira 409) e `RegraNegocioException` (vira 409).
- Enums: membros em PascalCase em C#. O nome do contrato (UPPER_SNAKE_CASE) vem de `[JsonStringEnumMemberName("SOS_CAMADA_1")]` em cada membro, e o tipo leva `[JsonConverter(typeof(JsonStringEnumConverter<T>))]`. O helper `NomeContrato` converte nos dois sentidos a partir desse atributo e é usado pelo conversor do EF e pela leitura do MQTT. Não use `JsonNamingPolicy` para isso (ela geraria `SOS_CAMADA1`).
- Migrations novas só quando o pedido disser. A `Identidade` (1.15) cria as tabelas do Identity, `Entregador.UsuarioId`, `Ocorrencia.EncerradaAutomaticamente` e os índices únicos de CNH e telefone; a `Cofre` (1.16) acrescenta `Concedido` e `Motivo` ao `AcessoCofre`.

### Máquina de estados da `Ocorrencia`

| Estado atual | Ação | Próximo | Efeitos no domínio |
|---|---|---|---|
| (nova) | `Abrir` (evento SOS) | `SOS_CAMADA_1` | `Camada = 1`, `CriadaEm`, coordenadas do evento |
| `SOS_CAMADA_1` | `EscalarParaCamada2` (60 s após `CriadaEm`, sem aceite) | `SOS_CAMADA_2` | `Camada = 2` |
| `SOS_CAMADA_1` ou `SOS_CAMADA_2` | `Aceitar` | `SOCORRO_A_CAMINHO` | `AceitaEm`, cria `AceiteSocorro`, `TempoAteAceiteSegundos` = trecho do capacete (`ContagemTerminaEm - OcorridoEm`, ou `JANELA_OBSERVACAO_S + CONTAGEM_CANCELAMENTO_S` se estimado ou sem contagem) + trecho do servidor (`AceitaEm - CriadaEm`), cada um nunca negativo e arredondado para baixo só na soma (seção 7) |
| `SOS_CAMADA_1` ou `SOS_CAMADA_2` | `Cancelar` | `CANCELADA` | `EncerradaEm` |
| `SOS_CAMADA_1` ou `SOS_CAMADA_2` | `Encerrar` (SAMU chegou antes) | `ENCERRADA` | `EncerradaEm`, `TempoAteAceiteSegundos` nulo |
| `SOCORRO_A_CAMINHO` | `Encerrar` | `ENCERRADA` | `EncerradaEm` |
| `SOS_CAMADA_1`, `SOS_CAMADA_2` ou `SOCORRO_A_CAMINHO` | `EncerrarAutomaticamente` (`TEMPO_ENCERRAMENTO_AUTOMATICO_MIN` ou mais desde `CriadaEm`, inclusivo) | `ENCERRADA` | `EncerradaEm`, `EncerradaAutomaticamente = true`; o indicador fica como está (nulo sem aceite) |
| qualquer outra combinação | | não muda | `TransicaoInvalidaException`, nenhum campo alterado |

`ENCERRADA` e `CANCELADA` são finais. Os comandos MQTT de cada transição (`SOCORRO_A_CAMINHO`, `ENCERRAR`) são enviados pela camada de serviço, não pelo domínio.

## Mensageria (MQTT)

- A API assina `helmcare-rdu/v1/+/telemetria` e `helmcare-rdu/v1/+/evento` em QoS 1 e publica em `helmcare-rdu/v1/{codigo}/comando` em QoS 1, com `clientId` próprio (nunca o do capacete, `helmcare-rdu-HC-0001`).
- O `ts` vem do relógio do capacete. No Wokwi ele anda mais devagar que o relógio real, então a API nunca rejeita nem corrige um evento por comparar o `ts` com a hora do servidor; só compara `ts` com outro `ts` do mesmo capacete.
- Eventos com o mesmo dispositivo, tipo e `ts` são um só (tabela `EventosMqttProcessados`), e o registro de idempotência é gravado na mesma transação do efeito.
- Uma mensagem inválida ou de capacete desconhecido nunca derruba o assinante: é registrada no log e descartada.
- Logs de MQTT levam só o código do capacete, o tipo, o `ts` e ids; nunca o payload, que tem coordenadas.

## Serviço de Risco

- A API chama o Serviço de Risco (`POST indice-turno` e `GET trechos-criticos`) por um HttpClient tipado, com URL base e tempo limite na seção `ServicoRisco` da configuração. O app nunca chama o serviço direto.
- Com 503, qualquer 5xx, erro de rede ou tempo esgotado, o índice do turno vem da versão simulada, com aviso no log. Um 4xx ou uma resposta fora do formato é erro de integração: log de erro e versão simulada. O turno nunca falha por causa do Serviço de Risco.
- A versão simulada usa a mesma regra do serviço (média ponderada das horas do turno e o mesmo texto da explicação), para o mesmo turno ter o mesmo índice com o serviço no ar ou não.
- Logs da integração levam só o motivo, o status HTTP e o tempo; nunca o corpo da requisição ou da resposta.

## Rede de voluntários e SOS

- O escalonamento para a camada 2 roda em segundo plano (BackgroundService com PeriodicTimer criado com o TimeProvider), então vale sem nenhuma requisição chegando; os testes avançam o FakeTimeProvider em vez de esperar.
- A regra de quem pode atender fica só no `Voluntario.PodeAtender`; serviços e consultas não repetem o cálculo do raio.
- Antes do aceite, voluntário só vê a localização aproximada (2 casas, `Coordenada.Aproximada`); a exata só vai para quem aceitou. Distâncias mostradas são calculadas até a localização aproximada.
- Os comandos da ocorrência (`SOCORRO_A_CAMINHO` e `ENCERRAR`) são publicados depois de gravar e não bloqueiam a rota: se o broker falhar, aviso no log e a rota responde normalmente.
- O aviso ao contato de emergência é simulado: só uma linha de log com o `ocorrenciaId` e a quantidade de contatos, depois que a ocorrência é gravada; nunca nome, telefone ou localização.

## Identidade e autorização (1.15)

- Login com o ASP.NET Core Identity (`AddIdentityApiEndpoints`, esquema bearer do próprio Identity, token opaco protegido pelo Data Protection; sem JWT e sem `MapIdentityApi`: as rotas são nossas). `accessToken` de 60 min e `refreshToken` de 14 dias, configuráveis em `OpcoesHelmCare`.
- Papéis `ENTREGADOR` e `SOCORRISTA` (criados pela migration). O token leva o papel e o id do perfil (claim `entregadorId` ou `socorristaId`).
- Senha de 8 a 64 caracteres, com maiúscula, minúscula e dígito (símbolo não é exigido). E-mail único sem diferenciar maiúsculas, até 254 caracteres. Cinco tentativas erradas bloqueiam a conta por 15 min, também nas contas novas.
- Login: e-mail ou senha errados, conta bloqueada e conta de socorrista dão o mesmo `401`, com o mesmo texto.
- **Ordem dos códigos:** primeiro `401` (sem token, inválido ou vencido), depois `404` (recurso inexistente) e por último `403` (recurso de outro usuário ou papel errado). Nunca há redirecionamento (302): só `401` ou `403` em ProblemDetails.
- A conferência do dono fica no `AutorizacaoRecursos` (a partir do `IUsuarioAtual`), chamada pelos serviços depois de carregar o recurso; nunca nos controllers. O padrão é exigir login; as rotas públicas são marcadas uma a uma. O roteador MQTT e o laço em segundo plano não passam pela autorização.

| Rotas | Quem pode |
|---|---|
| `GET /entregadores/{id}`, `GET` e `PUT /entregadores/{id}/ficha-medica`, `PUT /entregadores/{id}/telefone`, `POST`, `PUT` e `DELETE` dos contatos, `GET /cofre/auditoria/{entregadorId}` | Só o próprio entregador |
| `POST /dispositivos`, `POST /voluntarios` e `POST /turnos` | Só o próprio entregador: o `entregadorId` do corpo precisa ser o do token |
| `/turnos/{id}/...` (risco, alerta, cancelar alerta e encerrar) | Só o dono do turno |
| `PUT /voluntarios/{id}/disponibilidade`, `GET /ocorrencias/proximas?voluntarioId=` e `POST /ocorrencias/{id}/aceites` | Só o voluntário do próprio entregador (o `voluntarioId` precisa ser o do token) |
| `GET /ocorrencias/{id}?voluntarioId=` | A vítima ou qualquer entregador que seja voluntário; o `voluntarioId`, se vier, precisa ser o do token |
| `POST /ocorrencias/{id}/cancelar` | Só a vítima |
| `POST /ocorrencias/{id}/encerrar` | A vítima ou o voluntário que aceitou |
| `POST /entregadores`, `POST /entregadores/login`, `POST /entregadores/renovar-token`, `POST /socorristas/login`, `GET /trechos-criticos`, `GET /saude` e a página `GET /cofre/{qrToken}` | Públicas |

- Token de `SOCORRISTA` em qualquer rota do entregador: `403`.
- **Log:** e-mail, senha, `accessToken` e `refreshToken` nunca vão para o log. No login, só o id do perfil e o resultado (sucesso, falha ou bloqueio); e-mail desconhecido não registra nada do e-mail.
- Limite de 10 requisições por minuto por IP do cliente no `POST /entregadores/login`, no `POST /entregadores/renovar-token` e no `POST /socorristas/login` (`429` em ProblemDetails).
- **Data Protection:** as chaves ficam no diretório de `HelmCare:DiretorioChavesDataProtection` (variável `HelmCare__DiretorioChavesDataProtection`), fora da pasta publicada; sem isso, um reinício invalida todos os tokens. Em Development, o padrão é `LocalApplicationData/HelmCare/chaves-data-protection`; nos outros ambientes, o caminho é obrigatório e a API não sobe sem ele. Nada desse diretório entra no git.
- **Cabeçalhos encaminhados:** `X-Forwarded-For` e `X-Forwarded-Proto`, aceitos só dos proxies de `HelmCare:ProxiesConfiaveis` (padrão `127.0.0.1` e `::1`, o nginx na mesma VM), antes do limite de logins e de qualquer uso do IP.
- A rota `POST /api/v1/dev/entregadores/{id}/conta` só existe em Development (fora dele não é mapeada nem aparece no Scalar).

## Cofre médico (contrato 1.16)

- **Janela de acesso por socorrista e entregador:** abre na primeira concessão e vale até o `expiraEm` (a concessão mais `TTL_ACESSO_COFRE_MIN`, 60 min, em `OpcoesHelmCare`). Na origem `SOS_ATIVO`, também fecha quando a ocorrência que a abriu vai para `ENCERRADA` ou `CANCELADA`; na `EMERGENCIA_MANUAL`, só o TTL vale. Cada socorrista tem a própria janela.
  - Pedido dentro da janela: `200`, com o mesmo `expiraEm` e a mesma origem, e uma linha nova de auditoria. Repetir não estica o prazo.
  - Janela fechada: `403` com o motivo `ACESSO_EXPIRADO`, e o socorrista fica bloqueado para esse entregador por mais `TTL_ACESSO_COFRE_MIN` depois do fechamento. A exceção é uma ocorrência nova do entregador (outro SOS), que abre uma janela nova na hora.
  - Emergência manual: abre quando não há ocorrência ativa e o socorrista não tem janela aberta nem fechada dentro do bloqueio para esse entregador.
  - A regra fica no domínio (`JanelaAcessoCofre`), calculada a partir dos `AcessoCofre` concedidos do par.
- **Auditoria:** toda tentativa de um socorrista autenticado com `qrToken` existente grava um `AcessoCofre` (`Concedido` e `Motivo`, do enum `MotivoNegacaoCofre`: `SOCORRISTA_NAO_VERIFICADO` e `ACESSO_EXPIRADO`, nulo quando concedido). Numa negação: a origem é a que valeria naquele momento, o `ocorrenciaId` é o da ocorrência ativa (se houver) e o `expiraEm` é o da janela fechada (`ACESSO_EXPIRADO`) ou igual ao `acessadoEm` (`SOCORRISTA_NAO_VERIFICADO`). `qrToken` inexistente (`404`) e token de entregador (`403`) não gravam auditoria: ficam só no log, com o id do usuário e o resultado. O `ip` é o `RemoteIpAddress` depois dos cabeçalhos encaminhados e fica só no banco.
- **Ordem dos códigos do acesso:** `401` sem token; `403` para o papel `ENTREGADOR` (sem auditoria); `404` para `qrToken` inexistente; depois a regra do socorrista (`403` não verificado, `403` expirado ou `200`).
- **Resposta do acesso:** `{ origem, ocorrenciaId, acessadoEm, expiraEm, tipoSanguineo, alergias, condicoesCriticas, contatos: [{ nome, telefone, parentesco }] }`, com `Cache-Control: no-store`. O telefone dos contatos vem completo (só dígitos). Nunca `medicamentos`, nem nome, CPF ou CNH do entregador. Sem ficha, os três campos de saúde vêm `null`.
- **Auditoria `GET /api/v1/cofre/auditoria/{entregadorId}`:** só o dono; em ordem decrescente de `acessadoEm`, com `{ acessadoEm, expiraEm, origem, concedido, motivo, socorrista: { nome, organizacao } }`; sem `ip` e sem dados de saúde.
- **Login do socorrista (`POST /api/v1/socorristas/login`):** mesmo corpo e formato do login do entregador, com `socorristaId`, `nome`, `organizacao` e `verificado`; o mesmo `401` para senha errada, conta bloqueada e conta de entregador; mesma verificação de hash no e-mail desconhecido; mesmo limite de logins. Sem renovação: o `POST /entregadores/renovar-token` com um refresh de socorrista dá `401`.
- **Rota de dev `POST /api/v1/dev/socorristas`:** cria o socorrista e a conta (papel `SOCORRISTA`) numa transação só; `201` com o e-mail mascarado; e-mail repetido, `409`.
- **Página `GET /cofre/{qrToken}`:** sempre o mesmo HTML estático (exista o `qrToken` ou não), sem nenhum dado no HTML, com `wwwroot/cofre/index.html`, `assets/cofre.js` e `assets/cofre.css`, sem CDN e sem script inline. O token fica só numa variável do JavaScript. Cabeçalhos na página e nos assets: `Content-Security-Policy: default-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'`, `Referrer-Policy: no-referrer`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` e `Cache-Control: no-store`.
- **`qrToken` é credencial (seção 9):** só no QR Code, no endereço da página e no `POST /dispositivos`; nunca em log, mensagem de erro, ProblemDetails (o `instance` sai com `{qrToken}`), auditoria ou outra resposta.

## Previsão do tempo e clima (contrato 1.17)

- A previsão vem da Open-Meteo (`https://api.open-meteo.com/v1/forecast`, sem chave, dados CC BY 4.0 com crédito da fonte), por um HttpClient tipado em `Integracoes/OpenMeteo`, no padrão do Serviço de Risco: URL base, coordenada fixa da cidade (padrão `-23.5641`, `-46.6525`), tempo limite de 3 s sem novas tentativas e cache do clima de 10 min, todos na seção `OpenMeteo` da configuração.
- **`POST /turnos`:** as horas cheias UTC que se sobrepõem ao turno (as mesmas do Serviço de Risco); `chuvaMmHora` é a maior precipitação; `temperaturaC` e `umidadePct` são os da hora de maior índice de calor (`IndiceCalor`, empate na primeira hora); chuva e temperatura com 1 casa, umidade inteira, nas faixas da seção 8.
- **Reserva:** falha de rede ou status fora de 2xx, mais de 3 s, resposta inválida ou horas do turno não cobertas levam à `PrevisaoTempoSimulada` (7 mm/h, 24 °C e 85%), com aviso no log só com o motivo e o tempo. O turno nunca falha por causa do tempo; o cancelamento do cliente propaga.
- **Previsão simulada forçada** (`HelmCare:PrevisaoSimuladaForcada`): o `POST /turnos` usa a simulada sem chamar a Open-Meteo (testes e demonstração com o índice 60). Não vale para o `GET /clima`.
- **`GET /clima`:** só o papel `ENTREGADOR`; condições atuais (`current`), `indiceCalorC` com 1 casa e `alertaCalor` pelo índice já arredondado contra `ALERTA_CALOR_C`; cache de 10 min na memória; sem a Open-Meteo e sem cache válido, `503` "Previsão do tempo indisponível", sem valor simulado.
- Nenhuma coordenada nem corpo da Open-Meteo vai para o log (o log do HttpClient, que leva a URL com a coordenada, fica em `Warning`).

## Dados pessoais (seção 9 do contrato)

- Nunca registre em log: `cpf`, `cnh`, `telefone`, `nome`, `email`, contatos de emergência, ficha médica, coordenadas exatas (nem a posição do voluntário), senhas ou tokens. Logs usam ids (GUID) e o código do capacete.
- Respostas mascaradas: CPF `***.***.***-12`, CNH `*******8900`, celular `(11) *****-1234`, telefone fixo `(11) ****-4567` e e-mail `r***@exemplo.com`. A ficha médica completa só volta no `GET` e no `PUT /api/v1/entregadores/{id}/ficha-medica`, para o próprio entregador e com `Cache-Control: no-store`; no Cofre saem só `tipoSanguineo`, `alergias` e `condicoesCriticas` (seção 7 do contrato).
- Dados de seed são sempre simulados.

## Testes (`HelmCare.Testes`)

- MSTest 4, com `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]`.
- Proibido usar banco, rede, broker MQTT, Docker, EF Core InMemory ou SQLite em memória. Os dados ficam atrás de interfaces, e os testes usam fakes escritos à mão em `tests/HelmCare.Testes/Fakes/`. Tempo controlado com `FakeTimeProvider` quando necessário.
- Teste o comportamento e o efeito (estado, campos, exceção), não apenas que um método foi chamado.
- Nome dos testes: `Metodo_Cenario_ResultadoEsperado`, com Arrange, Act e Assert.

## Segurança e higiene

- Nunca versione `bin/`, `obj/`, `.vs/`, `TestResults/`, `.env`, bancos ou `*.user`.
- Nenhuma senha, token ou connection string com senha em arquivo versionado, inclusive `appsettings*.json`, testes e `.http`. A connection string vem de user-secrets (Development) ou da variável `ConnectionStrings__HelmCareApi`; quem configura é o Renan.
- Conferência de higiene:

```shell
git status --short --untracked-files=all api | grep -E '(^|/)(bin|obj|\.vs|TestResults)/|(^|/)\.env$|\.(db|user)$' || echo "ok: higiene"
```

Esse comando lista o que entraria no próximo commit (arquivos novos e alterados que o `.gitignore` não esconde) e acusa qualquer item proibido.

## Git

- Trabalhe na branch atual e deixe as alterações sem commit. Quem commita é o Renan.
- No fim de cada etapa, sugira os commits: para cada um, a mensagem em Conventional Commits, em português, com escopo `api` (`feat(api): ...`, `test(api): ...`, `chore(api): ...`, `docs(api): ...`), e a lista exata de arquivos.
- Nunca rode `git add`, `git commit`, push, PR ou merge.
- As mensagens de commit sugeridas e os textos de PR não levam nenhuma linha de atribuição nem menção a ferramentas de IA (nada de Co-Authored-By ou Claude-Session).

## Ao terminar cada etapa, mostre

1. Arquivos criados ou alterados, com uma linha sobre cada um.
2. A saída real, sem resumir, das linhas finais de `dotnet build` (contagem de avisos e erros), de `dotnet test` (totais) e, quando houver, de `dotnet ef`.
3. `git status --short` (os arquivos novos e alterados, ainda sem commit).
4. A saída da conferência de higiene (seção "Segurança e higiene").
5. Os commits sugeridos (mensagem e arquivos de cada um).
6. Qualquer decisão que não estava no pedido ou desvio do pedido, com o motivo.