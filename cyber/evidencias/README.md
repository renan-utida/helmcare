<p align="center"><img src="../assets/marca/logo-horizontal.png" alt="Logo do HelmCare: um capacete de moto com uma linha de batimento cardíaco na viseira" width="420"></p>

# Evidências do módulo de Cybersecurity

Prints e vídeo citados no [relatório](../docs/relatorio-cyber.md) e na [auditoria do protótipo](../auditoria/auditoria-prototipo.md). Todos os dados pessoais que aparecem são simulados.

```text
evidencias/
  README.md     este arquivo
  prints/       os prints P01 a P18, citados como figuras no relatório
  video/        video-dashboard.mp4, o vídeo do painel com legendas e a logo
```

## Prints

| Arquivo | O que mostra | Seção do relatório |
|---|---|---|
| [`P01-estrutura.png`](prints/P01-estrutura.png) | Estrutura de pastas do módulo `cyber/` | 1 Contexto |
| [`P02a-validadores.png`](prints/P02a-validadores.png) | Validação dos dígitos verificadores de CPF e CNH (`validadores.py`) | 2 Parsing de PII |
| [`P02b-regras.png`](prints/P02b-regras.png) | Catálogo de regras escrito como dados (`regras.py`) | 2 Parsing de PII |
| [`P03-pytest-pii.png`](prints/P03-pytest-pii.png) | Testes do motor de PII: 614 passando | 2 Parsing de PII |
| [`P04-motor-payload.png`](prints/P04-motor-payload.png) | O motor sobre o cadastro simulado: caminho, categoria, método, confiança, DV e valor mascarado | 2 Parsing de PII |
| [`P05-avaliacao-motor.png`](prints/P05-avaliacao-motor.png) | `scripts/avaliar_motor.py`: precisão e recall por categoria sobre o corpus de 77 casos | 2 Parsing de PII |
| [`P06-api-demo-entregadores.png`](prints/P06-api-demo-entregadores.png) | `POST /cyber/v1/demo/entregadores` em PROTEÇÃO: resposta com o resumo do interceptador e log do servidor redigido | 2 Parsing de PII |
| [`P07-c1-antes-depois.png`](prints/P07-c1-antes-depois.png) | Cenário C1 em AUDITORIA e em PROTEÇÃO (`scripts/demo.py --cenario C1`) | 3 Taint Analysis |
| [`P08-rastro.png`](prints/P08-rastro.png) | Rastro de um evento no painel: fonte, propagação e destino com a regra | 3 Taint Analysis |
| [`P09-politica.png`](prints/P09-politica.png) | Política como dado e os ids de regra (`politica.py`) | 3 Taint Analysis |
| [`P10-pytest-taint.png`](prints/P10-pytest-taint.png) | Testes da política e dos canais autorizados: 115 passed, 296 deselected | 3 Taint Analysis |
| [`P11a-prompt.png`](prints/P11a-prompt.png) | Mensagem enviada ao LLM, só com texto derivado de enums (`prompt.py`) | 4 Tradução por IA |
| [`P11b-traducao-ia.png`](prints/P11b-traducao-ia.png) | `scripts/testar_ia.py` com o modelo `gemini-3.5-flash-lite`: origem IA e cache | 4 Tradução por IA |
| [`P12-selo-reserva.png`](prints/P12-selo-reserva.png) | Selo "modelo de reserva" com o motivo SEM_CHAVE, índice 81 (C4 em AUDITORIA) | 4 Tradução por IA |
| [`P13-painel-verde.png`](prints/P13-painel-verde.png) | Painel em PROTEÇÃO depois do C1: índice 15, verde | 5 Painel Risco Cidadão |
| [`P14-painel-amarelo.png`](prints/P14-painel-amarelo.png) | Painel em PROTEÇÃO depois de Executar todos: índice 40, amarelo | 5 Painel Risco Cidadão |
| [`P15-painel-vermelho.png`](prints/P15-painel-vermelho.png) | Painel em AUDITORIA depois de Executar todos: índice 97, vermelho | 5 Painel Risco Cidadão |
| [`P16-qrtoken-tarjado.png`](prints/P16-qrtoken-tarjado.png) | O print 14 do Mobile com tarja preta opaca sobre todo o QR Code, que codifica o `qrToken` | 6 Auditoria do protótipo |
| [`P17a-teste-codigo.png`](prints/P17a-teste-codigo.png) | Código do teste da API que garante o `qrToken` fora do ProblemDetails e do log | 6 Auditoria do protótipo |
| [`P17b-teste-rodando.png`](prints/P17b-teste-rodando.png) | Os 12 testes de `QrTokenForaDasSaidasTests` passando | 6 Auditoria do protótipo |
| [`P18-pytest-cobertura.png`](prints/P18-pytest-cobertura.png) | `python -m pytest -q --cov=helmcare_cyber` com o total e a cobertura | 7 Testes e qualidade |

## Vídeo

O vídeo está publicado no YouTube, como não listado: [https://youtu.be/PSRhGlnmjcI](https://youtu.be/PSRhGlnmjcI). A mesma versão, com legendas e a logo, está no repositório. Ele foi gravado quando o módulo tinha 1421 testes; a versão final tem 1482.

| Arquivo | O que mostra | Seção do relatório |
|---|---|---|
| [`video-dashboard.mp4`](video/video-dashboard.mp4) | Vídeo do painel com legendas que descrevem cada momento e a logo do HelmCare pequena no alto, numa faixa escura fora do painel, sem narração | 5 Painel Risco Cidadão |

O vídeo é versionado no Git (`.gitignore` libera só `evidencias/video/video-dashboard.mp4` e ignora qualquer outro `.mp4` em `evidencias/`) e fica fora do ZIP da entrega por padrão (`python scripts/empacotar_zip.py --incluir-video` o inclui).

## Marca

A logo do HelmCare vem de `docs/marca/` do repositório. O módulo guarda uma cópia idêntica em [`assets/marca/`](../assets/marca/) (`logo-horizontal.png` nos documentos e no PDF, `logo-horizontal-escuro.png` no vídeo) para ser autossuficiente, e um teste confere que a cópia continua igual à original.
