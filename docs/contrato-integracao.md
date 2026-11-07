# HelmCare - Contrato de Integração

**Versão 1.0 - 06/11/2026**

Este documento define como as partes do HelmCare conversam entre si: nomes, tópicos MQTT, endpoints, payloads, estados e regras. **Todas as disciplinas seguem este contrato.** Mudanças só entram por meio da conversa central de coordenação, que gera uma nova versão.

---

## 1. Componentes e portas

| Componente | Tecnologia | Endereço local | Pasta |
|---|---|---|---|
| Capacete (protótipo) | ESP32 no Wokwi | Broker MQTT público | `iot/` |
| Broker MQTT | HiveMQ público | `broker.hivemq.com:1883` | |
| API principal | ASP.NET Core (C#) | `http://localhost:5000/api/v1` | `api/` |
| Cofre médico (página web) | Servida pela API | `http://localhost:5000/cofre/{qrToken}` | `api/` |
| Serviço de Risco | FastAPI (Python) | `http://localhost:8000/risco/v1` | `servico-risco/` |
| Banco de dados | PostgreSQL | `localhost:5432` (bancos `helmcare_api` e `helmcare_risco`) | |
| App do entregador | Expo (React Native + TypeScript) | Consome apenas a API principal | `mobile/` |
| Modelo de ML | XGBoost (arquivo exportado) | Carregado pelo Serviço de Risco | `ml/` |
| Infra | Debian + Shell Script + systemd + nginx | nginx na porta 80: `/api` e `/cofre` para a API, `/risco` para o Serviço de Risco | `infra/` |

**Regra de arquitetura:** o app **nunca** chama o Serviço de Risco diretamente. A API principal funciona como porta de entrada (gateway) e repassa as chamadas.

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
| Coordenadas | Graus decimais (`latitude`, `longitude`) |
| Dados pessoais | **Somente simulados**, nunca de pessoas reais |

---

## 3. Entidades do domínio

| Entidade | Campos principais | Observações |
|---|---|---|
| `Entregador` | `id`, `nome`, `cpf`, `cnh`, `telefone`, `criadoEm` | `cpf`, `cnh` e `telefone` são PII |
| `ContatoEmergencia` | `id`, `entregadorId`, `nome`, `telefone`, `parentesco` | PII |
| `FichaMedica` | `id`, `entregadorId`, `tipoSanguineo`, `alergias`, `condicoesCriticas`, `medicamentos` | **PII sensível (saúde)** |
| `Dispositivo` | `id`, `codigo` (ex.: `HC-0001`), `entregadorId`, `qrToken` | O `qrToken` é o que vai no QR Code do capacete |
| `Turno` | `id`, `entregadorId`, `inicio`, `fim`, `indiceRisco`, `faixaRisco`, `protecaoAtiva` | |
| `EventoImpacto` | `id`, `dispositivoId`, `ocorridoEm`, `picoG`, `orientacaoAnormal`, `pilotandoAntes`, `presencaAntes`, `resultado` | Registro de cada impacto, inclusive falsos positivos |
| `Ocorrencia` | `id`, `entregadorId`, `eventoImpactoId`, `estado`, `camada`, `latitude`, `longitude`, `criadaEm`, `aceitaEm`, `encerradaEm`, `tempoAteAceiteSegundos` | É o SOS. `tempoAteAceiteSegundos` é o indicador de impacto. |
| `Voluntario` | `id`, `entregadorId`, `tipo`, `disponivel`, `latitude`, `longitude` | Todo voluntário também é um entregador cadastrado |
| `AceiteSocorro` | `id`, `ocorrenciaId`, `voluntarioId`, `aceitoEm` | |
| `Socorrista` | `id`, `nome`, `registroProfissional`, `organizacao`, `verificado` | Perfil com acesso ao Cofre |
| `AcessoCofre` | `id`, `ocorrenciaId`, `socorristaId`, `origem`, `acessadoEm`, `expiraEm`, `ip` | Auditoria de todo acesso à ficha médica |

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
| `TipoComandoMqtt` | `ARMAR`, `DESARMAR`, `SOCORRO_A_CAMINHO`, `ENCERRAR` |

---

## 5. Constantes compartilhadas

Todas configuráveis (arquivo de configuração ou variável de ambiente), com estes valores iniciais:

| Constante | Valor | Onde é usada |
|---|---|---|
| `JANELA_OBSERVACAO_S` | 10 | ESP32 |
| `CONTAGEM_CANCELAMENTO_S` | 15 | ESP32 e app |
| `LIMIAR_IMPACTO_G` | 3,0 (calibrar no Wokwi) | ESP32 |
| `VELOCIDADE_PILOTAGEM_KMH` | 15 | ESP32 (velocidade simulada) |
| `RAIO_CAMADA_1_KM` | 2 | API |
| `TEMPO_ACEITE_CAMADA_1_S` | 60 | API |
| Camada 2 | Toda a rede de voluntários disponíveis | API |
| `TTL_ACESSO_COFRE_MIN` | 60 | API |
| `META_TEMPO_ACEITE_S` | 120 | API, app e pitch |
| `INTERVALO_TELEMETRIA_S` | 10 | ESP32 |
| `INTERVALO_POLLING_APP_S` | 5 | App (consulta de chamados e status) |
| Coordenadas simuladas do capacete | `-23.5641`, `-46.6525` (região da Av. Paulista) | ESP32 |

---

## 6. MQTT (capacete e API)

**Raiz dos tópicos:** `helmcare-rdu/v1/{codigoDispositivo}`
O prefixo `helmcare-rdu` evita colisão com outros usuários do broker público. QoS 1. **Nenhum dado pessoal trafega no MQTT**, apenas o código do dispositivo.

### 6.1 Telemetria (ESP32 para a API)

Tópico `helmcare-rdu/v1/HC-0001/telemetria`, a cada 10 s enquanto armado:

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

### 6.3 Comandos (API para o ESP32)

Tópico `helmcare-rdu/v1/HC-0001/comando`:

```json
{ "tipo": "SOCORRO_A_CAMINHO", "ts": "2026-11-12T21:16:40Z" }
```

O capacete mostra o comando no OLED (por exemplo, "Socorro a caminho").

---

## 7. API principal (C#): `/api/v1`

| Método | Rota | O que faz |
|---|---|---|
| POST | `/entregadores` | Cadastra um entregador |
| GET | `/entregadores/{id}` | Dados do entregador (PII mascarada) |
| PUT | `/entregadores/{id}/ficha-medica` | Cria ou atualiza a ficha médica |
| POST | `/entregadores/{id}/contatos` | Adiciona um contato de emergência |
| POST | `/dispositivos` | Vincula um capacete a um entregador |
| POST | `/turnos` | Inicia um turno e devolve o Índice de Risco |
| POST | `/turnos/{id}/encerrar` | Encerra o turno |
| GET | `/turnos/{id}/risco` | Índice de Risco atual do turno |
| GET | `/trechos-criticos` | Repassa a lista do Serviço de Risco |
| PUT | `/voluntarios/{id}/disponibilidade` | Atualiza a disponibilidade e a posição do voluntário |
| GET | `/ocorrencias/proximas?latitude=&longitude=` | Chamados visíveis para um voluntário (localização aproximada) |
| GET | `/ocorrencias/{id}` | Status de uma ocorrência e o cronômetro |
| POST | `/ocorrencias/{id}/aceites` | Voluntário aceita o chamado (libera a localização exata) |
| POST | `/ocorrencias/{id}/cancelar` | Entregador informa que está bem |
| POST | `/ocorrencias/{id}/encerrar` | Encerra a ocorrência |
| POST | `/socorristas/login` | Autentica um socorrista verificado |
| GET | `/cofre/{qrToken}` | Página web do Cofre (sem dados se não houver emergência) |
| POST | `/cofre/{qrToken}/acesso` | Socorrista autenticado acessa a ficha (ou abre emergência manual) |
| GET | `/cofre/auditoria/{entregadorId}` | Histórico de acessos ao Cofre |
| GET | `/saude` | Verificação de saúde (usada pelo monitoramento do OS) |

**Exemplo: `POST /turnos`**

Requisição:
```json
{ "entregadorId": "3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90", "inicio": "2026-11-12T20:00:00Z", "fimPrevisto": "2026-11-13T00:00:00Z" }
```

Resposta:
```json
{
  "turnoId": "9b1d0c2e-1111-4a2b-8c3d-2e4f5a6b7c8d",
  "indiceRisco": 72,
  "faixaRisco": "ALTO",
  "componentes": { "sinistro": 80, "chuva": 70, "calor": 30 },
  "explicacao": "Risco alto: chuva prevista às 21h e o seu horário tem histórico de sinistros graves."
}
```

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
| `sinistro` | 50% | Previsão do modelo dividida pelo percentil 95 histórico da faixa horária, limitada a 100 |
| `chuva` | 25% | 0 mm/h = 0; 10 mm/h ou mais = 100; linear entre eles |
| `calor` | 25% | Índice de calor de 27 °C = 0; 41 °C ou mais = 100; linear entre eles |

Faixas: 0 a 25 `BAIXO` - 26 a 50 `MODERADO` - 51 a 75 `ALTO` - 76 a 100 `MUITO_ALTO`.

---

## 9. Regras de dados pessoais (Cyber)

| Classificação | Campos | Onde **pode** aparecer | Onde **nunca** aparece |
|---|---|---|---|
| PII sensível (saúde) | Todos os campos de `FichaMedica` | Somente na resposta de `POST /cofre/{qrToken}/acesso`, para socorrista verificado, com ocorrência ativa e dentro do TTL | Logs, MQTT, notificações, respostas para voluntários, telas de outros usuários |
| PII | `cpf`, `cnh`, `telefone`, `nome`, contatos de emergência | Telas do próprio entregador | Logs, MQTT, respostas para voluntários |
| Localização | `latitude`, `longitude` | Exata: contato de emergência e voluntário que **aceitou**. Aproximada: voluntários antes do aceite. | Logs em texto aberto, MQTT com identificação pessoal |
| Credenciais | Senhas e tokens | Nunca em texto | Qualquer saída |

Máscaras padrão: CPF `***.***.***-12`, telefone `(11) *****-1234`.

---

## 10. Repositório

- Monorepo `helmcare` com as pastas `iot/`, `api/`, `servico-risco/`, `mobile/`, `ml/`, `infra/`, `cyber/` e `docs/`.
- `README.md` na raiz, com a Ficha da CitizenTech no início, e um `README.md` em cada pasta.
- `prompts.md` na raiz, com uma seção por disciplina e uma seção "Coordenação geral".
- Branches: `main` + `feature/<disciplina>-<tarefa>` (ex.: `feature/api-sos-escalonado`).
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
