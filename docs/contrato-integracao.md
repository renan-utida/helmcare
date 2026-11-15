# HelmCare - Contrato de Integração

**Versão 1.14 - 15/11/2026**

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

**Chamada ao Serviço de Risco:** a API usa a URL base da configuração `ServicoRisco:UrlBase` (variável de ambiente `ServicoRisco__UrlBase`, absoluta e terminada em barra) e o tempo limite `ServicoRisco:TempoLimiteS` (padrão 3 s, de 1 a 30, sem novas tentativas). No desenvolvimento, o padrão é `http://127.0.0.1:8000/risco/v1/`, e não `localhost`, porque o uvicorn sobe em `127.0.0.1` e, no Windows, o `localhost` tenta primeiro o IPv6 (`::1`), o que gasta cerca de 2 s numa conexão recusada. Na VM, a URL vem da variável de ambiente.

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
- **Relógio do capacete:** o `ts` vem do relógio do ESP32, sincronizado por NTP só na inicialização. No Wokwi, esse relógio anda na velocidade da simulação (de 25% a 50% da real antes dos ajustes de desempenho do capacete, e perto de 89% depois deles), então o `ts` atrasa em relação à hora real, e o atraso cresce com o tempo de simulação (no teste de 12/11, a 47% da velocidade real, chegou a 1 min 55 s depois de cerca de 2 min). A API nunca rejeita um evento por causa do `ts` nem compara o `ts` com o relógio do servidor. Diferenças de tempo só são calculadas entre dois instantes do mesmo relógio (veja o indicador de impacto, na seção 7).
- **Reenvio do SOS:** enquanto estiver em `SOS_ENVIADO`, o capacete republica o evento `SOS` com o **mesmo** `ts` a cada `INTERVALO_TELEMETRIA_S`, até receber `SOCORRO_A_CAMINHO` ou `ENCERRAR`. A API trata eventos com o mesmo `dispositivo`, `tipo` e `ts` como um só (idempotência). Um `SOS` repetido não cria nada, mas a API olha a ocorrência mais recente do entregador: se ela já estiver em `SOCORRO_A_CAMINHO`, a API publica `SOCORRO_A_CAMINHO` de novo, e, se já estiver `ENCERRADA` ou `CANCELADA`, publica `ENCERRAR` de novo. Assim, um comando que se perdeu é refeito no próximo reenvio, sem fila nem nova tentativa na API.
- **SOS sem impacto em aberto:** como os eventos saem em QoS 0, o `IMPACTO_DETECTADO` pode se perder. Se chegar um `SOS` sem impacto em aberto para o dispositivo, a API cria o `EventoImpacto` com os dados do próprio SOS, `ocorridoEm` = `ts` menos 25 s (`JANELA_OBSERVACAO_S` + `CONTAGEM_CANCELAMENTO_S`) e `ocorridoEmEstimado` = verdadeiro. Assim, nem o SOS nem o indicador de impacto se perdem.

**Como a API registra cada evento:**

| Evento | Quando o capacete envia | O que a API faz |
|---|---|---|
| `IMPACTO_DETECTADO` com `pilotandoAntes` e `presencaAntes` falsos | Impacto descartado. O capacete volta para `ARMADO` | Grava o `EventoImpacto` com `resultado` = `IGNORADO` |
| `IMPACTO_DETECTADO` com pelo menos um dos dois verdadeiro | Começa a observação de 10 s | Grava o `EventoImpacto` com `resultado` vazio |
| `FALSO_POSITIVO` | A velocidade de moto voltou durante a observação (maior ou igual a `VELOCIDADE_PILOTAGEM_KMH`, sustentada por 3 s) | `resultado` = `FALSO_POSITIVO` |
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
| GET | `/entregadores/{id}` | Dados do entregador (PII mascarada). Da ficha médica, só `possuiFichaMedica` |
| PUT | `/entregadores/{id}/ficha-medica` | Cria ou substitui a ficha médica e devolve a ficha completa (única rota que devolve a ficha ao próprio entregador) |
| POST | `/entregadores/{id}/contatos` | Adiciona um contato de emergência |
| POST | `/dispositivos` | Vincula um capacete a um entregador |
| POST | `/turnos` | Inicia um turno e devolve o Índice de Risco. Com `protecaoAtiva` verdadeiro, a API publica `ARMAR` para o capacete do entregador |
| POST | `/turnos/{id}/encerrar` | Encerra o turno. Com a proteção ativa, a API envia `DESARMAR` (o capacete só obedece em `ARMADO`). Com alerta em andamento (`IMPACTO_DETECTADO`, `OBSERVACAO`, `CONTAGEM` ou `SOS_ENVIADO`) ou ocorrência ativa, responde `409` |
| GET | `/turnos/{id}/risco` | Índice de Risco atual do turno |
| GET | `/turnos/{id}/alerta` | Estado do alerta de queda do capacete no turno (tela de alerta do app) |
| POST | `/turnos/{id}/alerta/cancelar` | Cancela o alerta durante a contagem (a API publica o comando `CANCELAR`) |
| GET | `/trechos-criticos?limite=10` | Repassa a lista do Serviço de Risco, com o mesmo parâmetro `limite` |
| POST | `/voluntarios` | Torna um entregador cadastrado voluntário da rede (começa indisponível) |
| PUT | `/voluntarios/{id}/disponibilidade` | Atualiza a disponibilidade e a posição do voluntário |
| GET | `/ocorrencias/proximas?voluntarioId=` | Chamados que o voluntário pode atender, pela disponibilidade e pela posição do último `PUT /voluntarios/{id}/disponibilidade` (localização aproximada) |
| GET | `/ocorrencias/{id}?voluntarioId=` | Status de uma ocorrência e o cronômetro. `voluntarioId` é opcional: a localização só vem exata para o voluntário que aceitou |
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
| Criação de recurso (`POST /entregadores`, `/entregadores/{id}/contatos`, `/dispositivos`, `/turnos`, `/voluntarios` e `/ocorrencias/{id}/aceites`) | `201 Created`, com o cabeçalho `Location`. Quando o recurso não tem `GET` próprio, o `Location` aponta para o recurso de leitura mais próximo: contatos, dispositivo e voluntário para `/entregadores/{entregadorId}`, turno para `/turnos/{id}/risco` e aceite para `/ocorrencias/{id}?voluntarioId={voluntarioId}`. O `Location` é sempre uma URL absoluta |
| Leitura, `PUT` e ações sobre um recurso existente (encerrar, cancelar) | `200 OK`, com o recurso atualizado |
| Comando enviado ao capacete sem esperar resposta (`POST /turnos/{id}/alerta/cancelar`) | `202 Accepted` |
| Dados inválidos (validações da seção 3, parâmetros fora da faixa) | `400 Bad Request` |
| Sem login onde ele é exigido (Cofre) | `401 Unauthorized` |
| Autenticado, mas sem permissão (socorrista não verificado, acesso ao Cofre expirado) | `403 Forbidden` |
| Id ou `qrToken` inexistente (entregador, turno, ocorrência, voluntário, dispositivo, Cofre) | `404 Not Found` |
| Regra de negócio ou transição de estado inválida (segundo turno ativo, ocorrência já encerrada, alerta fora da contagem, aceite simultâneo) | `409 Conflict` |
| Comando ao capacete que não chegou ao broker, nas rotas do turno (`POST /turnos` com `protecaoAtiva`, `POST /turnos/{id}/encerrar` e `POST /turnos/{id}/alerta/cancelar`) | `503 Service Unavailable`, e nada é gravado. O app pede para tentar de novo |
| Serviço de Risco fora no `GET /trechos-criticos` (qualquer falha da seção 8, "Na API principal") | `503 Service Unavailable`, com o título "Serviço de Risco indisponível". Não há lista simulada. O `POST /turnos` nunca responde esse `503`: usa a versão simulada |

Todos os erros saem em ProblemDetails. O app decide pelo status HTTP, nunca pelo texto do `title` ou do `detail`, que pode variar para o mesmo código (por exemplo, o `409` de um aceite atrasado e o de um aceite simultâneo).

**Regras do cadastro:**

- **Ficha médica:** o `GET /entregadores/{id}` nunca traz dados de saúde, só `possuiFichaMedica`. A ficha completa só volta na resposta do `PUT /entregadores/{id}/ficha-medica`, que substitui a ficha inteira. O app mostra se a ficha existe e, para editar, envia a ficha completa de novo. O app não guarda a ficha no SQLite local.
- **Capacete:** o `qrToken` e a `urlCofre` (endereço da página do Cofre) só saem na resposta do `POST /dispositivos`, para imprimir o QR Code. Nenhuma outra rota devolve o `qrToken`.

**Regras do turno:**

- **Proteção:** a requisição do `POST /turnos` aceita `protecaoAtiva` (opcional, padrão verdadeiro). Com ela verdadeira, o entregador precisa ter um capacete vinculado (senão, `409`), e a API publica `ARMAR` no tópico de comando. A proteção é desligada ao encerrar o turno, que publica `DESARMAR` (seção 7, `POST /turnos/{id}/encerrar`).
- **Um turno ativo por entregador:** um segundo `POST /turnos` com outro turno ainda ativo responde `409` e não cria nada.
- **Duração:** o `fimPrevisto` vem depois do `inicio` e no máximo 24 h depois (exatamente 24 h passa). Fora disso, `400`.
- **Encerrar:** o `200` do `POST /turnos/{id}/encerrar` traz `turnoId`, `entregadorId`, `inicio`, `fimPrevisto`, `fim`, `protecaoAtiva` e o risco no mesmo formato do `POST /turnos`.

**Trechos críticos:** o `limite` é opcional, com padrão 10 e valores aceitos de 1 a 50 (fora disso, `400`). A lista vem em ordem decrescente de quantidade de sinistros graves.

**Regras da rede de voluntários:**

- **Quem é o voluntário:** como o protótipo não tem login para voluntários, as rotas da rede identificam o voluntário pelo `voluntarioId`. A disponibilidade e a posição usadas em todas as regras (raio da camada 1, quem pode ver e quem pode aceitar) são as do último `PUT /voluntarios/{id}/disponibilidade`.
- **Cadastro:** `POST /voluntarios` recebe `{ "entregadorId": "...", "tipo": "ENTREGADOR" }` (`TipoVoluntario`). Responde `201` com `{ voluntarioId, entregadorId, tipo, disponivel, latitude, longitude }`, com `disponivel` falso e a posição `null`. Campo ausente ou `tipo` inválido, `400`; entregador inexistente, `404`; entregador que já é voluntário, `409` (um entregador tem no máximo um `Voluntario`).
- **Disponibilidade:** `PUT /voluntarios/{id}/disponibilidade` recebe `{ disponivel, latitude, longitude }`. `disponivel` é obrigatório, a latitude vai de -90 a 90 e a longitude de -180 a 180. Disponível sem as duas coordenadas, `400`; indisponível apaga a posição, mesmo que ela venha na requisição; voluntário inexistente, `404`. Responde `200` no mesmo formato do cadastro. A posição só volta nessa resposta, para o próprio voluntário, e nunca vai para o log.
- **Chamados próximos:** `GET /ocorrencias/proximas?voluntarioId=` devolve só os chamados que o voluntário pode atender: na camada 1, a até `RAIO_CAMADA_1_KM`; na camada 2, todos. Voluntário indisponível recebe a lista vazia, e o entregador nunca vê o próprio SOS. `voluntarioId` ausente, `400`; inexistente, `404`. Cada item traz `ocorrenciaId`, `estado`, `camada`, `criadaEm`, `latitude` e `longitude` (aproximadas, seção 9), `distanciaKm` (1 casa decimal) e `cronometro` (mesmo formato do `GET /ocorrencias/{id}`), em ordem crescente de distância e, no empate, de `criadaEm`.
- **Distância mostrada:** o filtro do raio usa a posição exata da ocorrência, mas a `distanciaKm` é calculada da posição do voluntário até a localização aproximada, para a distância não revelar a posição exata. Por isso ela pode diferir da distância real em até cerca de 0,7 km.
- **Aceite:** o `POST /ocorrencias/{id}/aceites` recebe `{ "voluntarioId": "..." }` e, no `201`, devolve o mesmo corpo do `GET /ocorrencias/{id}` com a localização exata para esse voluntário. `voluntarioId` ausente, `400`; ocorrência ou voluntário inexistente, `404`.
- **Cancelar e encerrar:** o `200` do `POST /ocorrencias/{id}/cancelar` e do `POST /ocorrencias/{id}/encerrar` traz o mesmo corpo do `GET /ocorrencias/{id}`, com a localização aproximada.
- **Localização:** em `GET /ocorrencias/{id}`, a localização só vem exata quando o `voluntarioId` é o do voluntário que aceitou. Sem `voluntarioId`, ou com outro voluntário, vem aproximada (seção 9).

**Regras do SOS na API:**

- **Camada 2:** a ocorrência escala quando passam `TEMPO_ACEITE_CAMADA_1_S` desde a criação, inclusive (60 s exatos já escalam). O escalonamento roda em segundo plano na API, sem depender de nenhuma requisição, e confere as ocorrências a cada 1 s (configuração interna da API, fora da seção 5). Assim, a ocorrência escala até 1 s depois dos 60 s.
- **Aviso ao contato de emergência:** no protótipo, é simulado. Depois que a ocorrência é gravada, a API registra no log só o `ocorrenciaId` e a quantidade de contatos, nunca nome, telefone ou localização.
- **Raio da camada 1:** inclusivo (um voluntário a exatamente 2 km entra).
- **Comandos da ocorrência:** o `SOCORRO_A_CAMINHO` (no aceite) e o `ENCERRAR` (no cancelamento e no encerramento) são publicados depois de gravar, sem bloquear a rota: se o broker falhar, a rota responde normalmente, porque o socorro não pode depender do capacete. O comando é refeito no próximo reenvio do `SOS` (seção 6.2). Se a vítima não tem capacete vinculado, nenhum comando é publicado: fica um aviso no log, e a rota responde normalmente.
- **Concorrência:** o escalonamento e as rotas da ocorrência mudam a mesma linha. O aceite faz uma nova tentativa automática num conflito de concorrência e, se a ocorrência relida já não aguarda aceite, responde `409`. O cancelar e o encerrar respondem `409` num conflito, e o app repete a chamada.
- **Escalonamento com o banco fora:** o laço registra um erro a cada rodada (1 s) e continua; volta a escalar sozinho quando o banco volta.
- **Cronômetro:** `GET /ocorrencias/{id}` traz `cronometro` com `segundosDesdeQueda`, `metaSegundos` (`META_TEMPO_ACEITE_S`) e `parado`. O `segundosDesdeQueda` segue a regra dos dois trechos do indicador, com o trecho do servidor indo até o aceite, até o encerramento ou até agora. `parado` é verdadeiro depois do aceite ou do fim da ocorrência. Entre uma consulta e outra, o app pode somar os segundos no próprio relógio.
- **Indicador de impacto:** `tempoAteAceiteSegundos` é o tempo da queda até o aceite, medido em dois trechos, cada um no próprio relógio, para nunca misturar o relógio do capacete com o do servidor:
  1. **Trecho do capacete:** `contagemTerminaEm` menos `ocorridoEm` do `EventoImpacto`, os dois no relógio do capacete. Vale `JANELA_OBSERVACAO_S` + `CONTAGEM_CANCELAMENTO_S` (25 s) quando o impacto foi estimado (`ocorridoEmEstimado`) ou quando o `contagemTerminaEm` está vazio porque o `CONTAGEM_INICIADA` se perdeu.
  2. **Trecho do servidor:** `aceitaEm` menos `criadaEm` da `Ocorrencia`, os dois no relógio do servidor.

  Cada trecho nunca é negativo (abaixo de zero, conta zero). O indicador é a soma dos dois, em segundos completos (arredondado para baixo). Uma ocorrência encerrada sem aceite fica com o indicador vazio e conta como "sem aceite". Num ESP32 real, com o relógio certo, o resultado é o mesmo de contar direto da queda até o aceite. No Wokwi, o relógio do capacete atrasa em relação à hora real (seção 6.2), e essa regra impede que o atraso entre no indicador.

**Exemplo: `POST /turnos`**

Requisição:
```json
{ "entregadorId": "3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90", "inicio": "2026-11-12T20:00:00Z", "fimPrevisto": "2026-11-13T00:00:00Z", "protecaoAtiva": true }
```

Resposta:
```json
{
  "turnoId": "9b1d0c2e-1111-4a2b-8c3d-2e4f5a6b7c8d",
  "indiceRisco": 60,
  "faixaRisco": "ALTO",
  "componentes": { "sinistro": 84, "chuva": 70, "calor": 0 },
  "explicacao": "Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros graves (pico às 19h)."
}
```

É o resultado real desse turno (quinta-feira, das 17h às 21h no horário local) com a previsão do exemplo da seção 8 (7 mm/h, 24 °C e 85%), que a API repassa ao Serviço de Risco. A conta da fórmula com 80, 70 e 30 dando 65 continua como exemplo de arredondamento na seção 8.

**Exemplo: `GET /turnos/{id}/alerta`**

Resposta, durante a contagem:
```json
{ "estadoCapacete": "CONTAGEM", "contagemTerminaEm": "2026-11-12T21:16:17Z", "ocorrenciaId": null, "indiceCalorC": 37.1 }
```

- `estadoCapacete` usa o `EstadoDispositivo` e é o último estado recebido do capacete, por evento ou por telemetria. Se o entregador não tem capacete vinculado (turno com `protecaoAtiva` falso), a resposta é `200` com todos os campos `null`, e o `POST /turnos/{id}/alerta/cancelar` responde `409`. O alerta de um turno já encerrado continua respondendo.
- `contagemTerminaEm` só vem preenchido em `CONTAGEM` e está no relógio do capacete, que no Wokwi atrasa em relação à hora real. Por isso o app não usa esse campo para a contagem regressiva:
  - conta os 15 s (`CONTAGEM_CANCELAMENTO_S`) no próprio relógio, a partir da primeira leitura em que `estadoCapacete` é `CONTAGEM`, e mostra o `contagemTerminaEm` só como informação;
  - quando a contagem do app chega a zero e o estado ainda é `CONTAGEM`, mostra "Enviando SOS..." até o estado mudar;
  - o botão de cancelar segue habilitado enquanto `estadoCapacete` for `CONTAGEM`. Quem decide é a API: `POST /turnos/{id}/alerta/cancelar` responde `202` em `CONTAGEM` e `409` depois.
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

Resposta: o mesmo formato de `indiceRisco`, `faixaRisco`, `componentes` e `explicacao` mostrado na seção 7. Com o modelo real, essa requisição (quinta-feira, das 17h às 21h no horário local) dá `sinistro` 84, `chuva` 70 e `calor` 0 (24 °C com 85% de umidade dão índice de calor de 24,7 °C, abaixo de 27), com `indiceRisco` 60 e faixa `ALTO`, e `explicacao` "Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros graves (pico às 19h).". É o exemplo de referência dos testes do Serviço de Risco e da versão simulada da API.

**Regras da requisição (`400` em ProblemDetails, como na seção 7):**

- `inicio` e `fim` vêm com fuso (ISO 8601 com `Z` ou deslocamento); data sem fuso é recusada. O serviço normaliza para UTC.
- O `fim` vem depois do `inicio`, com no máximo 24 h de turno (exatamente 24 h passa), como no `POST /turnos`.
- `previsao` é obrigatória: `chuvaMmHora` de 0 a 200, `temperaturaC` de -10 a 50 e `umidadePct` de 0 a 100.
- A previsão é uma só para o turno inteiro. Por isso a `explicacao` fala em "chuva prevista no turno", sem citar a hora da chuva.
- `limite` fora de 1 a 50 no `GET /trechos-criticos` também dá `400` (o FastAPI devolveria `422`, e o serviço troca para `400`).

**Saúde:** `GET /risco/v1/saude` responde `200` com `{ "status": "Healthy" }`, ou `503` com `{ "status": "Unhealthy" }` quando o banco ou o modelo não estão disponíveis. O monitoramento olha o status HTTP, como na API principal.

**Degradação:** o serviço sobe mesmo que o modelo ou o banco falhem na inicialização, e cada rota responde conforme o que falta:

| Falha | `POST /indice-turno` | `GET /trechos-criticos` | `GET /saude` |
|---|---|---|---|
| Modelo não carregou | `503` em ProblemDetails | `200` | `503` |
| Banco fora do ar | `200`, sem gravar o histórico | `503` em ProblemDetails | `503` |

**Na API principal:** no `POST /turnos`, qualquer falha do Serviço de Risco leva à versão simulada, e o turno nunca falha por causa dele.

| Falha na chamada ao Serviço de Risco | Log da API | Resultado |
|---|---|---|
| `503`, outro `5xx`, erro de rede ou tempo esgotado | Aviso | Versão simulada |
| `4xx` ou resposta fora do formato (JSON inválido, campo faltando, índice ou componente fora de 0 a 100, faixa desconhecida, explicação vazia) | Erro (é erro de integração) | Versão simulada |
| Requisição cancelada pelo próprio cliente | Nenhum | Não é falha do serviço: o cancelamento sobe e a simulada não é usada |

Os logs levam só o motivo, o status HTTP e o tempo, nunca o corpo da requisição ou da resposta. No `GET /api/v1/trechos-criticos`, as mesmas falhas dão `503` (seção 7), e o `limite` fora de 1 a 50 dá `400` sem chamar o serviço.

**Versão simulada da API:** usa uma tabela de 7 dias × 24 horas com o componente `sinistro` de cada hora local já normalizado (100 × previsão ÷ P95, limitado a 100, com 2 casas), gerada com o modelo real (versão 1.0.0) para novembro e sem feriado, e as mesmas horas do turno, agregação e explicação do serviço (seção 8.1). Para turnos de novembro em dias sem feriado, dá o mesmo resultado do serviço; em feriados (como 15/11 e 20/11) e em outros meses, pode diferir. Se a versão do modelo mudar, a tabela é gerada de novo.

**Erros e documentação:** os erros saem em `application/problem+json` (`400`, `404`, `405`, `500` e `503`), sem repetir o valor recebido e sem detalhes internos. A documentação fica em `/risco/v1/docs` (Swagger) e o esquema em `/risco/v1/openapi.json`.

**Explicação:** uma frase só, com ponto final, nesta ordem: `Risco {faixa}: `, depois `chuva prevista no turno` (componente `chuva` 1 ou mais), depois o histórico do horário, sempre presente, e, por último, `calor previsto no turno (índice de calor de {x,x} °C)` (componente `calor` 1 ou mais, com uma casa decimal, vírgula e 0,5 para cima). Os trechos são unidos por vírgula, com " e " antes do último. A versão simulada da API gera o mesmo texto. O histórico do horário é uma de três frases, escolhida pelo componente `sinistro`:

| Componente `sinistro` | Frase |
|---|---|
| 51 ou mais | `o seu horário tem histórico de sinistros graves (pico às {hora}h)` |
| 26 a 50 | `o seu horário tem histórico moderado de sinistros graves` |
| Até 25 | `o seu horário tem poucos sinistros graves no histórico` |

Os limites das frases valem sobre os componentes já arredondados a inteiro, os mesmos da resposta. A `{hora}` do pico é a hora local de maior valor de `sinistro` entre as horas do turno (a primeira delas, em caso de empate).

**Histórico dos cálculos:** cada `POST /indice-turno` grava as entradas, o resultado, as previsões de cada hora e a versão do modelo no banco `helmcare_risco`, sem nenhum dado pessoal. Se a gravação falhar, o cálculo responde `200` mesmo assim e o erro vai para o log: o entregador precisa do índice mais do que o serviço precisa do histórico. Não há rota para consultar o histórico.

**Fórmula do índice (0 a 100):**

| Componente | Peso | Normalização para 0 a 100 |
|---|---|---|
| `sinistro` | 50% | Para cada hora do turno: 100 × previsão do modelo ÷ P95 (0,322927, seção 8.1), limitado a 100. As horas do turno são agregadas pela média ponderada (seção 8.1) |
| `chuva` | 25% | 0 mm/h = 0; 10 mm/h ou mais = 100; linear entre eles |
| `calor` | 25% | Índice de calor de 27 °C = 0; 41 °C ou mais = 100; linear entre eles |

Faixas: 0 a 25 `BAIXO`, 26 a 50 `MODERADO`, 51 a 75 `ALTO` e 76 a 100 `MUITO_ALTO`.

**Arredondamento:** cada componente e o índice final são inteiros de 0 a 100, arredondados para o inteiro mais próximo, com 0,5 para cima. Exemplo: `sinistro` 80, `chuva` 70 e `calor` 30 dão 0,5 × 80 + 0,25 × 70 + 0,25 × 30 = 40 + 17,5 + 7,5 = **65**, faixa `ALTO`.

**A chuva entra só pelo componente `chuva`.** O modelo de ML usa só o calendário (seção 8.1), porque as variáveis do tempo, testadas no notebook, não melhoraram as previsões. A chuva e o calor entram no índice pelos componentes `chuva` e `calor`, como exposição direta do entregador à condição do tempo. A `explicacao` devolvida ao entregador nunca diz que a chuva aumenta os sinistros graves: o histórico não mostra isso. Ela pode dizer que há chuva prevista (pede atenção redobrada) e que o horário tem histórico de sinistros graves, como no exemplo da seção 7.

**Exemplo: `GET /trechos-criticos?limite=2`** (mesmo formato no Serviço de Risco e na API principal)

```json
{
  "periodo": "2019 a 2025",
  "trechos": [
    { "posicao": 1, "logradouro": "SP 270 (RODOVIA RAPOSO TAVARES)", "latitude": -23.58341, "longitude": -46.77771, "quantidadeSinistrosGraves": 46 },
    { "posicao": 2, "logradouro": "SP 270 (RODOVIA RAPOSO TAVARES)", "latitude": -23.58481, "longitude": -46.78738, "quantidadeSinistrosGraves": 38 }
  ]
}
```

### 8.1 Artefatos do modelo de ML

Gerados pelo notebook de ML na pasta `ml/modelo/` e carregados pelo Serviço de Risco:

| Arquivo | Conteúdo |
|---|---|
| `modelo_sinistros_xgb.json` | Modelo no formato nativo do XGBoost (mais estável entre versões do que um pickle) |
| `metadados_modelo.json` | Ordem e codificação das variáveis, como converter a requisição do `POST /indice-turno` nelas, período de treino, métricas e versões das bibliotecas |
| `p95_sinistros_graves.json` | Percentil 95 usado na normalização do componente `sinistro`: campo `p95` (0,322927) e a fórmula |
| `trechos_criticos.json` | Campos `periodo`, `fonte`, `criterio`, `quantidadeTrechos` e a lista `trechos`, em que cada trecho tem `posicao`, `logradouro`, `latitude`, `longitude` e `quantidadeSinistrosGraves`. São 100 trechos (pelo menos 50, para atender o parâmetro `?limite=`), em ordem decrescente de quantidade |

**O que o modelo prevê:** a quantidade esperada de sinistros graves com motocicletas na cidade de São Paulo em **uma hora local (0 a 23)**. O Serviço de Risco aplica o modelo a cada hora do turno.

**Variáveis de entrada:** só o calendário, na ordem `hora`, `dia_semana` (0 = segunda), `mes` e `feriado`, todos da data e da hora locais. A previsão do tempo (`chuvaMmHora`, `temperaturaC` e `umidadePct`) continua na requisição do `POST /indice-turno`, mas entra só nos componentes `chuva` e `calor`. **Motivo:** no notebook, as variáveis do tempo não melhoraram as previsões (deviance de validação de 0,6977 sem elas contra 0,6990 com elas; no teste, 0,7429 contra 0,7427), e o modelo com elas aprendia um efeito de chuva forte que não se repete fora do treino. Os feriados seguem a regra e a lista de datas de `metadados_modelo.json`.

**Horas do turno:** o `inicio` e o `fim` chegam em UTC (seção 2). O Serviço de Risco percorre as horas cheias que se sobrepõem ao turno e converte o início de cada uma para `America/Sao_Paulo` antes de montar as variáveis. No exemplo da seção 8, das 20h às 00h em UTC, as horas locais são 17h, 18h, 19h e 20h de quinta-feira. Os passos completos estão em `metadados_modelo.json`.

**Percentil 95:** P95 único das previsões do modelo final nas 61.368 horas locais de 2019 a 2025, igual a **0,322927** sinistro grave esperado por hora. Foi escolhido no lugar do P95 por hora do dia porque só ele preserva a ordem do risco entre os horários (numa quinta-feira de novembro, o componente vai de 21 pontos de madrugada a 88 às 19h). O P95 por hora colocaria as 9h acima das 18h, e 63% das horas cairiam na faixa de 76 a 100.

**Agregação das horas do turno:** cada hora é normalizada pelo P95 (100 × previsão ÷ P95, limitado a 100, ainda sem arredondar), e o componente `sinistro` é a média ponderada desses valores pelo tempo (em segundos) que cada hora fica dentro do turno, com um único arredondamento no fim (0,5 para cima). O arredondamento por hora do passo 6 de `metadados_modelo.json` não é usado. **Motivo:** o P95 é um percentil de horas isoladas, então a média do turno fica na mesma escala dele. O maior valor deixaria turnos de 4, 10 e 12 horas que passam pelo pico todos com o mesmo número, e a soma cresceria só com a duração do turno. O pico não se perde: a `explicacao` pode citar a hora de maior risco. A versão simulada da API principal usa a mesma regra, para o mesmo turno ter o mesmo índice com o Serviço de Risco no ar ou não.

---

## 9. Regras de dados pessoais (Cyber)

| Classificação | Campos | Onde **pode** aparecer | Onde **nunca** aparece |
|---|---|---|---|
| PII sensível (saúde) | Todos os campos de `FichaMedica` | Telas do próprio entregador. Na resposta de `POST /cofre/{qrToken}/acesso`, só `tipoSanguineo`, `alergias` e `condicoesCriticas`, para socorrista verificado e dentro do TTL (seção 7) | Logs, MQTT, notificações, respostas para voluntários, auditoria, telas de outros usuários |
| PII | `cpf`, `cnh`, `telefone`, `nome`, contatos de emergência | Telas do próprio entregador. Os contatos de emergência também saem na resposta do Cofre para o socorrista verificado | Logs, MQTT, respostas para voluntários |
| Localização | `latitude`, `longitude` | Exata: contato de emergência e voluntário que **aceitou**. Aproximada (arredondada para 2 casas decimais, cerca de 1 km): voluntários antes do aceite. | Logs em texto aberto, MQTT com identificação pessoal |
| Credenciais | Senhas e tokens | Nunca em texto | Qualquer saída |

Máscaras padrão: CPF `***.***.***-12`, CNH `*******8900`, celular `(11) *****-1234` e telefone fixo `(11) ****-4567`.

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
| 1.6 | 09/11/2026 | Rede de voluntários: `GET /ocorrencias/proximas?voluntarioId=` (posição e disponibilidade do último `PUT`), corpo do `POST /ocorrencias/{id}/aceites` com `voluntarioId` e localização exata na resposta, e `voluntarioId` opcional em `GET /ocorrencias/{id}` para a localização exata do voluntário que aceitou |
| 1.7 | 12/11/2026 | Indicador de impacto medido em dois trechos, cada um no próprio relógio (capacete e servidor); `contagemTerminaEm` do alerta só como informação, com a contagem regressiva no relógio do app; relógio do capacete no Wokwi (seção 6.2); falso positivo com a velocidade sustentada por 3 s; ajustes de pontuação |
| 1.8 | 12/11/2026 | Cadastro: ficha médica fora do `GET /entregadores/{id}` (só `possuiFichaMedica`), `qrToken` e `urlCofre` só no `POST /dispositivos`, máscaras da CNH e do telefone fixo; `Location` dos recursos sem `GET` próprio; `503` quando um comando do turno não chega ao broker; duração máxima do turno e resposta do encerrar; `IMPACTO_DETECTADO` como alerta em andamento; `DESARMAR` com a proteção ativa; alerta sem capacete com `200` e campos `null`; comandos da ocorrência sem bloquear a rota e refeitos no reenvio do `SOS`; formato do cronômetro; componente `sinistro` simulado até o Serviço de Risco |
| 1.9 | 14/11/2026 | Modelo de ML só com o calendário (as variáveis do tempo não melhoraram as previsões); a chuva entra só pelo componente `chuva`, e a explicação nunca diz que ela aumenta os sinistros graves; P95 único de 0,322927; conversão das horas do turno de UTC para o horário local; campos do `trechos_criticos.json` e exemplo do `GET /trechos-criticos`; velocidade do Wokwi depois dos ajustes do capacete |
| 1.10 | 14/11/2026 | Agregação das horas do turno pela média ponderada pelos minutos (fim da pendência da seção 8.1), também na versão simulada da API; regras da requisição do `POST /indice-turno` (fuso obrigatório, duração, faixas da previsão); corpo do `/saude` do Serviço de Risco; histórico dos cálculos sem bloquear a resposta; explicação com "chuva prevista no turno"; resultado real do exemplo da seção 8 |
| 1.11 | 14/11/2026 | Degradação do Serviço de Risco (o que cada rota responde sem o modelo ou sem o banco); erros em problem+json e caminho da documentação; formato da explicação em uma frase, com o pico do horário; exemplo do `POST /turnos` com o resultado real; pesos da agregação em segundos |
| 1.12 | 14/11/2026 | Integração da API com o Serviço de Risco: URL base com `127.0.0.1` e tempo limite de 3 s sem novas tentativas; falhas que levam à versão simulada, com aviso ou erro no log; cancelamento do cliente fora da reserva; `503` do `GET /trechos-criticos` com o serviço fora; tabela de novembro sem feriado na versão simulada; as três frases do histórico do horário e a hora do pico na explicação |
| 1.13 | 15/11/2026 | Rede de voluntários: `POST /voluntarios` (cadastro, começa indisponível), corpo e validações do `PUT /voluntarios/{id}/disponibilidade`, campos e ordem dos chamados próximos, distância até a localização aproximada; respostas do aceite, do cancelar e do encerrar da ocorrência; `Location` do voluntário e do aceite; escalonamento em segundo plano a cada 1 s; aviso simulado ao contato de emergência |
| 1.14 | 15/11/2026 | SOS escalonado na API: o SOS repetido olha a ocorrência mais recente do entregador; `Location` sempre absoluto; o app decide pelo status HTTP, não pelo texto do erro; sem capacete vinculado, o comando da ocorrência não é publicado; nova tentativa só no aceite e `409` no cancelar e no encerrar em conflito de concorrência; erro por rodada no escalonamento com o banco fora |
