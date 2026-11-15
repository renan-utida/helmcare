# CLAUDE.md - Serviço de Risco do HelmCare (Python)

Este arquivo orienta o Claude Code quando ele trabalha na pasta `servico-risco/` do monorepo `helmcare`. Leia tudo antes de qualquer etapa.

## Contexto

- Global Solution 2026.2 da FIAP, disciplina **Arquitetura Orientada a Serviços (SOA) e Web Services** (Prof. Thiago Dourado). Aluno: Renan Dias Utida (RM 558540, turma 3ESPW). Entrega em 19/11/2026, às 23h55.
- O Serviço de Risco é um serviço **independente** em Python 3.13 e FastAPI, porta 8000, prefixo `/risco/v1`, com PostgreSQL próprio (banco `helmcare_risco`, porta 5433 no desenvolvimento local). Ele calcula o Índice de Risco do Turno (modelo de ML + chuva + calor) e fornece os trechos críticos. Quem o chama é a API principal em C#; o app nunca o chama direto.
- Rubrica (10,0): Estruturação do domínio 3,0, Conexão com banco 3,0, Migrações 1,5, Documentação automática 1,5, Testes automatizados 1,0. Entregáveis: código completo, README com instruções e integrante, documentação dos endpoints (Swagger) e evidências.
- O Renan precisa explicar cada linha na defesa. Prefira código simples e comentado nas decisões a código esperto.

## Papéis

- **Renan:** decide, revisa e faz os commits, o push, a PR e o merge.
- **Claude Code (você):** cria e edita arquivos dentro de `servico-risco/`, instala dependências no `.venv`, roda testes, linter e migrações e mostra a saída real. Não faz commits.
- **Nunca faça:** `git add`, `git commit`, push, PR, merge, troca de branch, rebase, reset, stash, `docker compose down -v`, `docker volume rm` ou `prune`. Nunca rode `alembic downgrade` no banco de desenvolvimento (só no banco de testes, ou quando o pedido disser).
- **Segredos:** o `servico-risco/.env` já existe e é ignorado pelo Git. Nunca o abra, imprima (`cat`, `echo`, `grep` no conteúdo, `env`, `printenv`), copie nem versione, e nunca abra outro arquivo com senha. O `.env.example` leva **só os nomes** das variáveis, com valor vazio (`POSTGRES_PASSWORD=`, `RISCO_DATABASE_URL=`, `RISCO_TESTES_DATABASE_URL=`); os valores padrão das demais variáveis ficam no `config.py` e no README. O aplicativo e o `docker compose` leem o `.env` sozinhos, sem você abrir o arquivo.

## Ambiente (Windows, VS Code, Git Bash)

- O Renan usa a extensão do Claude Code no VS Code, com a raiz `helmcare` aberta, no Windows com **Git Bash**. Todo comando precisa funcionar no Git Bash: barras normais nos caminhos, nada de PowerShell, `cmd` ou `.bat`.
- Python 3.13.17 (o `py` do Windows já aponta para ele). Confirme com `py --version`. O ambiente virtual fica em `servico-risco/.venv`, criado a partir da raiz com `py -3.13 -m venv servico-risco/.venv`. O Renan o ativa no terminal dele com `source servico-risco/.venv/Scripts/activate`.
- **Você não depende de ativação** (ela não persiste entre chamadas do Bash). Chame sempre o interpretador do venv: `.venv/Scripts/python -m pip`, `.venv/Scripts/python -m pytest`, `.venv/Scripts/python -m ruff`, `.venv/Scripts/python -m alembic`, `.venv/Scripts/python -m uvicorn`.
- Cada chamada do Bash começa com `cd servico-risco && PYTHONUTF8=1 ...`, porque o diretório atual não é confiável entre chamadas e o console do Windows usa cp1252 por padrão.
- No código, todo `open`, `read_text` e `write_text` leva `encoding="utf-8"` (os JSONs de `ml/modelo/` têm acentos e o padrão do Windows é cp1252).
- Fim de linha: crie `servico-risco/.gitattributes` com `* text=auto eol=lf` (como o da `api/`), para o checkout no Windows não virar CRLF. No `pyproject.toml`, `line-ending = "auto"` no `ruff format`.

## Fonte da verdade

1. `docs/contrato-integracao.md` (**versão 1.10**), seções 2, 6.1, 8 e 8.1.
2. Este arquivo.
3. Os artefatos de `ml/modelo/` (seção 8.1 do contrato).

Nunca renomeie rotas, campos, enums ou valores do contrato, e **não acrescente campos** às respostas. Se algo parecer impossível ou contraditório, pare e avise com o título **MUDANÇA DE CONTRATO**, explicando o motivo, sem improvisar.

## Escopo de escrita

- Só dentro de `servico-risco/`.
- Nunca altere `docs/`, `ml/`, `api/`, o `README.md` da raiz, o `prompts.md`, o `.editorconfig` ou o `.gitattributes` da raiz. Os arquivos de `ml/modelo/` só são lidos.
- **Única exceção, na Rodada 1:** acrescentar ao `.gitignore` da raiz a linha `.ruff_cache/`, na seção Python, logo depois de `.pytest_cache/`. O `.gitignore` já ignora `.env`, `.venv/`, `__pycache__/`, `.pytest_cache/`, `.coverage` e `htmlcov/`; não repita essas linhas.

## Estrutura

```
servico-risco/
├── CLAUDE.md  README.md  .env.example  compose.yaml
├── requirements.txt  requirements-dev.txt  pyproject.toml (ruff e pytest)  alembic.ini
├── app/
│   ├── main.py  config.py
│   ├── dominio/          faixa_risco, indice_calor, componentes, calculadora_indice,
│   │                     horas_turno, agregacao, calendario, explicacao,
│   │                     trecho_critico, excecoes, portas
│   ├── aplicacao/        servico_indice_turno, servico_trechos_criticos, servico_saude
│   ├── infraestrutura/
│   │   ├── ml/           artefatos, previsor_xgboost
│   │   └── persistencia/ base, modelos, sessao, repositorios, semeador_trechos
│   └── api/              rotas_risco, esquemas, erros, dependencias
├── migrations/           env.py, versions/
├── tests/                dominio/  aplicacao/  api/  integracao/  fakes/
└── evidencias/
```

- Camadas e dependências (sempre para dentro): `api` e `infraestrutura` dependem de `aplicacao`, que depende de `dominio`. **O `dominio` não importa FastAPI, Pydantic, SQLAlchemy, xgboost, numpy nem `app.config`.** A infraestrutura implementa as portas (`typing.Protocol`) declaradas em `dominio/portas.py`.
- Rotas finas: validam a entrada, chamam um serviço de `aplicacao` e devolvem o esquema de saída. Nenhuma regra de negócio e nenhum SQL na rota.

## Comandos (Git Bash, a partir da pasta `servico-risco/`)

Cada linha abaixo roda como `cd servico-risco && PYTHONUTF8=1 <comando>` (o `cd` e a variável vão no começo de cada chamada do Bash).

```shell
# uma vez, a partir da raiz do repositório
py -3.13 -m venv servico-risco/.venv

# a partir de servico-risco/
.venv/Scripts/python -m pip install -r requirements-dev.txt
docker compose up -d
.venv/Scripts/python -m alembic upgrade head
.venv/Scripts/python -m uvicorn app.main:app --host 127.0.0.1 --port 8000
.venv/Scripts/python -m pytest
.venv/Scripts/python -m ruff check .
.venv/Scripts/python -m ruff format --check .
```

A conferência de higiene e o `git status` rodam na **raiz** do repositório.

## Pacotes e versões

- Fixas, por serem as do notebook de ML (`ml/modelo/metadados_modelo.json`): `xgboost==3.4.1`, `holidays==0.108`, `numpy==2.4.6`. Se alguma não restaurar, pare e reporte; não troque por conta própria.
- Sem `pandas`: o `numpy` basta para montar a entrada do modelo.
- `tzdata` obrigatório (o `zoneinfo` não tem o banco de fusos no Windows).
- Demais pacotes: instale a versão estável atual, depois grave **todas as versões exatas** (`==`) em `requirements.txt` (execução: `fastapi`, `uvicorn[standard]`, `sqlalchemy` 2.x, `alembic`, `psycopg[binary]` 3, `pydantic` 2 e `pydantic-settings`) e em `requirements-dev.txt` (`-r requirements.txt`, `pytest`, `pytest-cov`, `httpx`, `ruff`). Informe a lista final na entrega.
- SQLAlchemy **síncrono** (2.0, `Mapped[]`, `DeclarativeBase`) com `psycopg` 3 e rotas `def`: o cálculo do XGBoost é CPU, e o FastAPI roda rotas `def` em thread pool. Registre esse motivo no README.

## Convenções de código

- Python 3.13, type hints em tudo. `ruff` com 0 avisos (regras mínimas: `E`, `F`, `I`, `UP`, `B`), configurado no `pyproject.toml`. Nunca use `# noqa` para esconder aviso; corrija a causa.
- Idioma do domínio em **português sem acento** nos identificadores (`TrechoCritico`, `calcular_indice`, `previsao`). Classes em PascalCase, funções e arquivos em snake_case, constantes em UPPER_SNAKE_CASE. Termos do framework ficam em inglês.
- Docstring em português em todo módulo, classe e função pública, explicando o propósito e as regras. Comentário dentro do código só onde a decisão não é óbvia.
- Objetos de valor como `@dataclass(frozen=True)`. Exceções de domínio herdam de `DominioError`: `ValidacaoDominioError` (vira 400) e `RegraNegocioError` (vira 409, se precisar).
- **Proibido usar travessão (U+2014) ou meio-traço (U+2013)** em código, comentários, strings (inclusive a `explicacao`), mensagens, commits e documentação. Use vírgula, dois-pontos ou parênteses. Arquivos em UTF-8 com fim de linha LF.
- Datas sempre `datetime` com fuso. Na entrada, aceite qualquer fuso e normalize para UTC; o modelo usa o horário local de `America/Sao_Paulo`. O domínio recebe o instante como parâmetro: o relógio entra por uma porta `Relogio` (nos testes, um relógio fixo). Nunca `datetime.now()` direto no domínio ou nos serviços.
- JSON em camelCase (`ConfigDict(alias_generator=to_camel, populate_by_name=True)`), enums em UPPER_SNAKE_CASE, ids em UUID, datas em ISO 8601 UTC com `Z`. Campos desconhecidos na requisição são ignorados.
- Arredondamento: **0,5 para cima**, nunca o `round()` do Python (que é bancário). Use uma função única `arredondar_meio_para_cima` em `dominio/componentes.py`, com uma tolerância de 1e-9 contra ruído de ponto flutuante, e documente o motivo.
- O `numpy.float32` do XGBoost vira `float` do Python na fronteira do previsor.

## Regras do domínio (seção 8 e 8.1 do contrato)

**Horas do turno** (`horas_turno`): do `inicio`, arredondado para baixo na hora cheia (UTC), percorra as horas cheias enquanto a hora começar antes do `fim`. Para cada hora guarde: início UTC, início local (`America/Sao_Paulo`), `hora`, `dia_semana` (0 = segunda), `mes`, `feriado` (0 ou 1) e os **segundos** que a hora fica dentro do turno (`min(fim, hora + 1h) - max(inicio, hora)`). Um `fim` exatamente na hora cheia não inclui a hora seguinte. Turno de 24 h exatas alinhado dá 24 horas; desalinhado dá 25.

**Calendário** (`calendario`): consulta a lista `feriados.datas` de `metadados_modelo.json` (2019 a 2030). Fora desse período aplica a mesma regra: nacionais e estaduais de SP pela biblioteca `holidays` (`Brazil`, `subdiv="SP"`), mais 25/01, Corpus Christi (Páscoa + 60 dias), 20/11 e o Carnaval (segunda = Páscoa menos 48 dias, terça = Páscoa menos 47). Um teste prova que a regra reproduz a lista inteira de 2019 a 2030.

**Previsão por hora** (porta `PrevisorSinistros`, implementada em `infraestrutura/ml/previsor_xgboost.py`): `xgboost.Booster().load_model(...)`, uma vez no startup. O modelo tem nomes de variáveis, então o `DMatrix` leva `feature_names=["hora","dia_semana","mes","feriado"]`, nesta ordem (sem nomes, o XGBoost recusa a previsão). Todas as horas do turno vão numa única chamada `predict`.

**Componente `sinistro`:** para cada hora, `min(100, 100 * previsao / p95)`, **sem arredondar** (p95 de `p95_sinistros_graves.json`, 0,322927). O componente é a **média ponderada** desses valores pelos segundos de cada hora dentro do turno, com **um único arredondamento no fim**. O passo 6 de `metadados_modelo.json` (arredondar por hora) **não é usado**.

**Componente `chuva`:** `chuvaMmHora / 10 * 100`, limitado a 0..100, arredondado uma vez.

**Componente `calor`:** índice de calor da NOAA (função `indice_calor.calcular_c`) de `temperaturaC` e `umidadePct`; `(indiceCalorC - 27) / (41 - 27) * 100`, limitado a 0..100, arredondado uma vez. A fórmula é o porte exato da `IndiceCalor.CalcularC` do C#, em Fahrenheit e de volta a Celsius:

```
t = c * 1.8 + 32 ; u = umidade
indice = 0.5 * (t + 61.0 + ((t - 68.0) * 1.2) + (u * 0.094))
se indice > 79:
    indice = -42.379 + 2.04901523*t + 10.14333127*u - 0.22475541*t*u - 0.00683783*t*t
             - 0.05481717*u*u + 0.00122874*t*t*u + 0.00085282*t*u*u - 0.00000199*t*t*u*u
    se u < 13 e 80 <= t <= 112:  indice -= (13 - u) * 0.25 * sqrt((17 - abs(t - 95)) * 0.05882)
    senão se u > 85 e 80 <= t <= 87:  indice += (u - 85) * 0.1 * ((87 - t) * 0.2)
retorno (indice - 32) * 0.55555
```

Valores de referência (testes): 33,4 °C e 58% dão 39,8 °C; 23,5 °C e 58% dão 23,4 °C (uma casa decimal).

**Índice:** `0.5 * sinistro + 0.25 * chuva + 0.25 * calor`, calculado sobre os **componentes já arredondados**, arredondado uma vez e limitado a 0..100. Faixas: 0 a 25 `BAIXO`, 26 a 50 `MODERADO`, 51 a 75 `ALTO`, 76 a 100 `MUITO_ALTO`.

**Resposta** (exatamente estes campos): `indiceRisco`, `faixaRisco`, `componentes` (`sinistro`, `chuva`, `calor`) e `explicacao`.

**Explicação** (`explicacao.py`, função pura, texto fixo e testável), uma frase só, no formato do exemplo da seção 7 do contrato:
- Abre com `Risco {faixa em minúsculas e com espaço}: ` (`baixo`, `moderado`, `alto`, `muito alto`), junta os trechos com vírgula e " e " antes do último, e termina com ponto final.
- Ordem dos trechos: chuva, histórico do horário e calor.
- Chuva (componente 1 ou mais): "chuva prevista no turno". Sem citar a hora da chuva.
- Histórico do horário (sempre): componente sinistro 51 ou mais, "o seu horário tem histórico de sinistros graves (pico às {hora}h)"; de 26 a 50, "o seu horário tem histórico moderado de sinistros graves"; até 25, "o seu horário tem poucos sinistros graves no histórico". A `{hora}` é a hora local de maior valor entre as horas do turno (a primeira, em caso de empate).
- Calor (componente 1 ou mais): "calor previsto no turno (índice de calor de {x,x} °C)", com uma casa decimal e vírgula.
- Exemplo de referência: "Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros graves (pico às 19h)."
- **Nunca** diga que a chuva aumenta, eleva ou causa sinistros ou acidentes. Um teste varre as combinações de componentes e falha se a frase relacionar chuva e aumento de sinistros.

**Exemplo de referência dos testes** (seção 8 do contrato): `POST /risco/v1/indice-turno` com `inicio` 2026-11-12T20:00:00Z, `fim` 2026-11-13T00:00:00Z e previsão 7,0 mm/h, 24,0 °C e 85% dá as horas locais 17h, 18h, 19h e 20h de quinta-feira (previsões 0,26925; 0,27508; 0,28257; 0,25319), `sinistro` 84 (média 83,62), `chuva` 70, `calor` 0 (índice de calor 24,7 °C), `indiceRisco` 60 e faixa `ALTO`.

## API (`/risco/v1`)

| Método | Rota | Resposta |
|---|---|---|
| POST | `/risco/v1/indice-turno` | 200 com o índice; 400 em ProblemDetails; 503 se o modelo não estiver carregado |
| GET | `/risco/v1/trechos-criticos?limite=10` | 200 `{ "periodo", "trechos": [...] }` em ordem decrescente de `quantidadeSinistrosGraves`; 400 se `limite` fora de 1 a 50; 503 se o banco falhar ou não houver trechos carregados |
| GET | `/risco/v1/saude` | 200 `{ "status": "Healthy" }` ou 503 `{ "status": "Unhealthy" }` (banco com `SELECT 1` e timeout curto, e modelo carregado) |

- Documentação em `/risco/v1/docs` e esquema em `/risco/v1/openapi.json` (configure `docs_url` e `openapi_url`, porque o nginx repassa `/risco` sem remover o prefixo). Título, descrição, tags, `summary` e exemplos em português; declare as respostas 400 e 503 em cada rota; o exemplo de referência vai no esquema da requisição e da resposta.
- **Validação da requisição (400, não 422):** `inicio` e `fim` com fuso (use `AwareDatetime`; data sem fuso é recusada); `fim` depois do `inicio` e no máximo 24 h depois (exatamente 24 h passa); `previsao` obrigatória com `chuvaMmHora` de 0 a 200, `temperaturaC` de -10 a 50 e `umidadePct` de 0 a 100. O FastAPI devolve 422 por padrão: sobrescreva o tratador de `RequestValidationError` e converta `ValidacaoDominioError`.
- **Erros** em `application/problem+json` (RFC 9457/7807): `type` (URL da seção do RFC 9110, como o ASP.NET faz), `title`, `status`, `detail` em português e, na validação, `errors` (campo para lista de mensagens). Também 404 e 405 do roteador e 500 genérico, **sem stack trace e sem eco do corpo**.
- **Histórico:** cada `POST /indice-turno` grava `calculo_indice` e `calculo_indice_hora` numa só transação. Se a gravação falhar (inclusive banco fora do ar), a rota **responde 200 com o índice** e registra no log o id do cálculo e o tipo do erro (`logger.exception`, sem corpo da requisição). Não existe rota para consultar o histórico.
- Estado: o serviço não guarda sessão. O modelo é carregado no startup (`lifespan`) e só é lido depois. Se o modelo não carregar, o serviço sobe mesmo assim, registra o erro crítico, e `/saude` e `/indice-turno` respondem 503. O startup também não falha com o banco fora do ar.

## Banco de dados

PostgreSQL 17.6. URL em `RISCO_DATABASE_URL` (formato `postgresql+psycopg://helmcare:SENHA@127.0.0.1:5433/helmcare_risco`), lida por `pydantic-settings`; nunca em arquivo versionado. Outras variáveis (todas com prefixo `RISCO_`): `RISCO_MODELO_DIR` (padrão: `ml/modelo` do repositório, resolvido a partir do arquivo `config.py`, nunca do diretório atual), `RISCO_CARREGAR_TRECHOS_NA_INICIALIZACAO` (padrão `true`), `RISCO_TESTES_DATABASE_URL` (só para os testes de integração).

`config.py`: `SettingsConfigDict(env_prefix="RISCO_", env_file=<caminho absoluto do .env de servico-risco, a partir do arquivo config.py>, env_file_encoding="utf-8", extra="ignore")`. O `extra="ignore"` é obrigatório: o `.env` também guarda a `POSTGRES_PASSWORD` do compose (sem o prefixo `RISCO_`), e sem ele o `pydantic-settings` recusa a chave. O Renan acrescenta no `.env` as linhas `RISCO_DATABASE_URL` e `RISCO_TESTES_DATABASE_URL` antes da Rodada 2; você nunca as escreve nem as lê.

`compose.yaml`: o PostgreSQL da API já roda no Docker na porta 5432, então este compose precisa ser totalmente separado dele. `name: helmcare-risco` (a API usa `helmcare-api`), serviço `postgres` com `container_name: helmcare-risco-postgres`, volume com nome próprio `dados-postgres-risco`, imagem `postgres:17.6`, portas `127.0.0.1:5433:5432`, banco `helmcare_risco`, usuário `helmcare`, senha de `${POSTGRES_PASSWORD:?Configure servico-risco/.env}`, healthcheck com `pg_isready`. Nada de `network_mode` nem de volume ou rede compartilhados com a API. `.env.example` só com os nomes (veja Segredos) e um comentário de como gerar a senha (`openssl rand -hex 16`). Nenhuma senha versionada.

**503 por degradação (aprovado):** modelo que não carrega ou banco fora do ar no startup não derrubam o serviço: ele sobe, e `/saude` e `/indice-turno` respondem 503. A API em C# trata esse 503 como falha do serviço e usa a versão simulada. Isso entra na próxima versão do contrato (1.11); até lá, o texto da seção API deste arquivo é a regra.

Tabelas (nomes no singular, colunas em snake_case, tipos do SQLAlchemy 2: `Uuid`, `DateTime(timezone=True)`, `Float` para medidas e coordenadas). Defina `naming_convention` no `MetaData` para os nomes de constraints serem previsíveis.

| Tabela | Colunas |
|---|---|
| `conjunto_trechos_criticos` | `id` (uuid, PK), `periodo`, `fonte`, `criterio`, `hash_arquivo` (sha256 do JSON, único), `carregado_em` |
| `trecho_critico` | `id` (uuid, PK), `conjunto_id` (FK), `posicao`, `logradouro`, `latitude`, `longitude`, `quantidade_sinistros_graves`. Única em (`conjunto_id`, `posicao`). CHECK: `posicao >= 1`, `quantidade_sinistros_graves >= 0` |
| `calculo_indice` | `id` (uuid, PK), `calculado_em`, `inicio`, `fim`, `chuva_mm_hora`, `temperatura_c`, `umidade_pct`, `indice_calor_c`, `componente_sinistro`, `componente_chuva`, `componente_calor`, `indice_risco`, `faixa_risco`, `explicacao`, `versao_modelo`, `regra_agregacao` (texto fixo `MEDIA_PONDERADA_MINUTOS_V1`). CHECK: componentes e índice de 0 a 100, `faixa_risco` entre as 4 do contrato |
| `calculo_indice_hora` | `id` (uuid, PK), `calculo_id` (FK, `ON DELETE CASCADE`), `hora_inicio_utc`, `hora_local`, `dia_semana`, `mes`, `feriado` (bool), `previsao_sinistros`, `componente_sinistro_hora` (valor normalizado, sem arredondar), `minutos_no_turno`. Única em (`calculo_id`, `hora_inicio_utc`) |

- **Sem dado pessoal** em nenhuma tabela (a requisição nem tem `entregadorId`). A `versao_modelo` vem de `versaoModelo` do `metadados_modelo.json`.
- **Migrações Alembic** escritas ou revisadas à mão, com `upgrade` e `downgrade` funcionando: `0001_criar_trechos_criticos` (as duas primeiras tabelas) e `0002_criar_historico_calculos` (as duas últimas). `migrations/env.py` lê a URL de `app.config`. Não invente uma terceira revisão sem necessidade real.
- **Carga dos trechos** (`semeador_trechos`): idempotente, pelo `hash_arquivo`. Lê `trechos_criticos.json`, valida (`quantidadeTrechos` igual ao tamanho da lista, no mínimo 50, `posicao` de 1 a N, ordem decrescente) e grava o conjunto e os trechos numa transação. Se o hash já existe, não faz nada. O repositório lê o conjunto mais recente (`carregado_em`). Roda no startup (se a configuração permitir) e por `python -m app.infraestrutura.persistencia.semeador_trechos`. Falha na carga é registrada no log e não derruba o serviço.
- `create_engine` com `pool_pre_ping=True` e `connect_timeout` curto (3 s). Sessão por requisição (dependência do FastAPI), com commit explícito no repositório.

## Testes

- pytest. Nome: `test_<unidade>_<cenario>_<resultado>`, com Arrange, Act e Assert.
- **Unitários e de API usam fakes escritos à mão** em `tests/fakes/` (repositórios em memória, previsor falso, relógio fixo), sem banco e sem Docker. Os testes de API usam o `TestClient` do FastAPI com as dependências trocadas. O artefato real do modelo pode ser lido nos testes do previsor e no teste do exemplo de referência (é um arquivo local, rápido).
- **Integração** (`@pytest.mark.integracao`, em `tests/integracao/`): só rodam com `RISCO_TESTES_DATABASE_URL` definida (um banco separado, `helmcare_risco_teste`, que o Renan cria); sem ela, são ignorados com motivo claro. Cobrem: `alembic upgrade head` e `downgrade base`, o semeador idempotente (100 trechos, segunda carga sem duplicar), a leitura dos trechos com `limite`, o `POST /indice-turno` gravando `calculo_indice` e as 4 linhas de `calculo_indice_hora` do exemplo de referência, e um CHECK rejeitando componente 101.
- O que os testes precisam provar (mínimo): calor de referência (39,8 e 23,4); componentes nos limites (chuva 0, 5, 10 e 200 mm/h; calor 27, 34 e 41 °C); faixas nas fronteiras 25/26, 50/51 e 75/76; 80, 70 e 30 dão 65 `ALTO`; meio para cima (64,5 vira 65; 83,5 vira 84); horas do turno (exemplo UTC para 17h a 20h, hora parcial com peso proporcional, fim na hora cheia, 24 h alinhado e desalinhado, fuso -03:00 normalizado, data sem fuso, `fim` menor ou igual ao `inicio`, mais de 24 h); média ponderada; feriados (20/11/2026 é feriado, 12/11/2026 não, e a regra igual à lista); previsor (ordem das variáveis, paridade com as 4 previsões do exemplo com tolerância de 1e-5, erro claro se o arquivo faltar); explicação (sem a relação chuva e sinistros, com "chuva prevista no turno" quando há chuva); API (todos os 400, incluindo `limite` 0, 51 e `abc`, com `Content-Type: application/problem+json`; 404 de rota inexistente em problem+json; `/saude` 200 e 503; trechos com limite padrão 10 e em ordem decrescente; trechos 503 quando o repositório falha; histórico que falha e o `POST` que continua 200); contrato (o `openapi.json` expõe as 3 rotas sob `/risco/v1` e os campos em camelCase).
- Relate a cobertura (`pytest --cov=app`) na entrega, com meta de pelo menos 90% em `app/dominio`.

## Dados pessoais e segurança (seção 9 do contrato)

- O serviço não recebe nem guarda dado pessoal. Nada de identificador de entregador, nome, telefone ou coordenada de pessoa nos logs ou no banco. As coordenadas dos trechos críticos são de vias públicas.
- Logs com ids de cálculo, duração e tipo de erro. Nunca o corpo da requisição.
- Nenhuma senha, token ou URL com senha em arquivo versionado (inclusive testes, `alembic.ini` e README). O `alembic.ini` não leva a URL: ela vem do `env.py`.
- Higiene, antes de cada entrega:

```shell
git status --short --untracked-files=all servico-risco | grep -E '(^|/)(__pycache__|\.venv|\.pytest_cache|\.ruff_cache|htmlcov)/|(^|/)\.env$|\.(pyc|db)$|\.coverage$' || echo "ok: higiene"
```

- Limitação a documentar no README: no protótipo o serviço não tem autenticação, porque só a API na mesma máquina o chama (nginx e rede local). Em produção entrariam mTLS ou JWT entre serviços, limite de requisições e rede restrita.

## Git

- Trabalhe na branch atual (`feature/soa-servico-risco`) e deixe as alterações sem commit.
- No fim de cada rodada, sugira os commits: mensagem em Conventional Commits, em português, com escopo `risco` (`feat(risco): ...`, `test(risco): ...`, `chore(risco): ...`, `docs(risco): ...`), e a lista exata de arquivos de cada um.

## Rodadas de trabalho

Os pedidos vêm em rodadas (o Renan junta missões por causa do prazo). Faça só a rodada pedida.

| Rodada | Conteúdo |
|---|---|
| **1** | Esqueleto, configuração, domínio completo com testes unitários e a camada de ML com o teste do exemplo de referência |
| **2** | Banco, migrações, repositórios, carga dos trechos, serviços de aplicação, rotas, erros em problem+json, saúde, OpenAPI, testes de API e de integração. No README, deixe pronto um exemplo de requisição e resposta do `POST /risco/v1/indice-turno` (o exemplo de referência, com o `curl`), que o Renan repassa para a conversa da API em C# |
| **3** | Fechamento dos testes e da cobertura, README completo, evidências e auditoria final. As evidências registram também a chamada de ponta a ponta (API em C# chamando o serviço, com tempo limite e a versão simulada de reserva), quando a API já a tiver; o entregável de quarta é o índice calculado de ponta a ponta |

Extras só se sobrar tempo, nesta ordem: cabeçalho `X-Correlation-Id` repassado aos logs; teste de carga leve com o `httpx`.

## Rodada 1: pedido

Faça, nesta ordem:

1. **Esqueleto:** `requirements.txt`, `requirements-dev.txt`, `pyproject.toml` (ruff e pytest, com `pythonpath = ["."]`, `testpaths = ["tests"]` e o marcador `integracao` registrado), `.env.example` (só nomes), `.gitattributes`, `compose.yaml` (escrito, ainda sem subir o Docker), pacotes `app/` e `tests/` com `__init__.py`, `app/config.py` (`pydantic-settings`, variáveis `RISCO_*`, sem nenhuma conexão com banco) e um `README.md` provisório (só como executar no Git Bash e a estrutura; o README final é da rodada 3). Acrescente `.ruff_cache/` ao `.gitignore` da raiz (única alteração fora de `servico-risco/`). Crie o `.venv` com `py -3.13 -m venv servico-risco/.venv`, instale as dependências e mostre as versões instaladas. **Não** crie rotas nem banco ainda, e **não** abra o `.env`.
2. **Domínio** (`app/dominio/`): `faixa_risco`, `indice_calor`, `componentes`, `calculadora_indice`, `horas_turno`, `agregacao`, `calendario`, `explicacao`, `trecho_critico`, `excecoes` e `portas` (`PrevisorSinistros`, `Relogio`, `RepositorioTrechosCriticos`, `RepositorioCalculos`), seguindo as regras acima, com os testes unitários de `tests/dominio/`.
3. **ML** (`app/infraestrutura/ml/`): `artefatos` (lê `metadados_modelo.json` e `p95_sinistros_graves.json`, valida `ordemVariaveis` e compara as versões do `xgboost` e do `holidays` instalados com as de `versoesBibliotecas`, registrando aviso em caso de diferença) e `previsor_xgboost` (implementa a porta). Teste de paridade com as 4 previsões do exemplo de referência e teste que monta o índice completo do exemplo (84, 70, 0, 60, `ALTO`) usando o previsor real e o domínio.

Ao terminar, mostre:

1. Os arquivos criados ou alterados, com uma linha sobre cada um.
2. A saída real, sem resumir, das linhas finais de `pytest` (totais e cobertura) e de `ruff check .` e `ruff format --check .`.
3. A lista final de versões instaladas (`pip freeze` das dependências diretas).
4. `git status --short` (arquivos novos e alterados, ainda sem commit) e a saída da conferência de higiene.
5. Os commits sugeridos (mensagem e arquivos de cada um).
6. Qualquer decisão que não estava no pedido ou desvio do pedido, com o motivo.
