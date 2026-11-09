# HelmCare - Contrato de Integração

**Versão 1.5 - 09/11/2026**

Este documento define como as partes do HelmCare conversam entre si: nomes, tópicos MQTT, endpoints, payloads, estados e regras. **Todas as disciplinas seguem este contrato.** Mudanças só entram por meio da conversa central de coordenação, que gera uma nova versão.

---

## 1. Componentes e portas

| Componente | Tecnologia | Endereço local | Pasta |
|---|---|---|---|
| Capacete (protótipo) | ESP32 no Wokwi | Broker MQTT público | `iot/` |
| Broker MQTT | HiveMQ público | `broker.hivemq.com:1883` | |
| API principal | ASP.NET Core (C#) | `http://localhost:5000/api/v1` | `api/` |
| Cofre médico (página web) | Servida pela API | Página HTML em `http://localhost:5000/cofre/{qrToken}` (fora do `/api/v1`); os dados em JSON ficam em `/api/v1/cofre/...` (seção 7) | `api/` |
| Serviço de Risco | FastAPI (Python) | `http://localhost:8000/risco/v1` | `servico-risco/` |
| Banco de dados | PostgreSQL | Na VM: um servidor em `localhost:5432` com os bancos `helmcare_api` e `helmcare_risco`. No desenvolvimento local, cada serviço sobe o seu PostgreSQL no Docker: a API em `127.0.0.1:5432` e o Serviço de Risco em `127.0.0.1:5433` | |
| App do entregador | Expo (React Native + TypeScript) | Consome apenas a API principal | `mobile/` |
| Modelo de ML | XGBoost (artefatos em `ml/modelo/`, ver seção 8.1) | Carregado pelo Serviço de Risco | `ml/` |
| Infra | Debian + Shell Script + systemd + nginx | nginx na porta 80: `/api` e `/cofre` para a API, `/risco` para o Serviço de Risco | `infra/` |

**Regra de arquitetura:** o app **nunca** chama o Serviço de Risco diretamente. A API principal funciona como porta de entrada (gateway) e repassa as chamadas.

**Configuração e saúde da API:** a connection string vem da variável de ambiente `ConnectionStrings__HelmCareApi` (em desenvolvimento, do user-secrets), nunca de arquivo versionado. O `GET /api/v1/saude` responde `200` com o texto `Healthy` ou `503` com `Unhealthy` (inclui o teste de conexão com o banco), então o monitoramento deve olhar o status HTTP.

---

## 2. Convenções gerais

| Item | Padrão |
|---|---|
| Idioma do domínio | Português, sem acentos no código (`Ocorrencia`, `FichaMedica`) |
| Classes C# | PascalCase (`EventoImpacto`) |
| JSON | camelCase (`tempoAteAceiteSegundos`) |
| Valores de enum no JSON e no MQTT | UPPER_SNAKE_CASE (`SOS_CAMADA_1`) |
| Identificadores | GUID (`3f2a9c1e-...`) |
| Datas e horas | ISO 8601 em UTC (`2026-11-12T21:15:30Z`) |
| Versionamento das APIs | Prefixo `/v1` |
| Erros HTTP | ProblemDetails (RFC 7807) |
| Códigos de resposta | Ver o quadro "Códigos padrão" da seção 7 |
| Coordenadas | Graus decimais (`latitude`, `longitude`) |
| Dados pessoais | **Somente simulados**, nunca de pessoas reais |

---

## 3. Entidades do domínio

| Entidade | Campos principais | Observações |
|---|---|---|
| `Entregador` | `id`, `nome`, `cpf`, `cnh`, `telefone`, `criadoEm` | `cpf`, `cnh` e `telefone` são PII |
| `ContatoEmergencia` | `id`, `entregadorId`, `nome`, `telefone`, `parentesco` | PII |
| `FichaMedica` | `id`, `entregadorId`, `tipoSanguineo`, `alergias`, `condicoesCriticas`, `medicamentos` | **PII sensível (saúde)** |
| `Dispositivo` | `id`, `codigo` (ex.: `HC-0001`), `entregadorId`, `qrToken`, `estadoAtual`, `estadoAtualizadoEm`, `ultimoIndiceCalorC`, `ultimoIndiceCalorEm` | O `qrToken` é o que vai no QR Code do capacete. Os quatro últimos campos guardam o último estado e o último índice de calor recebidos do capacete |
| `Turno` | `id`, `entregadorId`, `inicio`, `fimPrevisto`, `fim`, `indiceRisco`, `faixaRisco`, `componenteSinistro`, `componenteChuva`, `componenteCalor`, `explicacao`, `protecaoAtiva` | Guarda o Índice calculado no início, para o `GET /turnos/{id}/risco` não precisar de nova chamada |
| `EventoImpacto` | `id`, `dispositivoId`, `ocorridoEm`, `ocorridoEmEstimado`, `picoG`, `orientacaoAnormal`, `pilotandoAntes`, `presencaAntes`, `contagemTerminaEm`, `resultado` | Registro de cada impacto, inclusive falsos positivos e ignorados. `resultado` fica vazio enquanto o desfecho não chega. `ocorridoEmEstimado` é verdadeiro quando o impacto foi criado a partir de um SOS (seção 6.2) |
| `Ocorrencia` | `id`, `entregadorId`, `eventoImpactoId`, `estado`, `camada`, `latitude`, `longitude`, `criadaEm`, `aceitaEm`, `encerradaEm`, `tempoAteAceiteSegundos` | É o SOS. `tempoAteAceiteSegundos` é o indicador de impacto. |
| `Voluntario` | `id`, `entregadorId`, `tipo`, `disponivel`, `latitude`, `longitude` | Todo voluntário também é um entregador cadastrado |
| `AceiteSocorro` | `id`, `ocorrenciaId`, `voluntarioId`, `aceitoEm` | |
| `Socorrista` | `id`, `nome`, `registroProfissional`, `organizacao`, `verificado`, `usuarioId` | Perfil com acesso ao Cofre. `usuarioId` liga o socorrista ao usuário do Identity, usado no login |
| `AcessoCofre` | `id`, `ocorrenciaId`, `entregadorId`, `socorristaId`, `origem`, `concedido`, `motivo`, `acessadoEm`, `expiraEm`, `ip` | Auditoria de **toda tentativa** de um socorrista autenticado, concedida ou negada. `concedido` diz se a ficha foi liberada e `motivo` explica a negação (por exemplo, socorrista não verificado ou acesso expirado). `ocorrenciaId` fica vazio na emergência manual, por isso o `entregadorId` |
| `EventoMqttProcessado` *(interna)* | `dispositivo`, `tipo`, `ts` | Garante a idempotência dos eventos MQTT (seção 6.2): a chave única é o trio |

**Validações de cadastro (a API rejeita com 400):**

| Campo | Regra |
|---|---|
| `nome` | Obrigatório, até 120 caracteres |
| `cpf` e `cnh` | 11 dígitos. A API aceita com ou sem máscara e guarda só os dígitos |
| `telefone` | 10 ou 11 dígitos, com DDD. Aceito com ou sem máscara, guardado só com os dígitos |
| `tipoSanguineo` | Somente `A+`, `A-`, `B+`, `B-`, `AB+`, `AB-`, `O+` ou `O-` (maiúsculas) |

---

## 4. Enums

| Enum | Valores |
|---|---|
| `EstadoDispositivo` (no ESP32) | `DESARMADO`, `ARMADO`, `IMPACTO_DETECTADO`, `OBSERVACAO`, `CONTAGEM`, `SOS_ENVIADO`, `CANCELADO`, `FALSO_POSITIVO` |
| `EstadoOcorrencia` (na API) | `SOS_CAMADA_1`, `SOS_CAMADA_2`, `SOCORRO_A_CAMINHO`, `ENCERRADA`, `CANCELADA` |
| `ResultadoImpacto` | `IGNORADO`, `FALSO_POSITIVO`, `CANCELADO_PELO_ENTREGADOR`, `SOS_ENVIADO` |
| `FaixaRisco` | `BAIXO`, `MODERADO`, `ALTO`, `MUITO_ALTO` |
| `TipoVoluntario` | `ENTREGADOR`, `VIGIA_MOTO` |
| `OrigemAcessoCofre` | `SOS_ATIVO`, `EMERGENCIA_MANUAL` |
| `TipoEventoMqtt` | `IMPACTO_DETECTADO`, `CONTAGEM_INICIADA`, `CANCELADO`, `FALSO_POSITIVO`, `SOS` |
| `TipoComandoMqtt` | `ARMAR`, `DESARMAR`, `CANCELAR`, `SOCORRO_A_CAMINHO`, `ENCERRAR` |

---

## 5. Constantes compartilhadas

Todas configuráveis (arquivo de configuração ou variável de ambiente), com estes valores iniciais:

| Constante | Valor | Onde é usada |
|---|---|---|
| `JANELA_OBSERVACAO_S` | 10 | ESP32 |
| `CONTAGEM_CANCELAMENTO_S` | 15 | ESP32, API e app |
| `LIMIAR_IMPACTO_G` | 2,5 (calibrado no Wokwi, onde cada eixo do MPU6050 vai só até ±2 g e a aceleração total máxima é 3,46 g) | ESP32 |
| `VELOCIDADE_PILOTAGEM_KMH` | 15. `pilotandoAntes` é verdadeiro quando a velocidade foi **maior ou igual** a 15 km/h antes do impacto | ESP32 (velocidade simulada) |
| `RAIO_CAMADA_1_KM` | 2 | API |
| `TEMPO_ACEITE_CAMADA_1_S` | 60 | API |
| Camada 2 | Toda a rede de voluntários disponíveis | API |
| `TTL_ACESSO_COFRE_MIN` | 60 | API |
| `META_TEMPO_ACEITE_S` | 120 | API, app e pitch |
| `INTERVALO_TELEMETRIA_S` | 10 | ESP32 (telemetria e reenvio do SOS) |
| `ALERTA_CALOR_C` | 32 (índice de calor, em °C). O aviso vale quando o índice é **maior ou igual** a 32 °C | ESP32 (aviso no OLED) e app (sugestão de pausa) |
| `INTERVALO_POLLING_APP_S` | 5 | App (consulta de chamados, status e alerta) |
| Coordenadas simuladas do capacete | `-23.5641`, `-46.6525` (região da Av. Paulista) | ESP32 |

---

## 6. MQTT (capacete e API)

**Raiz dos tópicos:** `helmcare-rdu/v1/{codigoDispositivo}`
O prefixo `helmcare-rdu` evita colisão com outros usuários do broker público. **Nenhum dado pessoal trafega no MQTT**, apenas o código do dispositivo.

**Clientes no broker:** cada cliente usa um `clientId` próprio, porque dois clientes com o mesmo `clientId` se derrubam. O capacete usa `helmcare-rdu-HC-0001` com sessão persistente (`cleanSession = false`), para os comandos em QoS 1 ficarem guardados no broker se ele cair da rede por alguns segundos. A API e o dashboard Node-RED usam `clientId` diferentes.

**QoS:** os comandos (API para o capacete) são publicados e assinados em QoS 1. O capacete publica telemetria e eventos em QoS 0, limite da biblioteca PubSubClient usada nas aulas. A API assina esses tópicos em QoS 1, mas a entrega efetiva fica em QoS 0. Para o evento mais importante, o SOS, vale a regra de reenvio da seção 6.2.

### 6.1 Telemetria (ESP32 para a API)

Tópico `helmcare-rdu/v1/HC-0001/telemetria`, a cada 10 s em todo estado diferente de `DESARMADO` (inclusive durante a observação, a contagem e o SOS, porque é isso que alimenta o `GET /turnos/{id}/alerta`):

```json
{
  "dispositivo": "HC-0001",
  "ts": "2026-11-12T21:15:30Z",
  "estado": "ARMADO",
  "temperaturaC": 33.4,
  "umidadePct": 58,
  "indiceCalorC": 37.1,
  "presenca": true,
  "velocidadeKmh": 32,
  "bateriaPct": 81
}
```

`bateriaPct` é opcional (item "se der tempo").

**Fórmula do índice de calor (`indiceCalorC`):** é o índice de calor da NOAA (regressão de Rothfusz, com a fórmula simplificada de Steadman quando o resultado fica abaixo de 80 °F, cerca de 26,7 °C, e os ajustes de umidade da NOAA), calculado em Fahrenheit e convertido para Celsius. É exatamente a função `computeHeatIndex(temperatura, umidade, false)` da biblioteca DHT sensor library (Adafruit), usada no capacete. O Serviço de Risco implementa a mesma fórmula para o componente `calor`, e os testes das duas partes usam estes valores de referência, medidos no Wokwi:

| Temperatura | Umidade | Índice de calor |
|---|---|---|
| 33,4 °C | 58% | 39,8 °C |
| 23,5 °C | 58% | 23,4 °C |

### 6.2 Eventos (ESP32 para a API)

Tópico `helmcare-rdu/v1/HC-0001/evento`, a cada mudança de estado:

```json
{
  "dispositivo": "HC-0001",
  "ts": "2026-11-12T21:16:02Z",
  "tipo": "SOS",
  "picoG": 4.8,
  "orientacaoAnormal": true,
  "pilotandoAntes": true,
  "presencaAntes": true,
  "latitude": -23.5641,
  "longitude": -46.6525
}
```

**Regras dos eventos:**

- **Detecção:** um impacto acontece quando a aceleração total é maior ou igual a `LIMIAR_IMPACTO_G`. O campo `orientacaoAnormal` (inclinação maior que 60°) é só informativo e não bloqueia a detecção, porque uma queda real pode terminar com o capacete na posição normal.
- **Dados do impacto:** todos os eventos de um mesmo impacto repetem `picoG`, `orientacaoAnormal`, `pilotandoAntes`, `presencaAntes`, `latitude` e `longitude`. O `ts` é o instante de cada evento.
- **Reenvio do SOS:** enquanto estiver em `SOS_ENVIADO`, o capacete republica o evento `SOS` com o **mesmo** `ts` a cada `INTERVALO_TELEMETRIA_S`, até receber `SOCORRO_A_CAMINHO` ou `ENCERRAR`. A API trata eventos com o mesmo `dispositivo`, `tipo` e `ts` como um só (idempotência).
- **SOS sem impacto em aberto:** como os eventos saem em QoS 0, o `IMPACTO_DETECTADO` pode se perder. Se chegar um `SOS` sem impacto em aberto para o dispositivo, a API cria o `EventoImpacto` com os dados do próprio SOS, `ocorridoEm` = `ts` menos 25 s (`JANELA_OBSERVACAO_S` + `CONTAGEM_CANCELAMENTO_S`) e `ocorridoEmEstimado` = verdadeiro. Assim, nem o SOS nem o indicador de impacto se perdem.

**Como a API registra cada evento:**

| Evento | Quando o capacete envia | O que a API faz |
|---|---|---|
| `IMPACTO_DETECTADO` com `pilotandoAntes` e `presencaAntes` falsos | Impacto descartado. O capacete volta para `ARMADO` | Grava o `EventoImpacto` com `resultado` = `IGNORADO` |
| `IMPACTO_DETECTADO` com pelo menos um dos dois verdadeiro | Começa a observação de 10 s | Grava o `EventoImpacto` com `resultado` vazio |
| `FALSO_POSITIVO` | A velocidade de moto voltou durante a observação | `resultado` = `FALSO_POSITIVO` |
| `CONTAGEM_INICIADA` | Começa a contagem de 15 s | Guarda `contagemTerminaEm` = `ts` + `CONTAGEM_CANCELAMENTO_S`, exibido em `GET /turnos/{id}/alerta` |
| `CANCELADO` | Botão do capacete ou comando `CANCELAR` durante a contagem | `resultado` = `CANCELADO_PELO_ENTREGADOR` |
| `SOS` | Fim da contagem sem cancelamento | `resultado` = `SOS_ENVIADO` e cria a `Ocorrencia` em `SOS_CAMADA_1` |

### 6.3 Comandos (API para o ESP32)

Tópico `helmcare-rdu/v1/HC-0001/comando`:

```json
{ "tipo": "SOCORRO_A_CAMINHO", "ts": "2026-11-12T21:16:40Z" }
```

| Comando | Efeito no capacete |
|---|---|
| `ARMAR` | De `DESARMADO` para `ARMADO` |
| `DESARMAR` | Só vale em `ARMADO`: vai para `DESARMADO`. Em `OBSERVACAO`, `CONTAGEM` e `SOS_ENVIADO`, é ignorado (não se desliga a proteção no meio de uma possível queda; durante a contagem, quem quer parar usa o `CANCELAR`) |
| `CANCELAR` | Só vale em `CONTAGEM`: vai para `CANCELADO`, publica o evento `CANCELADO` e volta para `ARMADO`. Fora da contagem, é ignorado |
| `SOCORRO_A_CAMINHO` | Em `SOS_ENVIADO`: mostra "Socorro a caminho" no OLED e para o reenvio do SOS |
| `ENCERRAR` | Em `SOS_ENVIADO`: para o reenvio do SOS e volta para `ARMADO`, porque o turno continua |

`FALSO_POSITIVO` e `CANCELADO` também voltam para `ARMADO`.

---

## 7. API principal (C#): `/api/v1`

| Método | Rota | O que faz |
|---|---|---|
| POST | `/entregadores` | Cadastra um entregador |
| GET | `/entregadores/{id}` | Dados do entregador (PII mascarada) |
| PUT | `/entregadores/{id}/ficha-medica` | Cria ou atualiza a ficha médica |
| POST | `/entregadores/{id}/contatos` | Adiciona um contato de emergência |
| POST | `/dispositivos` | Vincula um capacete a um entregador |
| POST | `/turnos` | Inicia um turno e devolve o Índice de Risco. Com `protecaoAtiva` verdadeiro, a API publica `ARMAR` para o capacete do entregador |
| POST | `/turnos/{id}/encerrar` | Encerra o turno. Com o capacete em `ARMADO`, a API envia `DESARMAR`. Com alerta em andamento (`OBSERVACAO`, `CONTAGEM` ou `SOS_ENVIADO`) ou ocorrência ativa, responde `409` |
| GET | `/turnos/{id}/risco` | Índice de Risco atual do turno |
| GET | `/turnos/{id}/alerta` | Estado do alerta de queda do capacete no turno (tela de alerta do app) |
| POST | `/turnos/{id}/alerta/cancelar` | Cancela o alerta durante a contagem (a API publica o comando `CANCELAR`) |
| GET | `/trechos-criticos?limite=10` | Repassa a lista do Serviço de Risco, com o mesmo parâmetro `limite` |
| PUT | `/voluntarios/{id}/disponibilidade` | Atualiza a disponibilidade e a posição do voluntário |
| GET | `/ocorrencias/proximas?latitude=&longitude=` | Chamados visíveis para um voluntário (localização aproximada) |
| GET | `/ocorrencias/{id}` | Status de uma ocorrência e o cronômetro |
| POST | `/ocorrencias/{id}/aceites` | Voluntário aceita o chamado (libera a localização exata). Se ele não puder atender (indisponível, é a própria vítima ou está fora do raio na camada 1), `409` e a ocorrência não muda |
| POST | `/ocorrencias/{id}/cancelar` | Entregador informa que está bem |
| POST | `/ocorrencias/{id}/encerrar` | Encerra a ocorrência, com ou sem aceite (por exemplo, o SAMU chegou antes de qualquer voluntário) |
| POST | `/socorristas/login` | Autentica um socorrista. Credenciais inválidas: `401` em ProblemDetails, sem token |
| POST | `/cofre/{qrToken}/acesso` | Socorrista autenticado acessa a ficha (ou abre emergência manual) |
| GET | `/cofre/auditoria/{entregadorId}` | Histórico de acessos ao Cofre |
| GET | `/indicadores` | *(Opcional)* Tempo médio até o aceite, percentual dentro da meta de `META_TEMPO_ACEITE_S` e taxa de falsos positivos |
| GET | `/saude` | Verificação de saúde (usada pelo monitoramento do OS) |

A página web do Cofre fica **fora** do `/api/v1`, em `GET /cofre/{qrToken}` (HTML, sem dados se não houver emergência). As rotas JSON do Cofre ficam dentro: `/api/v1/cofre/{qrToken}/acesso` e `/api/v1/cofre/auditoria/{entregadorId}`.

**Códigos padrão:**

| Situação | Código |
|---|---|
| Criação de recurso (`POST /entregadores`, `/entregadores/{id}/contatos`, `/dispositivos`, `/turnos` e `/ocorrencias/{id}/aceites`) | `201 Created`, com o cabeçalho `Location` |
| Leitura, `PUT` e ações sobre um recurso existente (encerrar, cancelar) | `200 OK`, com o recurso atualizado |
| Comando enviado ao capacete sem esperar resposta (`POST /turnos/{id}/alerta/cancelar`) | `202 Accepted` |
| Dados inválidos (validações da seção 3, parâmetros fora da faixa) | `400 Bad Request` |
| Sem login onde ele é exigido (Cofre) | `401 Unauthorized` |
| Autenticado, mas sem permissão (socorrista não verificado, acesso ao Cofre expirado) | `403 Forbidden` |
| Id ou `qrToken` inexistente (entregador, turno, ocorrência, voluntário, dispositivo, Cofre) | `404 Not Found` |
| Regra de negócio ou transição de estado inválida (segundo turno ativo, ocorrência já encerrada, alerta fora da contagem, aceite simultâneo) | `409 Conflict` |

Todos os erros saem em ProblemDetails.

**Regras do turno:**

- **Proteção:** a requisição do `POST /turnos` aceita `protecaoAtiva` (opcional, padrão verdadeiro). Com ela verdadeira, o entregador precisa ter um capacete vinculado (senão, `409`), e a API publica `ARMAR` no tópico de comando. A proteção é desligada ao encerrar o turno, que publica `DESARMAR` (seção 7, `POST /turnos/{id}/encerrar`).
- **Um turno ativo por entregador:** um segundo `POST /turnos` com outro turno ainda ativo responde `409` e não cria nada.

**Trechos críticos:** o `limite` é opcional, com padrão 10 e valores aceitos de 1 a 50 (fora disso, `400`). A lista vem em ordem decrescente de quantidade de sinistros graves.

**Regras do SOS na API:**

- **Camada 2:** a ocorrência escala quando passam `TEMPO_ACEITE_CAMADA_1_S` desde a criação, inclusive (60 s exatos já escalam).
- **Raio da camada 1:** inclusivo (um voluntário a exatamente 2 km entra).
- **Indicador de impacto:** `tempoAteAceiteSegundos` é contado do `ocorridoEm` do impacto (a queda) até o aceite, em segundos completos (arredondado para baixo) e nunca negativo. Uma ocorrência encerrada sem aceite fica com o indicador vazio e conta como "sem aceite".

**Exemplo: `POST /turnos`**

Requisição:
```json
{ "entregadorId": "3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90", "inicio": "2026-11-12T20:00:00Z", "fimPrevisto": "2026-11-13T00:00:00Z", "protecaoAtiva": true }
```

Resposta:
```json
{
  "turnoId": "9b1d0c2e-1111-4a2b-8c3d-2e4f5a6b7c8d",
  "indiceRisco": 65,
  "faixaRisco": "ALTO",
  "componentes": { "sinistro": 80, "chuva": 70, "calor": 30 },
  "explicacao": "Risco alto: chuva prevista às 21h e o seu horário tem histórico de sinistros graves."
}
```

**Exemplo: `GET /turnos/{id}/alerta`**

Resposta, durante a contagem:
```json
{ "estadoCapacete": "CONTAGEM", "contagemTerminaEm": "2026-11-12T21:16:17Z", "ocorrenciaId": null, "indiceCalorC": 37.1 }
```

- `estadoCapacete` usa o `EstadoDispositivo` e é o último estado recebido do capacete, por evento ou por telemetria.
- `contagemTerminaEm` só vem preenchido em `CONTAGEM`. Com ele, o app mostra a contagem regressiva sem depender do intervalo de polling.
- `ocorrenciaId` só vem preenchido depois do SOS, para o app abrir a tela da ocorrência.
- `indiceCalorC` é o da última telemetria (ou `null`). O app sugere pausa quando ele é maior ou igual a `ALERTA_CALOR_C`.

**Cofre médico (`POST /api/v1/cofre/{qrToken}/acesso`):**

- **Códigos:** sem login, `401`; socorrista autenticado, mas não verificado, `403`; acesso depois de expirar, `403`; `qrToken` inexistente, `404`. Toda tentativa de um socorrista autenticado, concedida ou negada, gera um `AcessoCofre` (seção 3).
- **Origem do acesso:** com ocorrência ativa do entregador (`SOS_CAMADA_1`, `SOS_CAMADA_2` ou `SOCORRO_A_CAMINHO`), a origem é `SOS_ATIVO`. Sem ocorrência ativa, o mesmo pedido de um socorrista verificado abre a emergência manual automaticamente: origem `EMERGENCIA_MANUAL` e `ocorrenciaId` vazio, sem nenhum campo extra na requisição.
- **Expiração:** o acesso expira quando passam `TTL_ACESSO_COFRE_MIN` desde que foi concedido ou quando a ocorrência vai para `ENCERRADA` ou `CANCELADA`. Na emergência manual, só o TTL vale.
- **O que a resposta mostra (o mínimo da Ficha):** `tipoSanguineo`, `alergias`, `condicoesCriticas` e os contatos de emergência (`nome`, `telefone` e `parentesco`). Os `medicamentos` **não** saem no Cofre: ficam só nas telas do próprio entregador.
- **Entregador sem ficha médica:** `200`, com os campos de saúde vazios e os contatos de emergência, se houver. A auditoria é gravada normalmente.

**Auditoria (`GET /api/v1/cofre/auditoria/{entregadorId}`):** sem login no protótipo, como as demais rotas do entregador (limitação documentada). Devolve, para cada tentativa, `acessadoEm`, `expiraEm`, `origem`, `concedido`, `motivo` e o socorrista (`nome` e `organizacao`). Nunca devolve dados de saúde, e o `ip` fica só no banco.

**`POST /turnos/{id}/alerta/cancelar`:** responde `202 Accepted` quando o capacete está em `CONTAGEM` (o comando `CANCELAR` foi publicado) e `409 Conflict`, em ProblemDetails, em qualquer outro estado. Depois do SOS, o cancelamento é pela rota `POST /ocorrencias/{id}/cancelar`, e a API envia `ENCERRAR` ao capacete.

---

## 8. Serviço de Risco (Python): `/risco/v1`

| Método | Rota | O que faz |
|---|---|---|
| POST | `/indice-turno` | Calcula o Índice de Risco do Turno |
| GET | `/trechos-criticos?limite=10` | Lista os trechos com mais sinistros graves |
| GET | `/saude` | Verificação de saúde |

**Exemplo: `POST /indice-turno`**

Requisição:
```json
{
  "inicio": "2026-11-12T20:00:00Z",
  "fim": "2026-11-13T00:00:00Z",
  "previsao": { "chuvaMmHora": 7.0, "temperaturaC": 24.0, "umidadePct": 85 }
}
```

Resposta: o mesmo formato de `indiceRisco`, `faixaRisco`, `componentes` e `explicacao` mostrado na seção 7.

**Fórmula do índice (0 a 100):**

| Componente | Peso | Normalização para 0 a 100 |
|---|---|---|
| `sinistro` | 50% | Previsão do modelo dividida pelo percentil 95 histórico (ver seção 8.1), limitada a 100 |
| `chuva` | 25% | 0 mm/h = 0; 10 mm/h ou mais = 100; linear entre eles |
| `calor` | 25% | Índice de calor de 27 °C = 0; 41 °C ou mais = 100; linear entre eles |

Faixas: 0 a 25 `BAIXO` - 26 a 50 `MODERADO` - 51 a 75 `ALTO` - 76 a 100 `MUITO_ALTO`.

**Arredondamento:** cada componente e o índice final são inteiros de 0 a 100, arredondados para o inteiro mais próximo, com 0,5 para cima. Exemplo: `sinistro` 80, `chuva` 70 e `calor` 30 dão 0,5 × 80 + 0,25 × 70 + 0,25 × 30 = 40 + 17,5 + 7,5 = **65**, faixa `ALTO`.

**A chuva aparece duas vezes, de propósito.** No componente `sinistro`, ela entra pelo modelo (o efeito da chuva no histórico de sinistros graves). No componente `chuva`, ela representa a exposição direta do entregador à condição do tempo. A `explicacao` devolvida ao entregador deve deixar essa diferença clara.

### 8.1 Artefatos do modelo de ML

Gerados pelo notebook de ML na pasta `ml/modelo/` e carregados pelo Serviço de Risco:

| Arquivo | Conteúdo |
|---|---|
| `modelo_sinistros_xgb.json` | Modelo no formato nativo do XGBoost (mais estável entre versões do que um pickle) |
| `metadados_modelo.json` | Ordem e codificação das variáveis, como converter a requisição do `POST /indice-turno` nelas, período de treino, métricas e versões das bibliotecas |
| `p95_sinistros_graves.json` | Percentil 95 histórico usado na normalização do componente `sinistro` |
| `trechos_criticos.json` | Logradouro, latitude, longitude e quantidade de sinistros graves, com pelo menos 50 trechos (para atender o parâmetro `?limite=`) |

**O que o modelo prevê:** a quantidade esperada de sinistros graves com motocicletas na cidade de São Paulo em **uma hora local (0 a 23)**. O Serviço de Risco aplica o modelo a cada hora do turno.

**Variáveis de entrada:** somente as que o Serviço de Risco consegue montar a partir da requisição: calendário (hora local, dia da semana, mês e feriado) e a previsão do tempo (`chuvaMmHora`, `temperaturaC` e `umidadePct`). Os feriados devem ser calculados da mesma forma no notebook e no Serviço de Risco, conforme descrito em `metadados_modelo.json`.

**Pendências registradas:**

| Ponto | Opções | Onde será decidido |
|---|---|---|
| Percentil 95 | Um P95 único sobre todas as horas (preserva a diferença entre horários, como 18h e 4h) ou um P95 por hora do dia | Item 10 do notebook de ML, com os números na mão. A preferência inicial é o P95 único. |
| Agregação das horas do turno | Como combinar as previsões de cada hora do turno em um único componente `sinistro` (por exemplo, média ou maior valor) | Disciplina de SOA, na implementação do `POST /indice-turno` |

---

## 9. Regras de dados pessoais (Cyber)

| Classificação | Campos | Onde **pode** aparecer | Onde **nunca** aparece |
|---|---|---|---|
| PII sensível (saúde) | Todos os campos de `FichaMedica` | Telas do próprio entregador. Na resposta de `POST /cofre/{qrToken}/acesso`, só `tipoSanguineo`, `alergias` e `condicoesCriticas`, para socorrista verificado e dentro do TTL (seção 7) | Logs, MQTT, notificações, respostas para voluntários, auditoria, telas de outros usuários |
| PII | `cpf`, `cnh`, `telefone`, `nome`, contatos de emergência | Telas do próprio entregador. Os contatos de emergência também saem na resposta do Cofre para o socorrista verificado | Logs, MQTT, respostas para voluntários |
| Localização | `latitude`, `longitude` | Exata: contato de emergência e voluntário que **aceitou**. Aproximada (arredondada para 2 casas decimais, cerca de 1 km): voluntários antes do aceite. | Logs em texto aberto, MQTT com identificação pessoal |
| Credenciais | Senhas e tokens | Nunca em texto | Qualquer saída |

Máscaras padrão: CPF `***.***.***-12`, telefone `(11) *****-1234`.

---

## 10. Repositório

- Monorepo `helmcare` com as pastas `iot/`, `api/`, `servico-risco/`, `mobile/`, `ml/`, `infra/`, `cyber/` e `docs/`.
- `README.md` na raiz, com a Ficha da CitizenTech no início, e um `README.md` em cada pasta.
- `prompts.md` na raiz, com uma seção por disciplina.
- Em `ml/`: `README.md`, o notebook, `RELATORIO_ML.md`, `requirements.txt`, `dados/` (com `brutos/` ignorada pelo Git e `processados/` versionada, contendo a base horária sem dados pessoais), `modelo/` (seção 8.1) e `figuras/`.
- Em `api/`: `HelmCare.slnx` com `src/HelmCare.Dominio`, `src/HelmCare.Api` e `tests/HelmCare.Testes` (MSTest), `compose.yaml` (PostgreSQL local), `.env.example`, `CLAUDE.md` e `README.md`.
- Em `iot/`: `README.md`, `integrantes.txt`, `wokwi.txt` (link público do projeto), `sketch.ino`, `config.h` (pinos e constantes), `diagram.json`, `libraries.txt`, `evidencias/` e, como acréscimo, `nodered/flows.json` (dashboard só de leitura).
- Branches: `main` + `feature/<disciplina>-<tarefa>` (ex.: `feature/api-sos-escalonado`), sempre criadas a partir da `main` atualizada e fechadas com PR e merge.
- Commits no padrão Conventional Commits: `feat:`, `fix:`, `docs:`, `test:`, `chore:`.

---

## 11. Como mudar este contrato

1. A necessidade aparece em uma conversa de disciplina e é marcada como **MUDANÇA DE CONTRATO**.
2. A mudança é levada para a conversa central de coordenação.
3. A conversa central gera uma nova versão do contrato (1.1, 1.2...) com o registro abaixo.
4. O arquivo antigo é substituído pelo novo nos arquivos do Projeto.

### Histórico de versões

| Versão | Data | Mudança |
|---|---|---|
| 1.0 | 06/11/2026 | Versão inicial |
| 1.1 | 07/11/2026 | Seção 8.1 (artefatos do modelo de ML, o que o modelo prevê, variáveis de entrada e pendências do P95 e da agregação do turno); nota sobre a chuva na fórmula do índice; estrutura da pasta `ml/`; `prompts.md` sem a seção de coordenação |
| 1.2 | 08/11/2026 | MQTT: o capacete publica em QoS 0 (PubSubClient) e reenvia o SOS até a confirmação, com idempotência na API; comando `CANCELAR`; rotas `GET /turnos/{id}/alerta` e `POST /turnos/{id}/alerta/cancelar`; regras de registro de cada evento (inclusive o impacto `IGNORADO`); efeito de cada comando no capacete; detecção só pelo pico, com `orientacaoAnormal` informativa; `LIMIAR_IMPACTO_G` calibrado em 2,5 g; estrutura da pasta `iot/` |
| 1.3 | 08/11/2026 | Campos internos (estado e calor no `Dispositivo`, Índice no `Turno`, estimativa e contagem no `EventoImpacto`, `entregadorId` no `AcessoCofre`, `usuarioId` no `Socorrista`) e tabela de idempotência; validações de cadastro; `ALERTA_CALOR_C` compartilhada e `indiceCalorC` no alerta do turno; telemetria em todo estado diferente de `DESARMADO`; `DESARMAR` só em `ARMADO`; `clientId` próprio por cliente e sessão persistente no capacete; SOS sem impacto em aberto; Cofre com a página fora do `/api/v1`; encerrar ocorrência sem aceite; regras da camada 2, do raio e do indicador; `GET /indicadores` opcional; localização aproximada com 2 casas decimais; configuração e saúde da API; portas do PostgreSQL local; estrutura da pasta `api/` |
| 1.4 | 08/11/2026 | Exemplo do `POST /turnos` corrigido (índice 65, conforme a fórmula) e regra de arredondamento; fórmula do índice de calor com valores de referência; `ALERTA_CALOR_C` e `LIMIAR_IMPACTO_G` com maior ou igual; `protecaoAtiva` no `POST /turnos`, que publica `ARMAR`; `409` para segundo turno ativo; `limite`, ordem e padrão dos trechos críticos na API; quadro de códigos padrão; códigos do Cofre e auditoria de toda tentativa (`concedido` e `motivo` no `AcessoCofre`) |
| 1.5 | 09/11/2026 | Cofre: o que a resposta mostra (sem `medicamentos`, como na Ficha), emergência manual automática sem ocorrência ativa, expiração também com `CANCELADA`, entregador sem ficha (`200`) e campos da auditoria (sem login e sem `ip`); seção 9 ajustada; `409` para aceite recusado; `401` para login inválido do socorrista; `pilotandoAntes` com maior ou igual a 15 km/h |
