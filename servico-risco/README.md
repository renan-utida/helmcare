# HelmCare - Serviço de Risco (Arquitetura Orientada a Serviços (SOA) e Web Services)

Serviço independente que calcula o Índice de Risco do Turno (modelo de ML + chuva + calor) e fornece os trechos críticos. A API principal em C# consome este serviço via REST; o app nunca o chama direto.

> Status: em construção (Rodada 1: esqueleto, domínio e camada de ML). Este README é provisório; a versão final, com endpoints, testes e evidências, vem na Rodada 3.

Integrante: Renan Dias Utida (RM 558540, turma 3ESPW).

## Tecnologias

Python 3.13 · FastAPI · SQLAlchemy 2 · Alembic · PostgreSQL 17.6 · XGBoost · pytest · ruff

## Como executar (Windows, Git Bash)

Pré-requisito: Python 3.13 (`py --version`).

```shell
# uma vez, a partir da raiz do repositório (helmcare/)
py -3.13 -m venv servico-risco/.venv

# a partir de servico-risco/
cd servico-risco
source .venv/Scripts/activate        # opcional; sem ativar, use .venv/Scripts/python
python -m pip install -r requirements-dev.txt

# segredos: copie o modelo e preencha os valores locais (nunca versione o .env)
cp .env.example .env

# testes e linter
python -m pytest
python -m pytest --cov=app
python -m ruff check .
python -m ruff format --check .
```

Nas Rodadas seguintes entram `docker compose up -d` (PostgreSQL na porta 5433), `python -m alembic upgrade head` e `python -m uvicorn app.main:app --host 127.0.0.1 --port 8000`.

No Windows, se o console mostrar acentos errados, rode os comandos com `PYTHONUTF8=1` na frente.

## Configuração

Variáveis lidas por `app/config.py` (prefixo `RISCO_`, do ambiente ou do `servico-risco/.env`):

| Variável | Padrão | Uso |
|---|---|---|
| `RISCO_DATABASE_URL` | vazio | URL do banco `helmcare_risco` (Rodada 2) |
| `RISCO_TESTES_DATABASE_URL` | vazio | URL do banco de testes de integração (Rodada 2) |
| `RISCO_MODELO_DIR` | `ml/modelo` do repositório | Pasta dos artefatos do modelo |
| `RISCO_CARREGAR_TRECHOS_NA_INICIALIZACAO` | `true` | Carga dos trechos críticos no startup (Rodada 2) |

## Estrutura

```
servico-risco/
  app/
    config.py                 configuração (pydantic-settings)
    dominio/                  regras do índice, sem framework, banco ou ML
      faixa_risco, indice_calor, componentes, calculadora_indice,
      horas_turno, agregacao, calendario, explicacao,
      trecho_critico, excecoes, portas
    infraestrutura/
      ml/                     artefatos (leitura de ml/modelo/) e previsor_xgboost
  tests/
    dominio/                  testes unitários do domínio
    infraestrutura/ml/        testes do previsor com o modelo real
  compose.yaml                PostgreSQL próprio (porta 5433), separado do da API
  requirements.txt            dependências de execução (versões exatas)
  requirements-dev.txt        dependências de testes e linter
  pyproject.toml              configuração do ruff, do pytest e da cobertura
```

As dependências entre camadas apontam sempre para dentro: `infraestrutura` (e, depois, `api`) depende de `aplicacao`, que depende de `dominio`. O domínio declara as portas (`typing.Protocol`) e a infraestrutura as implementa.

## Endpoints e fórmula do índice

Seção 8 do [contrato de integração](../docs/contrato-integracao.md).
