# HelmCare - Serviço de Risco (Arquitetura Orientada a Serviços (SOA) e Web Services)

Serviço independente que calcula o Índice de Risco do Turno (modelo de ML + chuva + calor) e fornece os trechos críticos de sinistros graves. A API principal em C# consome este serviço via REST; o app nunca o chama direto.

Integrante: Renan Dias Utida (RM 558540, turma 3ESPW).

> Status: Rodada 2 concluída (banco, migrações, rotas, erros, saúde, OpenAPI e testes). As evidências e o fechamento do README vêm na Rodada 3.

## Tecnologias

Python 3.13 · FastAPI · SQLAlchemy 2 (síncrono) · psycopg 3 · Alembic · PostgreSQL 17.6 · XGBoost · Pydantic 2 · pytest · ruff

## Endpoints

Base: `http://localhost:8000/risco/v1`. Documentação interativa (Swagger) em [`/risco/v1/docs`](http://localhost:8000/risco/v1/docs) e esquema OpenAPI em [`/risco/v1/openapi.json`](http://localhost:8000/risco/v1/openapi.json).

| Método | Rota | Resposta |
|---|---|---|
| POST | `/risco/v1/indice-turno` | 200 com o índice; 400 em ProblemDetails; 503 se o modelo não estiver carregado |
| GET | `/risco/v1/trechos-criticos?limite=10` | 200 com `periodo` e `trechos` em ordem decrescente; 400 se `limite` fora de 1 a 50; 503 se o banco falhar ou não houver trechos |
| GET | `/risco/v1/saude` | 200 `{"status": "Healthy"}` ou 503 `{"status": "Unhealthy"}` |

As regras e a fórmula do índice estão na seção 8 do [contrato de integração](../docs/contrato-integracao.md).

### Exemplo: POST /risco/v1/indice-turno

É o exemplo de referência do contrato (quinta-feira, das 17h às 21h no horário de Brasília, 7 mm/h de chuva, 24 °C e 85% de umidade).

Requisição (Git Bash, com o serviço no ar):

```shell
curl -s -X POST http://127.0.0.1:8000/risco/v1/indice-turno \
  -H "Content-Type: application/json" \
  -d '{"inicio": "2026-11-12T20:00:00Z", "fim": "2026-11-13T00:00:00Z", "previsao": {"chuvaMmHora": 7.0, "temperaturaC": 24.0, "umidadePct": 85}}'
```

Resposta `200 OK` (`application/json`):

```json
{
  "indiceRisco": 60,
  "faixaRisco": "ALTO",
  "componentes": { "sinistro": 84, "chuva": 70, "calor": 0 },
  "explicacao": "Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros graves (pico às 19h)."
}
```

Erro de validação: `400 Bad Request` (`application/problem+json`), por exemplo com o `fim` antes do `inicio`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Requisição inválida",
  "status": 400,
  "detail": "Um ou mais campos da requisição são inválidos.",
  "errors": { "fim": ["O fim do turno deve ser depois do início."] }
}
```

Modelo não carregado: `503 Service Unavailable` (`application/problem+json`) com `"title": "Serviço indisponível"`. A API em C# deve tratar 503, erro de rede e tempo esgotado como falha do serviço e usar a sua versão simulada.

## Como executar (Windows, Git Bash)

Pré-requisitos: Python 3.13 (`py --version`) e Docker Desktop. Todos os comandos abaixo, menos o primeiro, rodam a partir de `servico-risco/`.

```shell
# 1. Ambiente virtual (uma vez, a partir da raiz do repositório)
py -3.13 -m venv servico-risco/.venv
cd servico-risco

# 2. Dependências
.venv/Scripts/python -m pip install -r requirements-dev.txt

# 3. Segredos: copie o modelo e preencha (nunca versione o .env)
cp .env.example .env
#   POSTGRES_PASSWORD=<gere com: openssl rand -hex 16>
#   RISCO_DATABASE_URL=postgresql+psycopg://helmcare:<senha>@127.0.0.1:5433/helmcare_risco
#   RISCO_TESTES_DATABASE_URL=postgresql+psycopg://helmcare:<senha>@127.0.0.1:5433/helmcare_risco_teste

# 4. Banco (PostgreSQL próprio na porta 5433, separado do banco da API na 5432)
docker compose up -d
docker compose exec postgres createdb -U helmcare helmcare_risco_teste   # banco dos testes, uma vez

# 5. Migrações
.venv/Scripts/python -m alembic upgrade head                  # banco helmcare_risco
.venv/Scripts/python -m alembic -x banco=teste upgrade head   # banco helmcare_risco_teste

# 6. Serviço (carrega o modelo e os trechos críticos no startup)
.venv/Scripts/python -m uvicorn app.main:app --host 127.0.0.1 --port 8000
```

A carga dos trechos também pode ser feita à mão (é idempotente):

```shell
.venv/Scripts/python -m app.infraestrutura.persistencia.semeador_trechos
```

No Windows, se o console mostrar acentos errados, ponha `PYTHONUTF8=1` na frente dos comandos.

## Testes

```shell
.venv/Scripts/python -m pytest                         # todos (unitários, API e integração)
.venv/Scripts/python -m pytest --cov=app               # com cobertura
.venv/Scripts/python -m pytest -m "not integracao"     # só os que não usam banco
.venv/Scripts/python -m ruff check .
.venv/Scripts/python -m ruff format --check .
```

- **Unitários e de API** (`tests/dominio`, `tests/aplicacao`, `tests/api`, `tests/infraestrutura`): sem banco e sem Docker. Os testes de API usam o `TestClient` do FastAPI com as dependências trocadas por fakes escritos à mão (`tests/fakes`: previsor, relógio e repositórios em memória). O modelo real é lido no teste do exemplo de referência.
- **Integração** (`tests/integracao`, marcador `integracao`): usam o banco de `RISCO_TESTES_DATABASE_URL` e são ignorados, com o motivo, quando ela não está definida. Recriam o esquema pelas migrações (`downgrade base` e `upgrade head`), conferem que modelos e migrações batem (`alembic check`), testam a carga idempotente, os repositórios, o histórico gravado e uma CHECK do banco. Por segurança, só rodam se o banco se chamar `*_teste` e for diferente do `RISCO_DATABASE_URL`.

## Configuração

Variáveis lidas por `app/config.py` (prefixo `RISCO_`, do ambiente ou do `servico-risco/.env`):

| Variável | Padrão | Uso |
|---|---|---|
| `RISCO_DATABASE_URL` | vazio | URL do banco `helmcare_risco` |
| `RISCO_TESTES_DATABASE_URL` | vazio | URL do banco dos testes de integração |
| `RISCO_MODELO_DIR` | `ml/modelo` do repositório | Pasta dos artefatos do modelo |
| `RISCO_CARREGAR_TRECHOS_NA_INICIALIZACAO` | `true` | Carga dos trechos críticos no startup |

O `.env` também guarda a `POSTGRES_PASSWORD`, lida pelo `docker compose`. As URLs são `SecretStr`: nunca aparecem em log, nem no `alembic.ini`.

## Banco de dados

Quatro tabelas, sem nenhum dado pessoal (a requisição nem tem identificador de entregador):

| Tabela | Conteúdo |
|---|---|
| `conjunto_trechos_criticos` | Uma carga do `trechos_criticos.json`, com o sha256 do arquivo (único) |
| `trecho_critico` | Os trechos de cada carga, com posição única por conjunto |
| `calculo_indice` | Cada cálculo: entradas, componentes, índice, faixa, explicação e versão do modelo |
| `calculo_indice_hora` | As horas de cada cálculo: variáveis do modelo, previsão, valor normalizado e minutos no turno |

Migrações escritas à mão em `migrations/versions/`: `0001_criar_trechos_criticos` e `0002_criar_historico_calculos`, com `upgrade` e `downgrade`.

## Estrutura

```
servico-risco/
  app/
    main.py                    aplicação FastAPI, startup e OpenAPI
    config.py                  configuração (pydantic-settings)
    dominio/                   regras do índice, sem framework, banco ou ML
    aplicacao/                 casos de uso: índice do turno, trechos críticos, saúde
    infraestrutura/
      ml/                      artefatos e previsor XGBoost
      persistencia/            modelos, sessão, repositórios e carga dos trechos
      relogio.py               relógio do sistema
    api/                       rotas, esquemas, erros (problem+json) e dependências
  migrations/                  Alembic (env.py e versions/)
  tests/                       dominio, aplicacao, api, infraestrutura, integracao, fakes
  alembic.ini  compose.yaml  pyproject.toml  requirements*.txt
```

As dependências apontam para dentro: `api` e `infraestrutura` dependem de `aplicacao`, que depende de `dominio`. O domínio declara as portas (`typing.Protocol`) e a infraestrutura as implementa; um teste garante que o domínio não importa FastAPI, Pydantic, SQLAlchemy, xgboost, numpy nem a configuração.

## Decisões de arquitetura

- **Degradação com 503 (decisão aprovada, entra no Contrato v1.11):** se o modelo não carregar ou o banco estiver fora do ar no startup, o serviço sobe mesmo assim e registra o erro. A `/saude` responde 503 com `{"status": "Unhealthy"}`, o `POST /indice-turno` responde 503 em problem+json se faltar o modelo, e os trechos respondem 503 se faltar o banco. A API em C# trata esse 503 como falha do serviço e usa a versão simulada.
- **Histórico que não bloqueia:** cada `POST /indice-turno` grava o cálculo e as horas numa só transação. Se a gravação falhar, a resposta continua 200 e o log registra o id do cálculo e o tipo do erro, nunca o corpo da requisição.
- **SQLAlchemy síncrono e rotas `def`:** o cálculo do XGBoost usa CPU e bloqueia; com rotas `def`, o FastAPI roda cada requisição num pool de threads, sem travar o servidor. O driver síncrono (psycopg 3) deixa o código mais simples do que a versão assíncrona, sem perda para este volume.
- **400 no lugar de 422:** o tratador de validação do FastAPI foi substituído, e o OpenAPI não lista 422. Os erros saem em `application/problem+json` (RFC 9457), com `type` apontando para a seção do RFC 9110, como no ASP.NET.

## Limitações conhecidas

- No protótipo o serviço não tem autenticação, porque só a API na mesma máquina o chama (nginx e rede local). Em produção entrariam mTLS ou JWT entre serviços, limite de requisições e rede restrita.
