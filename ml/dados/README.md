# Dados utilizados no modelo

Todos os dados são **reais e públicos**. Os arquivos brutos são pesados (cerca de 1 GB) e **não ficam no repositório**: siga o passo a passo abaixo para baixá-los e organizá-los em `ml/dados/brutos/`.

| Dado | Fonte | Link | Período usado no modelo | Arquivos em `brutos/` |
|---|---|---|---|---|
| Sinistros de trânsito: sinistros, pessoas (vítimas) e veículos | Infosiga SP (Detran-SP), versão 3.1, dados atualizados em 19/10/2026 | https://infosiga.detran.sp.gov.br/ | 2019 a 2025 | 9 arquivos CSV em `infosiga/` |
| Dados meteorológicos horários da estação automática A701 (São Paulo - Mirante de Santana) | INMET (Instituto Nacional de Meteorologia) | https://portal.inmet.gov.br/dadoshistoricos | 2019 a 2025 | 7 arquivos CSV em `inmet/` |

**Data de download:** 07/11/2026

**Por que 2019 a 2025:** o Infosiga passou a registrar também os sinistros **não fatais** a partir de 2019. Antes disso, a base tem apenas os fatais. O ano de 2026 fica de fora porque ainda está incompleto.

**Licença:** os dados do Infosiga SP são publicados pelo Detran-SP sob a licença Creative Commons Atribuição 4.0 (CC BY 4.0), com citação obrigatória da fonte.

---

## Como baixar

### 1. Infosiga SP (sinistros de trânsito)

1. Acesse https://infosiga.detran.sp.gov.br/.
2. No menu da página, abra a seção **Dados Abertos e Documentos** e selecione a aba **Dados abertos**.
3. A aba mostra 4 opções. Baixe as três primeiras:

| Opção | Baixar? | O que vem |
|---|---|---|
| **Download dos dados** | Sim | Arquivo `dados_infosiga.zip`, com 9 arquivos CSV |
| **Dicionário de dados** | Sim | 3 PDFs com a descrição de cada coluna (sinistros, pessoas e veículos) |
| **Diagrama entidade relacionamento** | Sim | 1 PDF com a relação entre as três tabelas |
| **Fluxograma de consolidação** | Não | Não é usado no modelo |

4. Extraia o `dados_infosiga.zip`.
5. Dentro de `ml/dados/brutos/`, crie a pasta **`infosiga`** e coloque nela os **9 arquivos CSV** extraídos.
6. Dentro de `ml/dados/brutos/infosiga/`, crie a pasta **`documentacao`** e coloque nela os **3 PDFs do dicionário** e o **PDF do diagrama**.

### 2. INMET (dados meteorológicos)

1. Acesse https://portal.inmet.gov.br/dadoshistoricos.
2. Na lista **Dados Históricos Anuais**, baixe os 7 arquivos de **ANO 2019 (AUTOMÁTICA)** até **ANO 2025 (AUTOMÁTICA)**. Não baixe o de 2026, que é um ano incompleto.
3. Extraia cada um dos 7 arquivos `.zip`. Cada um traz um CSV por estação automática do Brasil (mais de 500 arquivos por ano).
4. Em cada ano, procure o **único** arquivo da estação de São Paulo, cujo nome segue o padrão:
   `INMET_SE_SP_A701_SAO PAULO - MIRANTE_01-01-AAAA_A_31-12-AAAA.CSV`

   **Atenção:** existe uma estação com código parecido, `S701` (Angélica, MS), que **não** deve ser usada. Confira se o nome tem **`SE_SP_A701`** e **`SAO PAULO - MIRANTE`**.
5. Dentro de `ml/dados/brutos/`, crie a pasta **`inmet`** e coloque nela os **7 arquivos CSV** encontrados. Os demais arquivos podem ser apagados.

---

## Estrutura final das pastas

```
ml/dados/
├── README.md
└── brutos/
    ├── .gitkeep
    ├── infosiga/
    │   ├── sinistros_2015-2021.csv
    │   ├── sinistros_2022-2024.csv
    │   ├── sinistros_2025-2026.csv
    │   ├── pessoas_2015-2021.csv
    │   ├── pessoas_2022-2024.csv
    │   ├── pessoas_2025-2026.csv
    │   ├── veiculos_2015-2021.csv
    │   ├── veiculos_2022-2024.csv
    │   ├── veiculos_2025-2026.csv
    │   └── documentacao/
    │       ├── Infosiga_dicionario_dados_sinistros_v1.8.pdf
    │       ├── Infosiga_dicionario_dados_pessoas_v1.8.pdf
    │       ├── Infosiga_dicionario_dados_veiculos_v1.8.pdf
    │       └── Infosiga_diagrama_ER.pdf
    └── inmet/
        ├── INMET_SE_SP_A701_SAO PAULO - MIRANTE_01-01-2019_A_31-12-2019.CSV
        ├── INMET_SE_SP_A701_SAO PAULO - MIRANTE_01-01-2020_A_31-12-2020.CSV
        ├── INMET_SE_SP_A701_SAO PAULO - MIRANTE_01-01-2021_A_31-12-2021.CSV
        ├── INMET_SE_SP_A701_SAO PAULO - MIRANTE_01-01-2022_A_31-12-2022.CSV
        ├── INMET_SE_SP_A701_SAO PAULO - MIRANTE_01-01-2023_A_31-12-2023.CSV
        ├── INMET_SE_SP_A701_SAO PAULO - MIRANTE_01-01-2024_A_31-12-2024.CSV
        └── INMET_SE_SP_A701_SAO PAULO - MIRANTE_01-01-2025_A_31-12-2025.CSV
```

Os arquivos dentro de `brutos/` são ignorados pelo `.gitignore` e nunca vão para o GitHub. Apenas o `.gitkeep` é versionado, para a pasta existir no repositório.

---

## Verificação dos arquivos

Para conferir se os arquivos certos foram baixados:

**Infosiga:** os CSVs usam `;` como separador. Os três arquivos `sinistros_*.csv` somam cerca de 1,45 milhão de sinistros (de 2015 a 2026, no estado inteiro). O modelo filtra os sinistros com motocicletas na capital (município de São Paulo) entre 2019 e 2025.

**INMET:** cada CSV tem 8 linhas de cabeçalho com os dados da estação (código `A701`), seguidas de uma linha por hora do ano:

| Ano | Horas no arquivo | Observação |
|---|---|---|
| 2019 | 8.760 | Completo |
| 2020 | 8.784 | Completo (ano bissexto) |
| 2021 | 8.760 | Completo |
| 2022 | 8.760 | Completo |
| 2023 | 8.760 | Cerca de 7,4% das horas sem registro de chuva |
| 2024 | 8.784 | Completo (ano bissexto) |
| 2025 | 8.760 | Cerca de 0,5% das horas sem registro |

Os horários do INMET estão em **UTC** e são convertidos para o horário de Brasília (UTC-3) antes do cruzamento com o Infosiga.

---

## Tratamentos aplicados
*(a preencher)*