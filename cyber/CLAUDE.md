# CLAUDE.md do módulo cyber

Módulo de Cybersecurity do HelmCare (GS CitizenTech). Tudo fica em `cyber/`.

- Fontes da verdade: `docs/contrato-integracao.md` (a seção 9 é a regra de dados pessoais) e a Ficha.
- Não alterar `api/`, `mobile/`, `servico-risco/`, `iot/`, `ml/`, `infra/` nem o contrato.
- Nunca fazer git add, commit, push, checkout ou merge. Nunca abrir ou imprimir `.env` e segredos. A chave do LLM só vem de variável de ambiente.
- Dados pessoais sempre simulados. O valor original de um dado pessoal nunca entra em log, evento, repr, JSON de resposta, documentação ou chamada ao LLM.
- Código sem acentos nos identificadores, comentários em português do Brasil, sem travessões em nenhum texto.
- README e docs descrevem o projeto como documentação, sem citar etapas, rodadas ou andamento do trabalho.
- Todo código novo vem com testes pytest. Rodar `python -m pytest -q` dentro de `cyber/` antes de encerrar.
- Python 3.13, venv em `cyber/.venv`.
