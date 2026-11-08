# CLAUDE.md · API principal do HelmCare (C#)

Este arquivo orienta o Claude Code quando ele trabalha na pasta `api/` do monorepo `helmcare`. Leia tudo antes de qualquer etapa.

## Contexto

- Global Solution 2026.2 da FIAP, disciplina **C# Software Development** (Prof. Rafael Eduardo Feliciano Cassiolato). Aluno: Renan Dias Utida (RM 558540, turma 3ESPW).
- A API é o núcleo do HelmCare: ASP.NET Core (.NET 10), porta 5000, prefixo `/api/v1`, PostgreSQL (banco `helmcare_api`) com EF Core e migrations. Ela recebe os eventos do capacete por MQTT, orquestra o SOS escalonado, atende o app e serve a página do Cofre médico.
- Rubrica (10,0): POO 3,0 · Web API 2,5 · EF Core + LINQ 1,5 · Async/Await 1,0 · Qualidade do código 2,0.

## Papéis

- **Renan:** decide, revisa e faz os commits, o push, a PR e o merge.
- **Claude Code (você):** cria e edita arquivos dentro de `api/`, compila, roda testes e migrations e mostra a saída real. Não faz commits.
- **Nunca faça:** `git add`, `git commit`, push, PR, merge, troca de branch, rebase, reset, stash, `docker compose down -v`, `docker volume rm` ou `prune`. Nunca abra `.env`, user-secrets ou qualquer arquivo com senha.

## Fonte da verdade

1. `docs/contrato-integracao.md` (versão 1.2) mais as decisões da versão 1.3 listadas abaixo, que já valem.
2. Este arquivo.

Nunca renomeie entidades, campos, enums, rotas, payloads ou tópicos MQTT do contrato. Se algo do contrato parecer impossível ou contraditório, pare e me avise, explicando o motivo, sem improvisar.

### Decisões da v1.3 (implementar já)

- Campos internos permitidos: `Dispositivo.EstadoAtual`, `EstadoAtualizadoEm`, `UltimoIndiceCalorC`, `UltimoIndiceCalorEm`; `EventoImpacto.ContagemTerminaEm` e `OcorridoEmEstimado`; `Turno.FimPrevisto`, `ComponenteSinistro`, `ComponenteChuva`, `ComponenteCalor` e `Explicacao`; `AcessoCofre.EntregadorId`; tabela `EventoMqttProcessado` (idempotência por dispositivo, tipo e ts).
- SOS sem impacto em aberto: criar o `EventoImpacto` com `OcorridoEm = ts - 25 s` (`JANELA_OBSERVACAO_S + CONTAGEM_CANCELAMENTO_S`) e `OcorridoEmEstimado = true`.
- Encerrar sem aceite é permitido: `SOS_CAMADA_1` ou `SOS_CAMADA_2` podem ir para `ENCERRADA`, com `TempoAteAceiteSegundos` nulo.
- Cofre: página `GET /cofre/{qrToken}` (HTML, fora de `/api/v1`); `POST /api/v1/cofre/{qrToken}/acesso` e `GET /api/v1/cofre/auditoria/{entregadorId}` (JSON).
- `GET /api/v1/turnos/{id}/alerta` inclui `indiceCalorC` (última telemetria ou null). Constante `ALERTA_CALOR_C = 32`.
- O capacete só aceita `DESARMAR` em `ARMADO`. Encerrar turno com o capacete em `OBSERVACAO`, `CONTAGEM` ou `SOS_ENVIADO`, ou com ocorrência ativa, responde 409.
- Opcional (só quando pedido): `GET /api/v1/indicadores`.

## Escopo de escrita

- Só dentro de `api/`.
- `.github/workflows/` somente quando o pedido disser explicitamente.
- Nunca altere outras pastas, o `README.md` da raiz, o `prompts.md`, o `.gitignore`, o `.editorconfig` ou o `.gitattributes` da raiz.

## Estrutura

```
api/
├── CLAUDE.md · README.md · HelmCare.slnx · global.json
├── Directory.Build.props · .editorconfig (root = true) · .gitattributes (eol=lf)
├── compose.yaml (só PostgreSQL) · .env.example · evidencias/
├── src/
│   ├── HelmCare.Dominio/      Comum/ · Entidades/ · Enums/ · ObjetosDeValor/
│   └── HelmCare.Api/          Controllers/ · Contratos/ · Servicos/ · Persistencia/ · Mensageria/
│                              Integracoes/ · Seguranca/ · Configuracao/ · wwwroot/cofre/
└── tests/
    └── HelmCare.Testes/       Dominio/ · Servicos/ · Mensageria/ · Fakes/
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

### Máquina de estados da `Ocorrencia`

| Estado atual | Ação | Próximo | Efeitos no domínio |
|---|---|---|---|
| (nova) | `Abrir` (evento SOS) | `SOS_CAMADA_1` | `Camada = 1`, `CriadaEm`, coordenadas do evento |
| `SOS_CAMADA_1` | `EscalarParaCamada2` (60 s após `CriadaEm`, sem aceite) | `SOS_CAMADA_2` | `Camada = 2` |
| `SOS_CAMADA_1` ou `SOS_CAMADA_2` | `Aceitar` | `SOCORRO_A_CAMINHO` | `AceitaEm`, cria `AceiteSocorro`, `TempoAteAceiteSegundos = AceitaEm - EventoImpacto.OcorridoEm` |
| `SOS_CAMADA_1` ou `SOS_CAMADA_2` | `Cancelar` | `CANCELADA` | `EncerradaEm` |
| `SOS_CAMADA_1` ou `SOS_CAMADA_2` | `Encerrar` (SAMU chegou antes) | `ENCERRADA` | `EncerradaEm`, `TempoAteAceiteSegundos` nulo |
| `SOCORRO_A_CAMINHO` | `Encerrar` | `ENCERRADA` | `EncerradaEm` |
| qualquer outra combinação | | não muda | `TransicaoInvalidaException`, nenhum campo alterado |

`ENCERRADA` e `CANCELADA` são finais. Os comandos MQTT de cada transição (`SOCORRO_A_CAMINHO`, `ENCERRAR`) são enviados pela camada de serviço, não pelo domínio.

## Dados pessoais (seção 9 do contrato)

- Nunca registre em log: `cpf`, `cnh`, `telefone`, `nome`, contatos de emergência, ficha médica, coordenadas exatas, senhas ou tokens. Logs usam ids (GUID) e o código do capacete.
- Respostas mascaradas: CPF `***.***.***-12`, telefone `(11) *****-1234`. A ficha médica só aparece na resposta de `POST /api/v1/cofre/{qrToken}/acesso`.
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

## Ao terminar cada etapa, mostre

1. Arquivos criados ou alterados, com uma linha sobre cada um.
2. A saída real, sem resumir, das linhas finais de `dotnet build` (contagem de avisos e erros), de `dotnet test` (totais) e, quando houver, de `dotnet ef`.
3. `git status --short` (os arquivos novos e alterados, ainda sem commit).
4. A saída da conferência de higiene (seção "Segurança e higiene").
5. Os commits sugeridos (mensagem e arquivos de cada um).
6. Qualquer decisão que não estava no pedido ou desvio do pedido, com o motivo.