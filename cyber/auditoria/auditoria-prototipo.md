<p align="center"><img src="../assets/marca/logo-horizontal.png" alt="Logo do HelmCare: um capacete de moto com uma linha de batimento cardíaco na viseira" width="420"></p>

# Auditoria do protótipo HelmCare: dados pessoais nas saídas

## 1. Escopo e método

**Escopo.** As saídas por onde um dado pessoal do entregador poderia vazar: logs, mensagens de erro e ProblemDetails, respostas da API, mensagens MQTT, notificações, a página do Cofre médico, a persistência e a documentação publicada (README, prints e vídeos). A referência é a seção 9 do [Contrato de Integração](../../docs/contrato-integracao.md) (regras de dados pessoais), com as máscaras padrão (CPF `***.***.***-12`, CNH `*******8900`, celular `(11) *****-1234`, telefone fixo `(11) ****-4567`) e o `qrToken` do capacete tratado como credencial.

**Método.**

1. Leitura do código da API principal em C# (configuração de log, tratador de exceções e testes da pasta `Configuracao`) para registrar os controles que já existem.
2. Execução do Taint Analysis do módulo de Cybersecurity nos cenários C1 a C11, nos modos AUDITORIA e PROTEÇÃO (`python scripts/demo.py`), com dados simulados.
3. Varredura da documentação e das imagens publicadas no repositório em busca de credenciais.

Todos os dados usados nesta auditoria são simulados. Nenhum valor real de credencial é transcrito aqui.

## 2. Controles que a API C# já tem

Cada item abaixo foi conferido na leitura do código citado.

| Controle | Onde está | Teste |
|---|---|---|
| Logs de requisição do ASP.NET Core abaixo do nível registrado: a categoria `Microsoft.AspNetCore` fica em `Warning`, então o caminho da requisição (que pode levar o `qrToken`) não vai para o log | `api/src/HelmCare.Api/appsettings.json` e `appsettings.Development.json`, seção `Logging` | `QrTokenForaDasSaidasTests.Configuracao_DeLog_NaoRegistraOCaminho` (um caso por arquivo) |
| Sem `IncludeScopes` no log: o escopo da requisição, que também leva o caminho, não é registrado | os mesmos arquivos | o mesmo teste confere que `IncludeScopes` não aparece |
| Log do `HttpClient` em `Warning`: a URL da previsão do tempo, que leva a coordenada da cidade, não vai para o log | `appsettings.json` | `QrTokenForaDasSaidasTests.Configuracao_LogDoHttpClient_EmWarning` |
| Tratador de exceções que registra só o tipo da exceção, o status e o `traceId`; a mensagem da exceção e o corpo da requisição ficam fora do log | `api/src/HelmCare.Api/Configuracao/TratadorExcecoes.cs` | `QrTokenForaDasSaidasTests.TryHandleAsync_RotaDoCofre_SemQrTokenNaSaida` (404, 403 com motivo e 401) |
| `CaminhoSeguro.SemQrToken`: troca o segmento do `qrToken` por `{qrToken}` nos caminhos `/cofre/...` e `/api/v1/cofre/.../acesso` antes de o caminho sair no campo `instance` do ProblemDetails; `/cofre/assets/...` e `/api/v1/cofre/auditoria/...` ficam como estão | `api/src/HelmCare.Api/Configuracao/CaminhoSeguro.cs` | `QrTokenForaDasSaidasTests.SemQrToken_Caminhos_TrocaSoOToken` (5 caminhos) |
| ProblemDetails só com status (como o 401 do esquema bearer) também sai com `{qrToken}` no `instance` | `api/src/HelmCare.Api/Configuracao/ProblemasPadrao.cs` | `QrTokenForaDasSaidasTests.Completar_SoStatusNoCofre_InstanceSemQrToken` |
| Máscaras do Contrato nas respostas (CPF, celular, fixo e CNH), e valor fora do formato lança exceção sem repetir o valor | `api/src/HelmCare.Api/Contratos/Mascaras.cs` | `MascarasTests` |
| Página do Cofre igual para qualquer `qrToken`, sem dado no HTML, com `Content-Security-Policy`, `Referrer-Policy: no-referrer`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` e `Cache-Control: no-store` na página e nos assets | `api/src/HelmCare.Api/Configuracao/PaginaCofre.cs` | `PaginaCofreTests` |

Os 12 testes da classe `QrTokenForaDasSaidasTests` (5 + 3 + 1 + 2 + 1) passam: `dotnet test api/tests/HelmCare.Testes --filter "FullyQualifiedName~QrTokenForaDasSaidas"` termina com total 12, falhou 0, bem-sucedido 12 (Figuras P17a e P17b do relatório).

## 3. Taint Analysis em ação

O módulo de Cybersecurity reproduz onze erros de código possíveis, cada um num caminho de saída, e passa a saída pelo guarda de Taint Analysis. No modo AUDITORIA, o guarda só registra; no modo PROTEÇÃO, aplica a política antes de o dado sair. Resultado de `python scripts/demo.py`:

| Cenário | Rótulo | AUDITORIA | PROTEÇÃO | Regras aplicadas |
|---|---|---|---|---|
| C1 Cadastro em log por f-string | Simulado | EXPOSTO | NEUTRALIZADO | TA-LOG-01 |
| C2 Senha impressa no log de login | Simulado | EXPOSTO | NEUTRALIZADO | TA-LOG-01 |
| C3 Erro de validação repete CPF e telefone | Simulado | EXPOSTO | NEUTRALIZADO | TA-PUBLICO-01 |
| C4 Ficha médica no chamado ao voluntário | Simulado | EXPOSTO | NEUTRALIZADO | TA-VOLUNT-01 |
| C5 Identificação pessoal no payload MQTT | Simulado | EXPOSTO | NEUTRALIZADO | TA-MQTT-02 |
| C6 Push com localização exata antes do aceite | Simulado | EXPOSTO | NEUTRALIZADO | TA-VOLUNT-01 |
| C7 Localização exata nos canais autorizados | Simulado | PERMITIDO | PERMITIDO | TA-ACEITOU-01, TA-CONTATO-01 |
| C8 Cofre com medicamentos por engano | Simulado | EXPOSTO | NEUTRALIZADO | TA-COFRE-01, TA-COFRE-02, TA-COFRE-03 |
| C9 Cofre para socorrista não autorizado | Simulado | EXPOSTO | NEUTRALIZADO | TA-NEGADO-01 |
| C10 Senha em texto puro na persistência | Simulado | EXPOSTO | NEUTRALIZADO | TA-PERS-01, TA-PERS-02 |
| C11 qrToken em documentação pública | Real (reproduzido com token simulado) | EXPOSTO | NEUTRALIZADO | TA-NEGADO-01 |

Cada modo gera 31 eventos de segurança, e os 11 cenários terminam "conforme o esperado" nos dois modos. O C7 é o contraexemplo: o canal é autorizado e o dado passa nos dois modos, o que mostra que a proteção não atrapalha o socorro. O antes e depois do C1 está na Figura P07, o rastro de um evento na Figura P08 e os testes da política e dos canais autorizados (115 testes selecionados, todos passando) na Figura P10 do relatório.

## 4. Achado real: o `qrToken` do print 14

**Onde está.** O `mobile/README.md` publica o print `mobile/docs/prints/14-qr-capacete.png`, a tela "QR Code do seu capacete". O QR Code da imagem codifica o endereço da página do Cofre com o `qrToken` de um capacete do banco de desenvolvimento. O token não aparece como texto na imagem, mas qualquer leitor de QR Code o recupera. O próprio `mobile/README.md` informa que o mesmo QR aparece também no vídeo do app.

**Risco: baixo.** Pela seção 9 do Contrato, o `qrToken` é credencial e só deveria aparecer no QR Code do capacete, no endereço da página e na resposta do `POST /dispositivos`. Aqui ele saiu num documento público. O risco é baixo porque:

- os dados do capacete e da ficha são simulados, e o banco é de desenvolvimento local;
- o endereço aponta para um IP de rede privada, que não abre nada fora da rede do autor;
- o QR sozinho não abre a ficha: o acesso ao Cofre exige login de um socorrista verificado, vale só dentro de uma janela de emergência com prazo (`TTL_ACESSO_COFRE_MIN`) e cada tentativa fica registrada na auditoria (seções 5 e 7 do Contrato).

**Correção.** A frente do Mobile troca a imagem por uma versão sem o token. O histórico do Git continua com a imagem antiga; limpá-lo (reescrever o histórico) é opcional, dado o risco baixo, e exigiria coordenação com todo o repositório.

**Recomendação preventiva.** Antes de publicar documentação, varrer textos e imagens em busca de credenciais: o motor de PII do módulo reconhece o caminho `/cofre/{token}` em texto (regra PII-QR-02), e QR Codes em imagens precisam de conferência manual ou tarja.

**Evidências.** A Figura P16 do relatório é o print 14 com tarja preta opaca sobre todo o QR Code (a única região em que o token aparece), e a imagem original nunca foi copiada para o módulo de Cybersecurity. As Figuras P17a e P17b mostram o teste da API que garante que o `qrToken` não sai no ProblemDetails nem no log do tratador. O cenário C11 reproduz o achado com um token simulado.

## 5. Real e Simulado

| Item | Tipo | O que mostra |
|---|---|---|
| C11 qrToken em documentação pública | Real, reproduzido com token simulado | Um achado de fato da documentação do projeto (seção 4) |
| C1 a C10 | Simulado | O que o módulo barraria se um desenvolvedor cometesse esse erro de código; são simulações, não ocorrências encontradas |

## 6. Conclusão e limitações

A API C# já protege as saídas mais sensíveis ao `qrToken` (log de requisição, escopos, ProblemDetails e log do tratador), com 12 testes dedicados. O módulo de Cybersecurity acrescenta uma camada que reconhece dados pessoais em qualquer saída e aplica a política da seção 9 com negação por padrão, e os onze cenários mostram o comportamento esperado nos dois modos. O único achado real foi o `qrToken` dentro do QR Code de um print publicado, de risco baixo, com correção encaminhada à frente do Mobile.

Limitações:

- a auditoria leu os controles da API, mas não foi um teste de intrusão;
- os cenários C1 a C10 são simulações de erros possíveis, não ocorrências encontradas;
- o guarda de Taint Analysis é Python e protege as saídas que passam por ele; a API C# tem os próprios controles e não usa o guarda, por decisão de escopo;
- credenciais dentro de imagens (como um QR Code) não são reconhecidas pelo motor de PII, que trabalha com texto.
