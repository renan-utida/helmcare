<p align="center"><img src="marca/logo-horizontal.png" alt="Logo do HelmCare: um capacete de moto com uma linha de batimento cardíaco na viseira" width="420"></p>

# HelmCare - Machine Learning (Inteligência Artificial & Machine Learning)

**Pergunta de ML:** quantos sinistros graves com moto são esperados em cada hora local da cidade de São Paulo, segundo o calendário? E as condições do tempo medidas pelo INMET mudam essa expectativa?

**Resposta curta:** o calendário (hora, dia da semana, mês e feriado) separa bem as horas: numa quinta-feira de novembro, as 18h têm 4,0 vezes o risco esperado das 3h. O tempo medido pelo INMET (chuva, temperatura e umidade) foi testado e não melhorou o modelo (deviance de Poisson de 0,7427 no teste com o tempo, contra 0,7429 sem ele). Por isso, o modelo exportado usa só o calendário, e a chuva e o calor entram no Índice de Risco do Turno como exposição direta do entregador.

O modelo treinado aqui alimenta o componente sinistro (50%) do Índice de Risco do Turno no [Serviço de Risco](../servico-risco/).

## Resumo

| | |
|---|---|
| Dados | Infosiga SP (alvo e calendário) e INMET A701 (teste das variáveis do tempo), 2019 a 2025 |
| Alvo | Sinistros com pelo menos 1 moto e pelo menos 1 vítima grave ou fatal, por hora local: 13.028 eventos em 61.368 horas |
| Modelo | XGBoost `count:poisson`, profundidade 5, taxa 0,03 e 200 árvores, escolhidos em 2 grades de 27 combinações com 5 dobras anuais |
| Divisão | Treino de 2019 a 2024, teste em 2025 |
| Resultado (deviance de Poisson no teste) | 0,7429 contra 0,7485 da linha de base (média por hora e dia da semana) |
| Arquivos para o Serviço de Risco | `modelo/`: modelo, metadados, P95 único (0,322927) e 100 trechos críticos |
| Limitações | Sem dado de exposição (quantas motos circulam), previsão média da cidade, nível de 2025 acima do histórico e localização por quilômetro dos sinistros da SP 270 (precisão de cerca de 1 km nos trechos críticos) |

## Conteúdo

- Notebook executado: [`GS_HelmCare_IA_and_ML.ipynb`](GS_HelmCare_IA_and_ML.ipynb), do problema à conclusão (Itens 0 a 12), feito no Google Colab
- [`dados/README.md`](dados/README.md): fontes, links e períodos dos dados
- [`requirements.txt`](requirements.txt): bibliotecas e versões do ambiente (Python 3.13, Google Colab)
- [`RELATORIO_ML.md`](RELATORIO_ML.md): relatório do problema à conclusão, com as métricas de treino e teste
- [`RELATORIO_ML.pdf`](RELATORIO_ML.pdf): o mesmo relatório em PDF (3 páginas, A4), gerado pelo [`relatorio/gerar_pdf.py`](relatorio/gerar_pdf.py)
- [`apresentacao/APRESENTACAO_ML.pdf`](apresentacao/APRESENTACAO_ML.pdf): slides do vídeo (gerados por apresentacao/gerar_slides.py)

## Vídeo (até 5 min, não listado)

[youtu.be/A4nlPggXHcY](https://youtu.be/A4nlPggXHcY)
