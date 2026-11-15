> Origem: seção "Integração com o Serviço de Risco" do `api/README.md` (API principal em C#), copiada sem alteração. Data da evidência: 14/11/2026.

## Integração com o Serviço de Risco

A API é a porta de entrada do app: o app nunca chama o Serviço de Risco direto (seção 1 do contrato). A API chama duas rotas do serviço, por um HttpClient tipado (`ClienteServicoRisco`):

| Rota da API | Rota do Serviço de Risco | Sem o serviço |
|---|---|---|
| `POST /api/v1/turnos` | `POST /risco/v1/indice-turno` | O índice vem da versão simulada; o turno abre normalmente (201) |
| `GET /api/v1/trechos-criticos?limite=N` | `GET /risco/v1/trechos-criticos?limite=N` | `503` em ProblemDetails (não há versão simulada da lista) |

**Configuração** (seção `ServicoRisco` do `appsettings.json`, sem segredo):

| Chave | Padrão | Variável de ambiente |
|---|---|---|
| `UrlBase` | `http://127.0.0.1:8000/risco/v1/` (absoluta e terminada em barra) | `ServicoRisco__UrlBase` |
| `TempoLimiteS` | `3` (de 1 a 30) | `ServicoRisco__TempoLimiteS` |

O padrão usa `127.0.0.1`, e não `localhost`, porque o uvicorn sobe em `127.0.0.1` e, no Windows, o `localhost` tenta primeiro o IPv6 (`::1`). Na VM, a URL vem de `ServicoRisco__UrlBase`.

**Regra da reserva** (seção 8, "Degradação"): o `ServicoRiscoComReserva` tenta o serviço e, se ele falhar, devolve o resultado do `ServicoRiscoSimulado`. O turno nunca falha por causa do Serviço de Risco.

- `503`, qualquer outro `5xx`, erro de rede ou tempo esgotado: aviso no log e versão simulada.
- `4xx` ou resposta fora do formato: erro no log (é erro de integração) e versão simulada.
- O cancelamento da requisição pelo cliente não é falha do serviço e nunca cai na simulada.
- Os logs levam só o motivo, o status HTTP e o tempo, nunca o corpo da requisição ou da resposta.

A versão simulada usa a mesma regra do serviço: a média ponderada das horas do turno (pelos segundos de cada hora dentro dele), uma tabela de 7 dias × 24 horas gerada com o modelo real e o mesmo texto da explicação. Assim, o mesmo turno tem o mesmo índice com o serviço no ar ou não. Limitação: a tabela é a de novembro sem feriado, então em 15/11 e 20/11 a simulada pode diferir do serviço.

### Evidência (14/11/2026)

Passo 4: com o Serviço de Risco no ar (`/risco/v1/saude` = `200`), o turno do exemplo do contrato, sem proteção:

```
POST /api/v1/turnos
{"entregadorId":"01a1dcd8-a986-7d7e-93e5-afc24e4de4fd","inicio":"2026-11-12T20:00:00Z","fimPrevisto":"2026-11-13T00:00:00Z","protecaoAtiva":false}

HTTP/1.1 201 Created
Location: http://localhost:5000/api/v1/turnos/01a1dcd8-aef1-712a-996b-163c9ea7142a/risco
{"turnoId":"01a1dcd8-aef1-712a-996b-163c9ea7142a","indiceRisco":60,"faixaRisco":"ALTO","componentes":{"sinistro":84,"chuva":70,"calor":0},"explicacao":"Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros graves (pico às 19h)."}
```

```
info: HelmCare.Api.Integracoes.ServicoRisco.ServicoRiscoComReserva[600]
      Índice de Risco calculado pelo Serviço de Risco em 298 ms.
```

Passo 9: com o Serviço de Risco parado (`/risco/v1/saude` sem resposta, `000`), o mesmo pedido para o mesmo entregador:

```
HTTP/1.1 201 Created
Location: http://localhost:5000/api/v1/turnos/01a1dcde-27fb-7d1b-b24c-1bdf6dd7415b/risco
{"turnoId":"01a1dcde-27fb-7d1b-b24c-1bdf6dd7415b","indiceRisco":60,"faixaRisco":"ALTO","componentes":{"sinistro":84,"chuva":70,"calor":0},"explicacao":"Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros graves (pico às 19h)."}
```

```
warn: HelmCare.Api.Integracoes.ServicoRisco.ServicoRiscoComReserva[601]
      Serviço de Risco indisponível (motivo ErroDeRede, status sem resposta, 2154 ms). Índice de Risco calculado pela versão simulada.
```

Passo 10: ainda com o serviço parado, os trechos críticos respondem `503`, e o turno encerra normalmente:

```
GET /api/v1/trechos-criticos?limite=2

HTTP/1.1 503 Service Unavailable
Content-Type: application/problem+json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.6.4","title":"Serviço de Risco indisponível","status":503,"detail":"Os trechos críticos não estão disponíveis agora; tente de novo.","instance":"/api/v1/trechos-criticos","traceId":"00-6571db4972bd470a253f83c8c226222f-053cf357843dbdaf-00"}
```

```
warn: HelmCare.Api.Servicos.TrechosCriticos.TrechosCriticosServico[611]
      Trechos críticos indisponíveis no Serviço de Risco (motivo ErroDeRede, status sem resposta, 2053 ms). A rota responde 503.
warn: HelmCare.Api.Configuracao.TratadorExcecoes[1]
      Exceção ServicoRiscoIndisponivelException respondida com 503 (traceId 00-6571db4972bd470a253f83c8c226222f-053cf357843dbdaf-00).
```

```
POST /api/v1/turnos/01a1dcde-27fb-7d1b-b24c-1bdf6dd7415b/encerrar

HTTP/1.1 200 OK
{"turnoId":"01a1dcde-27fb-7d1b-b24c-1bdf6dd7415b","entregadorId":"01a1dcd8-a986-7d7e-93e5-afc24e4de4fd","inicio":"2026-11-12T20:00:00Z","fimPrevisto":"2026-11-13T00:00:00Z","fim":"2026-11-15T01:41:56.9591454Z","protecaoAtiva":false,"indiceRisco":60,"faixaRisco":"ALTO","componentes":{"sinistro":84,"chuva":70,"calor":0},"explicacao":"Risco alto: chuva prevista no turno e o seu horário tem histórico de sinistros graves (pico às 19h)."}
```

