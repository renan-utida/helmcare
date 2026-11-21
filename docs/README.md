<p align="center"><img src="marca/logo-horizontal.png" alt="Logo do HelmCare: um capacete de moto com uma linha de batimento cardíaco na viseira" width="420"></p>

# HelmCare · Testing, Compliance & Quality Assurance

> **Do risco ao resgate, cuidado em cada corrida.**

**Global Solution 2026.2 (CitizenTech), FIAP**
**Disciplina:** Testing, Compliance & Quality Assurance, Prof. Salatiel Marinho
**Integrante:** Renan Dias Utida, RM 558540, turma 3ESPW

---

## Objetivo

Nesta disciplina, o HelmCare é planejado e verificado no Azure DevOps. O backlog descreve o que o sistema precisa fazer em Features e Product Backlog Items (PBIs), com critérios de aceite, e um plano de aceitação reúne os Test Cases funcionais que mostram, por fora do sistema, se cada critério é cumprido. A rastreabilidade liga cada Test Case aos PBIs que ele cobre.

## Links

| Item | Link |
|---|---|
| Azure DevOps (o professor tem o perfil Project Administrators) | https://dev.azure.com/RM558540-HelmCare-3ESPW/HelmCare |
| Pitch no YouTube | https://youtu.be/aCjWAEcS8iI |
| Relatório em PDF | [`HelmCare_Testing_GS_RM558540.pdf`](HelmCare_Testing_GS_RM558540.pdf) |
| Relatório em Markdown | [`relatorio-testing.md`](relatorio-testing.md) |
| Test Cases em texto | [`test-cases/test-cases-leitura.md`](test-cases/test-cases-leitura.md) |
| Contrato de Integração | [`contrato-integracao.md`](contrato-integracao.md) |

## O que a GS pede e onde está

| Pedido | Peso | Onde está |
|---|---|---|
| Features com descrição e critérios de aceite | 50% | 13 Features no Azure Boards, cada uma com descrição e critérios no campo Acceptance Criteria. No relatório: seção 3 (Epics e Features) e seção 4 (Detalhe das Features, com o print de cada uma) |
| Test Cases funcionais, positivos e negativos, caixa-preta, com objetivo, preparação, entradas, saídas esperadas e passos | 50% | 30 Test Cases no Azure Test Plans (15 positivos e 15 negativos). No relatório: seção 5 (Estratégia de testes) e seção 6 (Test Cases, com a tabela-resumo e o print e o texto de cada um). O texto completo está em [`test-cases/test-cases-leitura.md`](test-cases/test-cases-leitura.md) |

## Estrutura no Azure DevOps

| Item | Quantidade e organização |
|---|---|
| Processo | Scrum, no projeto HelmCare da organização RM558540-HelmCare-3ESPW |
| Epics | 4: Cadastro e turno, Prevenir, Socorrer e Proteger |
| Features | 13, de F01 Cadastro do entregador a F13 Conta e acesso |
| Product Backlog Items | 33, todos na Sprint 1, com o título no padrão `[Feature] detalhe` e a descrição "Como [usuário], eu quero [meta] para que [benefício]" |
| Test Plan | "HelmCare · Plano de Aceitação" (ID 52) |
| Suite estática | "Aceitação dos 13 Features (TC01 a TC30)" (ID 54), com os 30 Test Cases (IDs 230 a 259) na ordem de execução |
| Rastreabilidade | 61 vínculos Tested By de Test Case para PBI, e a pasta "Por PBI (rastreabilidade)" com 33 suites requirement-based, uma por PBI |
| Estado dos Test Cases | Definidos e no estado Not run: a entrega pede a definição dos casos de teste, não a execução |

A rastreabilidade PBI para Test Case está no relatório, na seção 7 (Cobertura e rastreabilidade): a tabela de cobertura por Feature, com os positivos e os negativos, a tabela PBI para Test Case, com o ID de cada PBI no Azure, e os prints do vínculo Tested By, das suites por PBI e da aba Chart.

## Como cada Test Case está escrito

Cada Test Case segue o mesmo formato, no Azure e em [`test-cases/test-cases-leitura.md`](test-cases/test-cases-leitura.md):

| Parte | Conteúdo |
|---|---|
| Título | `TCnn [referência] Polaridade: título`, por exemplo `TC01 [PBI 1.1 CA1, CA2, CA6; ...] Positivo: Cadastro completo do entregador com máscaras` |
| Referência | Os PBIs e os critérios de aceite cobertos (por exemplo, PBI 1.1 CA1) |
| Polaridade | Positivo (o caminho esperado funciona) ou Negativo (entradas inválidas, fronteiras e acessos indevidos são recusados) |
| Objetivo | O que o teste valida, em uma frase |
| Preparação | O estado necessário antes do primeiro passo e os Test Cases dos quais ele depende |
| Passos | Tabela com a ação e as entradas de cada passo e o resultado esperado |

Os testes são caixa-preta: cada passo usa só a interface pública (rotas da API, app, capacete no Wokwi e broker MQTT) e compara a saída com o esperado. As técnicas usadas são partição de equivalência e valor limite (2,5 g contra 2,4 g, 32,0 °C contra 31,9 °C, 2 km contra 2,1 km, limite de 1 a 50, escalada aos 60 s), transição de estados do capacete e da ocorrência e matriz de acesso por papel e por dono (401, 403 e 404). A massa de dados e as convenções estão na seção 5 do relatório.

## Dados e conformidade

- **Dados pessoais simulados.** Todos os dados pessoais do protótipo são simulados: nomes inventados, CPF, CNH e telefones fictícios e e-mails no domínio reservado `exemplo.com`. Nenhum print do relatório mostra dado pessoal real.
- **Cofre médico.** O QR Code do capacete só libera a ficha médica mínima (tipo sanguíneo, alergias, condições críticas e contato de emergência) para socorristas verificados, durante a emergência, com expiração e registro de cada acesso. Os Test Cases TC23 a TC25 cobrem o acesso concedido e as negações, e TC26 e TC27 cobrem a auditoria.
- **LGPD.** Pela Lei Geral de Proteção de Dados (Lei 13.709/2018), os dados de saúde são dados pessoais sensíveis (art. 5, inciso II). O desenho segue os princípios da necessidade (art. 6, inciso III) e da segurança (art. 6, inciso VII, e art. 46): só o mínimo necessário sai do Cofre, os documentos aparecem mascarados nas respostas (TC01) e a auditoria não expõe o ip nem dados de saúde (TC26). As regras de onde cada dado pode aparecer estão na seção 9 do [Contrato de Integração](contrato-integracao.md), e a análise completa está no [relatório de Cybersecurity](../cyber/docs/relatorio-cyber.md).

## Mapa da pasta

```
docs/
├── README.md                          esta página
├── HelmCare_Testing_GS_RM558540.pdf   relatório de entrega (47 páginas)
├── relatorio-testing.md               fonte do relatório
├── contrato-integracao.md             Contrato de Integração entre as partes do HelmCare
├── test-cases/
│   └── test-cases-leitura.md          os 30 Test Cases em texto, com a massa de dados
├── evidencias/                        57 prints do Azure DevOps usados no relatório (P00 a P51)
├── marca/                             logo e símbolo do HelmCare
├── ODS/                               ícones oficiais das ODS 3, 9, 10, 11 e 13 usados no pitch
└── pitch/
    └── HelmCare_Pitch.mp4             vídeo do pitch
```

## Como o PDF é gerado

O PDF sai de [`relatorio-testing.md`](relatorio-testing.md), com as imagens de [`evidencias/`](evidencias/) e a logo de [`marca/`](marca/), pelo mesmo processo do relatório de Cybersecurity ([`cyber/scripts/gerar_pdf.py`](../cyber/scripts/gerar_pdf.py)): o Markdown vira HTML com Pygments, as imagens entram embutidas e o navegador (Edge ou Chrome) em modo headless imprime o PDF em A4. Sem navegador, o HTML pode ser impresso com Ctrl+P.

## Pitch

O pitch de 3:45 está no YouTube em https://youtu.be/aCjWAEcS8iI e versionado em [`pitch/HelmCare_Pitch.mp4`](pitch/HelmCare_Pitch.mp4). Ele mostra o problema, a demonstração ponta a ponta do capacete no Wokwi e do app, os três pilares (prevenir, socorrer e proteger), o indicador de impacto e as ODS. Os dados pessoais mostrados no vídeo são simulados.

## Uso de IA

A IA foi usada como copiloto para aprender, planejar e revisar. Os prompts estão registrados em [`prompts.md`](../prompts.md), na raiz do repositório.
