// =====================================================================
// HelmCare - Módulo do capacete (ESP32 no Wokwi)
// Renan Dias Utida - RM 558540 - 3ESPW - FIAP
//
// Etapa 2: máquina de estados da detecção de queda e MQTT.
//
// O capacete fica DESARMADO até receber ARMAR (a API envia quando o
// entregador inicia o turno com a proteção ligada). Armado, ele:
//   1. detecta o impacto (aceleração total >= LIMIAR_IMPACTO_G);
//   2. confere se a pessoa pilotava ou estava com o capacete na cabeça
//      nos 5 s anteriores; se não, o impacto é IGNORADO;
//   3. observa 10 s: se a velocidade de moto voltar, é FALSO_POSITIVO;
//   4. senão, conta 15 s com buzzer, LED e OLED, e o botão (ou o
//      comando CANCELAR) cancela;
//   5. no fim da contagem envia o SOS e o reenvia a cada 10 s, com o
//      mesmo ts, até receber SOCORRO_A_CAMINHO ou ENCERRAR.
//
// Tópicos, payloads e regras seguem o Contrato de Integração v1.6 (seção 6).
// Nenhum dado pessoal sai do capacete: só o código HC-0001.
// =====================================================================

#include <WiFi.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <time.h>
#include <Wire.h>
#include <Adafruit_Sensor.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <DHT.h>
#include "config.h"

// ---------------------------------------------------------------------
// Estados do capacete (EstadoDispositivo, Contrato seção 4)
// ---------------------------------------------------------------------
enum EstadoDispositivo {
  DESARMADO,
  ARMADO,
  IMPACTO_DETECTADO,
  OBSERVACAO,
  CONTAGEM,
  SOS_ENVIADO,
  CANCELADO,
  FALSO_POSITIVO
};

// Converte o estado no texto do Contrato (o mesmo que vai no JSON).
const char* nomeEstado(EstadoDispositivo e) {
  switch (e) {
    case DESARMADO:         return "DESARMADO";
    case ARMADO:            return "ARMADO";
    case IMPACTO_DETECTADO: return "IMPACTO_DETECTADO";
    case OBSERVACAO:        return "OBSERVACAO";
    case CONTAGEM:          return "CONTAGEM";
    case SOS_ENVIADO:       return "SOS_ENVIADO";
    case CANCELADO:         return "CANCELADO";
    case FALSO_POSITIVO:    return "FALSO_POSITIVO";
  }
  return "?";
}

EstadoDispositivo estado = DESARMADO;
unsigned long estadoDesde = 0;   // millis() de quando entrou no estado atual

// ---------------------------------------------------------------------
// Objetos dos sensores, do display e da rede
// ---------------------------------------------------------------------
Adafruit_MPU6050 mpu;
// -1: sem pino de reset. Os dois últimos valores mantêm o I2C a 400 kHz
// durante e depois de cada desenho (a biblioteca voltaria para 100 kHz).
Adafruit_SSD1306 tela(OLED_LARGURA, OLED_ALTURA, &Wire, -1, I2C_FREQUENCIA_HZ, I2C_FREQUENCIA_HZ);
DHT dht(PINO_DHT, DHT22);

WiFiClient wifiClient;
PubSubClient mqtt(wifiClient);

// ---------------------------------------------------------------------
// Últimas leituras dos sensores
// ---------------------------------------------------------------------
float acelXg = 0, acelYg = 0, acelZg = 1;   // aceleração em g por eixo
float acelTotalG = 1;                        // módulo do vetor (em repouso, 1 g)
float inclinacaoGraus = 0;                   // 0 = capacete de pé
float giroGrausS = 0;                        // rotação total em graus por segundo
bool orientacaoAnormal = false;

float temperaturaC = NAN;
float umidadePct = NAN;
float indiceCalorC = NAN;

int velocidadeKmh = 0;
bool presenca = false;
bool botaoPressionado = false;

bool oledOk = false;
bool mpuOk = false;

// Cópia do último quadro enviado ao OLED (128 x 64 pixels = 1024 bytes).
// Só mandamos um quadro novo pelo I2C quando ele é diferente deste.
constexpr size_t TAMANHO_QUADRO = OLED_LARGURA * OLED_ALTURA / 8;
uint8_t ultimoQuadro[TAMANHO_QUADRO];
bool temQuadroAnterior = false;
int redesenhosOled = 0;   // quantos quadros foram enviados desde o último [STATUS]

// ---------------------------------------------------------------------
// Histórico dos últimos 5 s (velocidade e presença), em fila circular.
// É ele que responde "a pessoa pilotava ou estava com o capacete
// ANTES do impacto?". Uma amostra a cada 250 ms: 20 amostras.
// ---------------------------------------------------------------------
constexpr int TAM_HISTORICO = (JANELA_PRE_IMPACTO_S * 1000) / INTERVALO_HISTORICO_MS;
int historicoVelocidade[TAM_HISTORICO];
bool historicoPresenca[TAM_HISTORICO];
int historicoPosicao = 0;     // onde entra a próxima amostra
int historicoQuantidade = 0;  // quantas amostras válidas existem

// ---------------------------------------------------------------------
// Dados do impacto em andamento. Todos os eventos de um mesmo impacto
// repetem esses valores (Contrato, seção 6.2).
// ---------------------------------------------------------------------
struct DadosImpacto {
  float picoG;
  bool orientacaoAnormal;
  bool pilotandoAntes;
  bool presencaAntes;
  char tsImpacto[25];   // "2026-11-12T21:15:30Z" (20 caracteres + folga)
};
DadosImpacto impacto;

char tsSos[25];                    // ts fixo do SOS, repetido em todo reenvio
bool socorroACaminho = false;      // chegou o comando SOCORRO_A_CAMINHO?
bool acimaDoLimiarAntes = false;   // para detectar só a "subida" do limiar
unsigned long velocidadeAltaDesde = 0;  // 0 = velocidade abaixo de 15 km/h

// Aviso temporário no OLED (por exemplo, "Impacto ignorado")
char mensagem[22] = "";
unsigned long mensagemAte = 0;

// ---------------------------------------------------------------------
// Controle de tempo de cada tarefa (padrão millis, sem delay)
// ---------------------------------------------------------------------
unsigned long ultimoMpu = 0;
unsigned long ultimoDht = 0;
unsigned long ultimaAmostraHistorico = 0;
unsigned long ultimaTela = 0;
unsigned long ultimoStatus = 0;
unsigned long ultimaTelemetria = 0;
unsigned long ultimoReenvioSos = 0;
unsigned long ultimaTentativaMqtt = 0;
long ultimoSegundoBipado = -1;

// Debounce do botão
bool leituraBotaoAnterior = HIGH;
unsigned long mudancaBotaoEm = 0;

// =====================================================================
// Hora (NTP) e formato ISO 8601 em UTC
// =====================================================================

// Antes de sincronizar, o ESP32 acha que está em 1970. Qualquer hora
// depois de 2023 (1700000000 segundos) significa que o NTP já respondeu.
bool relogioSincronizado() {
  return time(nullptr) > 1700000000;
}

// Escreve a hora atual em UTC no formato do Contrato: 2026-11-12T21:15:30Z
void horaUtcIso(char* destino, size_t tamanho) {
  time_t agora = time(nullptr);
  struct tm utc;
  gmtime_r(&agora, &utc);
  strftime(destino, tamanho, "%Y-%m-%dT%H:%M:%SZ", &utc);
}

// =====================================================================
// Leituras dos sensores
// =====================================================================
void lerMpu() {
  if (!mpuOk) return;

  sensors_event_t a, g, temp;
  mpu.getEvent(&a, &g, &temp);

  // A biblioteca devolve m/s². Dividindo pela gravidade padrão, ficamos em g.
  acelXg = a.acceleration.x / SENSORS_GRAVITY_STANDARD;
  acelYg = a.acceleration.y / SENSORS_GRAVITY_STANDARD;
  acelZg = a.acceleration.z / SENSORS_GRAVITY_STANDARD;

  // Aceleração total: módulo do vetor (x, y, z). É ela que dispara o impacto.
  acelTotalG = sqrt(acelXg * acelXg + acelYg * acelYg + acelZg * acelZg);

  // Inclinação: ângulo entre o vetor medido e o eixo Z (topo da cabeça).
  if (acelTotalG > 0.1f) {
    float cosseno = constrain(acelZg / acelTotalG, -1.0f, 1.0f);
    inclinacaoGraus = degrees(acos(cosseno));
  }
  orientacaoAnormal = inclinacaoGraus > INCLINACAO_ANORMAL_GRAUS;

  // Rotação total, de rad/s para graus/s (informativo: imobilidade).
  float gx = degrees(g.gyro.x), gy = degrees(g.gyro.y), gz = degrees(g.gyro.z);
  giroGrausS = sqrt(gx * gx + gy * gy + gz * gz);
}

void lerDht() {
  float t = dht.readTemperature();   // °C
  float h = dht.readHumidity();      // %

  // O DHT às vezes devolve NAN. Nesse caso, mantemos a última leitura boa.
  if (isnan(t) || isnan(h)) {
    Serial.println("Aviso: falha na leitura do DHT22");
    return;
  }

  temperaturaC = t;
  umidadePct = h;
  // Índice de calor da NOAA, em Celsius (Contrato, seção 6.1).
  indiceCalorC = dht.computeHeatIndex(t, h, false);
}

void lerVelocidade() {
  // O ADC do ESP32 vai de 0 a 4095. Convertemos para 0 a 60 km/h.
  int bruto = analogRead(PINO_VELOCIDADE);
  velocidadeKmh = (int)round(bruto * (float)VELOCIDADE_MAX_SIM_KMH / 4095.0f);
}

void lerPresenca() {
  // INPUT_PULLUP: a chave do lado do fio preto liga ao GND e dá LOW.
  presenca = digitalRead(PINO_PRESENCA) == LOW;
}

// Lê o botão com debounce e devolve true só no instante em que ele é apertado.
bool lerBotao() {
  bool leitura = digitalRead(PINO_BOTAO);
  bool acabouDeApertar = false;

  if (leitura != leituraBotaoAnterior) {
    mudancaBotaoEm = millis();
    leituraBotaoAnterior = leitura;
  }

  if (millis() - mudancaBotaoEm > DEBOUNCE_MS) {
    bool pressionadoAgora = (leitura == LOW);
    if (pressionadoAgora && !botaoPressionado) acabouDeApertar = true;
    botaoPressionado = pressionadoAgora;
  }
  return acabouDeApertar;
}

// =====================================================================
// Histórico dos 5 s antes do impacto
// =====================================================================
void limparHistorico() {
  historicoPosicao = 0;
  historicoQuantidade = 0;
}

void guardarAmostraHistorico() {
  historicoVelocidade[historicoPosicao] = velocidadeKmh;
  historicoPresenca[historicoPosicao] = presenca;
  // Fila circular: depois da última posição, volta para a primeira.
  historicoPosicao = (historicoPosicao + 1) % TAM_HISTORICO;
  if (historicoQuantidade < TAM_HISTORICO) historicoQuantidade++;
}

// Pilotava se, em alguma amostra dos últimos 5 s, a velocidade foi >= 15 km/h.
bool pilotavaAntes() {
  for (int i = 0; i < historicoQuantidade; i++) {
    if (historicoVelocidade[i] >= VELOCIDADE_PILOTAGEM_KMH) return true;
  }
  return false;
}

// Estava com o capacete se, em alguma amostra dos últimos 5 s, a presença foi SIM.
bool estavaComCapaceteAntes() {
  for (int i = 0; i < historicoQuantidade; i++) {
    if (historicoPresenca[i]) return true;
  }
  return false;
}

// =====================================================================
// MQTT: publicação
// =====================================================================

// Serializa o JSON e publica (QoS 0, sem retain). Devolve se deu certo.
bool publicarJson(const char* topico, JsonDocument& doc) {
  if (!mqtt.connected() || !relogioSincronizado()) {
    Serial.printf("[MQTT] Nao publicado em %s (sem conexao ou sem hora)\n", topico);
    return false;
  }

  char buffer[MQTT_BUFFER];
  serializeJson(doc, buffer, sizeof(buffer));
  bool ok = mqtt.publish(topico, buffer);

  Serial.printf("[MQTT] %s %s\n       %s\n", ok ? "Publicado em" : "FALHA ao publicar em", topico, buffer);
  return ok;
}

// Arredonda para uma casa decimal (33.4 em vez de 33.400001).
double umaCasa(float valor) {
  return round(valor * 10.0) / 10.0;
}

// Telemetria (Contrato, seção 6.1). bateriaPct é opcional e não entra.
void publicarTelemetria() {
  char ts[25];
  horaUtcIso(ts, sizeof(ts));

  JsonDocument doc;
  doc["dispositivo"] = CODIGO_DISPOSITIVO;
  doc["ts"] = ts;
  doc["estado"] = nomeEstado(estado);

  // Se o DHT ainda não leu, os campos vão como null em vez de um número falso.
  if (isnan(temperaturaC)) {
    doc["temperaturaC"] = nullptr;
    doc["umidadePct"] = nullptr;
    doc["indiceCalorC"] = nullptr;
  } else {
    doc["temperaturaC"] = umaCasa(temperaturaC);
    doc["umidadePct"] = (int)round(umidadePct);
    doc["indiceCalorC"] = umaCasa(indiceCalorC);
  }

  doc["presenca"] = presenca;
  doc["velocidadeKmh"] = velocidadeKmh;

  publicarJson(TOPICO_TELEMETRIA, doc);
}

// Evento (Contrato, seção 6.2). Todos os eventos do mesmo impacto
// repetem os dados guardados em "impacto"; só o tipo e o ts mudam.
void publicarEvento(const char* tipo, const char* ts) {
  JsonDocument doc;
  doc["dispositivo"] = CODIGO_DISPOSITIVO;
  doc["ts"] = ts;
  doc["tipo"] = tipo;
  doc["picoG"] = round(impacto.picoG * 100.0) / 100.0;   // duas casas: 2.69
  doc["orientacaoAnormal"] = impacto.orientacaoAnormal;
  doc["pilotandoAntes"] = impacto.pilotandoAntes;
  doc["presencaAntes"] = impacto.presencaAntes;
  doc["latitude"] = LATITUDE_SIMULADA;
  doc["longitude"] = LONGITUDE_SIMULADA;

  publicarJson(TOPICO_EVENTO, doc);
}

// Evento com o ts do momento atual.
void publicarEventoAgora(const char* tipo) {
  char ts[25];
  horaUtcIso(ts, sizeof(ts));
  publicarEvento(tipo, ts);
}

// =====================================================================
// Transições da máquina de estados
// =====================================================================
void mudarEstado(EstadoDispositivo novo) {
  Serial.printf("[ESTADO] %s -> %s\n", nomeEstado(estado), nomeEstado(novo));
  estado = novo;
  estadoDesde = millis();
  ultimoSegundoBipado = -1;
}

void mostrarMensagem(const char* texto) {
  strncpy(mensagem, texto, sizeof(mensagem) - 1);
  mensagem[sizeof(mensagem) - 1] = '\0';
  mensagemAte = millis() + TEMPO_MENSAGEM_MS;
}

// Volta a proteger o turno. Usada no ARMAR, no fim do CANCELADO e do
// FALSO_POSITIVO, no ENCERRAR e no impacto ignorado.
void voltarParaArmado() {
  mudarEstado(ARMADO);
  // O histórico recomeça: amostras de antes do impacto anterior não
  // podem contar como "antes" de um impacto novo.
  limparHistorico();
  socorroACaminho = false;
  velocidadeAltaDesde = 0;
  noTone(PINO_BUZZER);

  // Se o MPU ainda estiver acima do limiar (os controles do Wokwi ficaram
  // na posição do impacto), só detectamos de novo depois que ele baixar.
  acimaDoLimiarAntes = acelTotalG >= LIMIAR_IMPACTO_G;

  // Telemetria imediata: a API fica sabendo na hora que o capacete
  // voltou a proteger, sem esperar os 10 s.
  publicarTelemetria();
  ultimaTelemetria = millis();
}

// T0: a aceleração total passou do limiar.
void iniciarImpacto() {
  impacto.picoG = acelTotalG;
  impacto.orientacaoAnormal = orientacaoAnormal;
  // Os dois sinais olham só o histórico, ou seja, o que aconteceu ANTES.
  impacto.pilotandoAntes = pilotavaAntes();
  impacto.presencaAntes = estavaComCapaceteAntes();
  horaUtcIso(impacto.tsImpacto, sizeof(impacto.tsImpacto));

  Serial.printf("[IMPACTO] %.2f g | pilotava antes: %s | capacete antes: %s\n",
                acelTotalG, impacto.pilotandoAntes ? "SIM" : "NAO",
                impacto.presencaAntes ? "SIM" : "NAO");

  tone(PINO_BUZZER, 1500, 300);
  mudarEstado(IMPACTO_DETECTADO);
}

// Fim da janela de 1 s do pico: publica o impacto e decide o caminho.
void concluirImpacto() {
  // O ts do IMPACTO_DETECTADO é o T0, o instante em que o limiar foi
  // cruzado, e não o fim da janela de medição.
  publicarEvento("IMPACTO_DETECTADO", impacto.tsImpacto);

  if (!impacto.pilotandoAntes && !impacto.presencaAntes) {
    // Capacete caiu do armário ou do retrovisor: ninguém em risco.
    Serial.println("[IMPACTO] Ignorado: nao pilotava e nao estava com o capacete");
    mostrarMensagem("Impacto ignorado");
    voltarParaArmado();
  } else {
    velocidadeAltaDesde = 0;
    mudarEstado(OBSERVACAO);
  }
}

// Botão do capacete ou comando CANCELAR durante a contagem.
void cancelarAlerta(const char* origem) {
  Serial.printf("[CONTAGEM] Cancelada pelo %s\n", origem);
  publicarEventoAgora("CANCELADO");
  tone(PINO_BUZZER, 800, 300);
  mudarEstado(CANCELADO);
}

// Fim da contagem sem cancelamento.
void enviarSos() {
  horaUtcIso(tsSos, sizeof(tsSos));   // este ts se repete em todo reenvio
  publicarEvento("SOS", tsSos);
  ultimoReenvioSos = millis();
  socorroACaminho = false;
  mudarEstado(SOS_ENVIADO);
}

// =====================================================================
// MQTT: comandos recebidos (Contrato, seção 6.3)
// =====================================================================
void ignorarComando(const char* tipo) {
  Serial.printf("[COMANDO] %s ignorado no estado %s\n", tipo, nomeEstado(estado));
}

void tratarComando(const char* tipo) {
  Serial.printf("[COMANDO] Recebido: %s (estado %s)\n", tipo, nomeEstado(estado));

  if (strcmp(tipo, "ARMAR") == 0) {
    if (estado == DESARMADO) {
      voltarParaArmado();
    } else {
      ignorarComando(tipo);
    }

  } else if (strcmp(tipo, "DESARMAR") == 0) {
    // Só vale em ARMADO: não se desliga a proteção no meio de uma possível queda.
    if (estado == ARMADO) {
      mudarEstado(DESARMADO);
    } else {
      ignorarComando(tipo);
    }

  } else if (strcmp(tipo, "CANCELAR") == 0) {
    if (estado == CONTAGEM) {
      cancelarAlerta("comando CANCELAR (app)");
    } else {
      ignorarComando(tipo);
    }

  } else if (strcmp(tipo, "SOCORRO_A_CAMINHO") == 0) {
    if (estado == SOS_ENVIADO) {
      socorroACaminho = true;   // para o reenvio e o alarme
      noTone(PINO_BUZZER);
      Serial.println("[SOS] Socorro a caminho: reenvio encerrado");
    } else {
      ignorarComando(tipo);
    }

  } else if (strcmp(tipo, "ENCERRAR") == 0) {
    if (estado == SOS_ENVIADO) {
      Serial.println("[SOS] Ocorrencia encerrada: o turno continua");
      voltarParaArmado();
    } else {
      ignorarComando(tipo);
    }

  } else {
    Serial.printf("[COMANDO] Tipo desconhecido: %s\n", tipo);
  }
}

// Chamada pela PubSubClient, dentro do mqtt.loop(), a cada mensagem recebida.
void aoReceberMensagem(char* topico, byte* payload, unsigned int tamanho) {
  // O cast para "const" faz o ArduinoJson COPIAR os textos. Sem ele, os
  // textos apontariam para o buffer da PubSubClient, que é sobrescrito
  // quando o próprio comando publica algo (por exemplo, no ARMAR).
  JsonDocument doc;
  DeserializationError erro = deserializeJson(doc, (const byte*)payload, tamanho);
  if (erro) {
    Serial.printf("[COMANDO] JSON invalido: %s\n", erro.c_str());
    return;
  }

  // Copia o tipo para um texto local. O "| \"\"" devolve texto vazio
  // se o campo "tipo" não existir.
  char tipo[24];
  strlcpy(tipo, doc["tipo"] | "", sizeof(tipo));
  tratarComando(tipo);
}

// =====================================================================
// Rede: WiFi, NTP e MQTT
// =====================================================================
void conectarWiFi() {
  Serial.print("Conectando ao WiFi");
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_SENHA, WIFI_CANAL);

  unsigned long inicio = millis();
  while (WiFi.status() != WL_CONNECTED && millis() - inicio < 15000) {
    delay(250);   // só no setup, antes de o capacete começar a proteger
    Serial.print(".");
  }
  Serial.println();
  Serial.println(WiFi.status() == WL_CONNECTED ? "WiFi conectado" : "WiFi ainda nao conectou (tento de novo no loop)");
}

void sincronizarRelogio() {
  // Fuso 0 e sem horário de verão: o Contrato pede UTC.
  configTime(0, 0, NTP_SERVIDOR_1, NTP_SERVIDOR_2);

  Serial.print("Sincronizando a hora (NTP)");
  unsigned long inicio = millis();
  while (!relogioSincronizado() && millis() - inicio < 15000) {
    delay(250);
    Serial.print(".");
  }
  Serial.println();

  if (relogioSincronizado()) {
    char ts[25];
    horaUtcIso(ts, sizeof(ts));
    Serial.printf("Hora sincronizada: %s\n", ts);
  } else {
    // O ESP32 continua tentando sozinho; até lá, nada é publicado.
    Serial.println("NTP ainda sem resposta: os eventos esperam a hora");
  }
}

// Uma tentativa de conexão. Chamada no loop a cada 5 s enquanto
// estiver desconectado, sem travar a detecção de queda.
void conectarMqtt() {
  if (WiFi.status() != WL_CONNECTED) return;

  Serial.printf("Conectando ao MQTT %s como %s...\n", MQTT_BROKER, MQTT_CLIENT_ID);

  // O keepalive que vai no CONNECT é o de 120 s: é ele que o broker usa
  // para decidir quando derrubar o capacete (ver config.h).
  mqtt.setKeepAlive(MQTT_KEEPALIVE_BROKER_S);

  // Parâmetros: clientId, usuário, senha, tópico, QoS e retain do "last
  // will" (não usamos), mensagem do last will e cleanSession = false
  // (sessão persistente: o broker guarda os comandos QoS 1).
  bool ok = mqtt.connect(MQTT_CLIENT_ID, nullptr, nullptr, nullptr, 0, false, nullptr, false);

  if (ok) {
    // Já conectado, o capacete passa a mandar ping a cada 15 s do seu
    // relógio. O broker continua com os 120 s combinados no CONNECT.
    mqtt.setKeepAlive(MQTT_PING_S);
    mqtt.subscribe(TOPICO_COMANDO, QOS_COMANDOS);
    Serial.printf("MQTT conectado. Assinando %s (QoS %d)\n", TOPICO_COMANDO, QOS_COMANDOS);
  } else {
    // Códigos da PubSubClient: -2 rede indisponível, -4 tempo esgotado, etc.
    Serial.printf("Falha no MQTT (codigo %d). Nova tentativa em %lu s\n",
                  mqtt.state(), INTERVALO_RECONEXAO_MS / 1000);
  }
}

// =====================================================================
// Máquina de estados: o que cada estado faz a cada volta do loop
// =====================================================================
void atualizarMaquina(bool botaoAcabouDeApertar) {
  unsigned long noEstado = millis() - estadoDesde;

  switch (estado) {

    case DESARMADO:
      // Proteção desligada: nada a fazer.
      break;

    case ARMADO: {
      // Detecta só a SUBIDA: estava abaixo do limiar e agora está acima.
      bool acima = acelTotalG >= LIMIAR_IMPACTO_G;
      if (acima && !acimaDoLimiarAntes) iniciarImpacto();
      acimaDoLimiarAntes = acima;
      break;
    }

    case IMPACTO_DETECTADO:
      // Durante 1 s, guarda o maior valor e se houve orientação anormal.
      impacto.picoG = max(impacto.picoG, acelTotalG);
      if (orientacaoAnormal) impacto.orientacaoAnormal = true;
      if (noEstado >= JANELA_PICO_MS) concluirImpacto();
      break;

    case OBSERVACAO:
      // Velocidade de moto por 3 s seguidos: a pessoa voltou a pilotar.
      if (velocidadeKmh >= VELOCIDADE_PILOTAGEM_KMH) {
        if (velocidadeAltaDesde == 0) velocidadeAltaDesde = millis();
        if (millis() - velocidadeAltaDesde >= VELOCIDADE_SUSTENTADA_S * 1000UL) {
          Serial.println("[OBSERVACAO] Velocidade de moto voltou: falso positivo");
          publicarEventoAgora("FALSO_POSITIVO");
          tone(PINO_BUZZER, 800, 300);
          mudarEstado(FALSO_POSITIVO);
          break;
        }
      } else {
        velocidadeAltaDesde = 0;
      }

      // Passaram os 10 s e a pessoa continua parada: começa a contagem.
      if (noEstado >= JANELA_OBSERVACAO_S * 1000UL) {
        mudarEstado(CONTAGEM);
        publicarEventoAgora("CONTAGEM_INICIADA");
      }
      break;

    case CONTAGEM:
      if (botaoAcabouDeApertar) {
        cancelarAlerta("botao do capacete");
      } else if (noEstado >= CONTAGEM_CANCELAMENTO_S * 1000UL) {
        enviarSos();
      }
      break;

    case SOS_ENVIADO:
      // QoS 0 pode perder a mensagem, então o SOS é reenviado com o
      // MESMO ts até a API confirmar (a API trata como um só).
      if (!socorroACaminho && millis() - ultimoReenvioSos >= INTERVALO_TELEMETRIA_S * 1000UL) {
        Serial.println("[SOS] Reenvio (mesmo ts)");
        publicarEvento("SOS", tsSos);
        ultimoReenvioSos = millis();
      }
      break;

    case CANCELADO:
    case FALSO_POSITIVO:
      // Mostra o resultado por 3 s e volta a proteger o turno.
      if (noEstado >= TEMPO_MENSAGEM_MS) voltarParaArmado();
      break;
  }

  // Fora da contagem, o botão não faz nada (depois do SOS, quem cancela
  // é o app, pela ocorrência).
  if (botaoAcabouDeApertar && estado != CANCELADO) {
    Serial.printf("[BOTAO] Pressionado no estado %s\n", nomeEstado(estado));
  }
}

// =====================================================================
// LED e buzzer de cada estado
// =====================================================================
void atualizarAtuadores() {
  unsigned long agora = millis();
  unsigned long noEstado = agora - estadoDesde;
  bool ledLigado = false;

  switch (estado) {
    case DESARMADO:
      ledLigado = false;
      break;

    case ARMADO:
      // Piscada curta a cada 2 s: "estou protegendo".
      ledLigado = (agora % 2000) < 100;
      break;

    case IMPACTO_DETECTADO:
    case OBSERVACAO:
      ledLigado = (agora % 1000) < 500;
      break;

    case CONTAGEM: {
      // Pisca rápido e bipa uma vez por segundo.
      ledLigado = (agora % 250) < 125;
      long segundo = noEstado / 1000;
      if (segundo != ultimoSegundoBipado) {
        tone(PINO_BUZZER, 2000, 120);
        ultimoSegundoBipado = segundo;
      }
      break;
    }

    case SOS_ENVIADO: {
      // LED aceso fixo; alarme a cada segundo até o socorro estar a caminho.
      ledLigado = true;
      if (!socorroACaminho) {
        long segundo = noEstado / 1000;
        if (segundo != ultimoSegundoBipado) {
          tone(PINO_BUZZER, 3000, 300);
          ultimoSegundoBipado = segundo;
        }
      }
      break;
    }

    case CANCELADO:
    case FALSO_POSITIVO:
      ledLigado = false;
      break;
  }

  digitalWrite(PINO_LED, ledLigado ? HIGH : LOW);
}

// =====================================================================
// OLED (sem acentos: a fonte do display não tem)
// =====================================================================
int segundosRestantes(unsigned long totalMs) {
  unsigned long noEstado = millis() - estadoDesde;
  if (noEstado >= totalMs) return 0;
  return (int)((totalMs - noEstado + 999) / 1000);   // arredonda para cima
}

void atualizarTela() {
  if (!oledOk) return;

  tela.clearDisplay();
  tela.setTextSize(1);                 // 21 colunas x 8 linhas
  tela.setTextColor(SSD1306_WHITE);
  tela.setCursor(0, 0);

  // Linha 0: código e situação da rede. Linha 1: estado do Contrato.
  bool wifiOk = WiFi.status() == WL_CONNECTED;
  tela.printf("%s W:%s M:%s\n", CODIGO_DISPOSITIVO, wifiOk ? "ok" : "--", mqtt.connected() ? "ok" : "--");
  tela.printf("%s\n", nomeEstado(estado));

  switch (estado) {
    case DESARMADO:
    case ARMADO: {
      tela.printf("Vel:%2d km/h Cab:%s\n", velocidadeKmh, presenca ? "SIM" : "NAO");
      if (isnan(temperaturaC)) {
        tela.println("T:--.-C U:--%");
      } else {
        tela.printf("T:%.1fC U:%.0f%% IC:%.0f\n", temperaturaC, umidadePct, indiceCalorC);
      }
      tela.printf("Acel:%.2fg Inc:%3.0f\n", acelTotalG, inclinacaoGraus);
      tela.println(estado == ARMADO ? "Protegendo o turno" : "Ligue a protecao");

      // Aviso temporário tem prioridade; senão, o aviso de calor.
      bool calor = !isnan(indiceCalorC) && indiceCalorC >= ALERTA_CALOR_C;
      if (millis() < mensagemAte) {
        tela.println(mensagem);
      } else if (calor) {
        tela.println("CALOR! Pausa e agua");
      } else {
        tela.println();
      }

      if (relogioSincronizado()) {
        char ts[25];
        horaUtcIso(ts, sizeof(ts));
        tela.printf("UTC %.8s", ts + 11);   // só o HH:MM:SS
      } else {
        tela.print("Aguardando hora NTP");
      }
      break;
    }

    case IMPACTO_DETECTADO:
      tela.printf("Impacto! %.2f g\n", impacto.picoG);
      tela.println("Medindo o pico...");
      tela.printf("Pilotava: %s\n", impacto.pilotandoAntes ? "SIM" : "NAO");
      tela.printf("Cabeca:   %s\n", impacto.presencaAntes ? "SIM" : "NAO");
      break;

    case OBSERVACAO: {
      bool imovel = giroGrausS < GIRO_IMOVEL_GRAUS_S;
      tela.printf("Pico: %.2f g\n", impacto.picoG);
      tela.printf("Observando: %2d s\n", segundosRestantes(JANELA_OBSERVACAO_S * 1000UL));
      tela.printf("Vel:%2d km/h\n", velocidadeKmh);
      tela.printf("Movimento: %s\n", imovel ? "imovel" : "sim");
      tela.printf("Inclinacao: %3.0f", inclinacaoGraus);
      break;
    }

    case CONTAGEM:
      tela.setTextSize(2);                       // 10 colunas
      tela.setCursor(0, 18);
      tela.printf("SOS em %2d", segundosRestantes(CONTAGEM_CANCELAMENTO_S * 1000UL));
      tela.setTextSize(1);
      tela.setCursor(0, 40);
      tela.println("Esta bem? Aperte");
      tela.println("CANCELAR no capacete");
      tela.print("ou cancele no app");
      break;

    case SOS_ENVIADO:
      tela.setTextSize(2);
      tela.setCursor(0, 18);
      tela.print("SOS ATIVO");
      tela.setTextSize(1);
      tela.setCursor(0, 40);
      tela.println("Ligue 192 (SAMU)");
      if (socorroACaminho) {
        tela.println("Socorro a caminho!");
      } else {
        tela.println("Avisando a rede...");
        tela.print("Reenvio a cada 10 s");
      }
      break;

    case CANCELADO:
      tela.println();
      tela.println("Alerta cancelado.");
      tela.println("Que bom que esta bem!");
      tela.println();
      tela.println("Voltando a proteger");
      break;

    case FALSO_POSITIVO:
      tela.println();
      tela.println("Falso positivo:");
      tela.println("voce voltou a andar.");
      tela.println();
      tela.println("Voltando a proteger");
      break;
  }

  // Desenhar na memória é rápido; caro é mandar o quadro pelo I2C.
  // Por isso só chamamos display() quando a tela mudou de verdade.
  // Na contagem, isso acontece uma vez por segundo (o número muda).
  uint8_t* quadro = tela.getBuffer();
  if (!temQuadroAnterior || memcmp(quadro, ultimoQuadro, TAMANHO_QUADRO) != 0) {
    tela.display();
    memcpy(ultimoQuadro, quadro, TAMANHO_QUADRO);
    temQuadroAnterior = true;
    redesenhosOled++;
  }
}

// Uma linha de status a cada 5 s, para acompanhar pelo Serial.
void imprimirStatus() {
  Serial.printf("[STATUS] %s | vel %d km/h | cabeca %s | acel %.2f g | inc %.0f | IC %.1f C | WiFi %s | MQTT %s | OLED %d quadros/5 s\n",
                nomeEstado(estado), velocidadeKmh, presenca ? "SIM" : "NAO",
                acelTotalG, inclinacaoGraus, indiceCalorC,
                WiFi.status() == WL_CONNECTED ? "ok" : "--",
                mqtt.connected() ? "ok" : "--", redesenhosOled);
  redesenhosOled = 0;
}

// =====================================================================
// setup e loop
// =====================================================================
void setup() {
  Serial.begin(115200);
  Serial.println();
  Serial.println("HelmCare: etapa 2, deteccao de queda e MQTT");

  pinMode(PINO_PRESENCA, INPUT_PULLUP);
  pinMode(PINO_BOTAO, INPUT_PULLUP);
  pinMode(PINO_LED, OUTPUT);
  pinMode(PINO_BUZZER, OUTPUT);
  digitalWrite(PINO_LED, LOW);

  Wire.begin(PINO_I2C_SDA, PINO_I2C_SCL);
  Wire.setClock(I2C_FREQUENCIA_HZ);

  // OLED
  oledOk = tela.begin(SSD1306_SWITCHCAPVCC, ENDERECO_OLED);
  if (!oledOk) {
    Serial.println("Erro: OLED SSD1306 nao encontrado (confira SDA 21, SCL 22)");
  } else {
    tela.clearDisplay();
    tela.setTextSize(1);
    tela.setTextColor(SSD1306_WHITE);
    tela.setCursor(0, 0);
    tela.println("HelmCare");
    tela.println("Conectando WiFi,");
    tela.println("hora (NTP) e MQTT...");
    tela.display();
  }

  // MPU6050 na faixa de 16 g (uma queda real passa de 8 g).
  mpuOk = mpu.begin();
  if (!mpuOk) {
    Serial.println("Erro: MPU6050 nao encontrado (confira SDA 21, SCL 22)");
  } else {
    mpu.setAccelerometerRange(MPU6050_RANGE_16_G);
    mpu.setGyroRange(MPU6050_RANGE_500_DEG);
    mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);
  }

  dht.begin();

  // Rede: WiFi, hora e configuração do MQTT.
  conectarWiFi();
  sincronizarRelogio();
  mqtt.setServer(MQTT_BROKER, MQTT_PORTA);
  mqtt.setCallback(aoReceberMensagem);
  mqtt.setBufferSize(MQTT_BUFFER);
  mqtt.setSocketTimeout(10);  // espera de até 10 s pela resposta do broker (simulação lenta)
  conectarMqtt();
  ultimaTentativaMqtt = millis();

  // Primeiras leituras.
  lerMpu();
  lerVelocidade();
  lerPresenca();
  // O DHT22 só é lido no loop: logo depois do begin() ele ainda devolve NAN.

  estadoDesde = millis();
  Serial.println("Capacete DESARMADO. Aguardando o comando ARMAR.");
}

void loop() {
  unsigned long agora = millis();

  // 1. Rede: reconecta sem travar e processa os comandos recebidos.
  if (!mqtt.connected() && agora - ultimaTentativaMqtt >= INTERVALO_RECONEXAO_MS) {
    ultimaTentativaMqtt = agora;
    conectarMqtt();
  }
  mqtt.loop();

  // 2. Sensores, cada um no seu ritmo.
  if (agora - ultimoMpu >= INTERVALO_MPU_MS) {
    ultimoMpu = agora;
    lerMpu();
  }
  if (agora - ultimoDht >= INTERVALO_DHT_MS) {
    ultimoDht = agora;
    lerDht();
  }
  lerVelocidade();
  lerPresenca();
  bool botaoAcabouDeApertar = lerBotao();

  // 3. Histórico dos 5 s anteriores (só interessa com o capacete armado).
  if (estado == ARMADO && agora - ultimaAmostraHistorico >= INTERVALO_HISTORICO_MS) {
    ultimaAmostraHistorico = agora;
    guardarAmostraHistorico();
  }

  // 4. Máquina de estados, LED e buzzer.
  atualizarMaquina(botaoAcabouDeApertar);
  atualizarAtuadores();

  // 5. Telemetria a cada 10 s em todo estado diferente de DESARMADO.
  if (estado != DESARMADO && millis() - ultimaTelemetria >= INTERVALO_TELEMETRIA_S * 1000UL) {
    ultimaTelemetria = millis();
    publicarTelemetria();
  }

  // 6. Saídas para quem está olhando.
  if (agora - ultimaTela >= INTERVALO_TELA_MS) {
    ultimaTela = agora;
    atualizarTela();
  }
  if (agora - ultimoStatus >= INTERVALO_STATUS_MS) {
    ultimoStatus = agora;
    imprimirStatus();
  }

  // 7. Pausa curta para o processador descansar (ver config.h).
  if (PAUSA_LOOP_MS > 0) delay(PAUSA_LOOP_MS);
}
