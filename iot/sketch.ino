// =====================================================================
// HelmCare - Módulo do capacete (ESP32 no Wokwi)
// Renan Dias Utida - RM 558540 - 3ESPW - FIAP
//
// Etapa 1: circuito montado e leitura de todos os sensores.
//
// Nesta etapa o capacete ainda não detecta quedas nem usa a rede.
// Ele só lê os sensores, mostra tudo no OLED e no Serial e testa
// os atuadores pelo botão. A lógica de detecção e o MQTT entram na
// etapa 2 (branch feature/iot-deteccao-mqtt).
//
// Teste do MPU6050 (item E): o "pico máximo" guarda a maior
// aceleração total vista desde o último reset. Segure o botão para
// zerar o pico e ouvir o bipe de teste.
// =====================================================================

#include <Wire.h>
#include <Adafruit_Sensor.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <DHT.h>
#include "config.h"

// ---------------------------------------------------------------------
// Estados do capacete (EstadoDispositivo, Contrato seção 4).
// Nesta etapa o capacete fica sempre em DESARMADO; os outros estados
// já ficam declarados com os nomes do Contrato para a etapa 2.
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

// ---------------------------------------------------------------------
// Objetos dos sensores e do display
// ---------------------------------------------------------------------
Adafruit_MPU6050 mpu;
Adafruit_SSD1306 tela(OLED_LARGURA, OLED_ALTURA, &Wire, -1);  // -1: sem pino de reset
DHT dht(PINO_DHT, DHT22);

// ---------------------------------------------------------------------
// Últimas leituras (atualizadas no loop, sem delay)
// ---------------------------------------------------------------------
// MPU6050
float acelXg = 0, acelYg = 0, acelZg = 1;   // aceleração em g por eixo
float acelTotalG = 1;                        // módulo do vetor (em repouso, 1 g)
float inclinacaoGraus = 0;                   // 0 = capacete de pé
float giroGrausS = 0;                        // rotação total em graus por segundo
bool orientacaoAnormal = false;

// Maiores valores vistos (para o teste do item E)
float picoMaxG = 0;
float maxAbsX = 0, maxAbsY = 0, maxAbsZ = 0;

// DHT22
float temperaturaC = NAN;
float umidadePct = NAN;
float indiceCalorC = NAN;

// Potenciômetro, chave e botão
int velocidadeKmh = 0;
bool presenca = false;
bool botaoPressionado = false;

// Controle de tempo de cada tarefa (padrão millis, sem delay)
unsigned long ultimoMpu = 0;
unsigned long ultimoDht = 0;
unsigned long ultimaTela = 0;
unsigned long ultimoSerial = 0;

// Debounce do botão
bool leituraBotaoAnterior = HIGH;
unsigned long mudancaBotaoEm = 0;

bool oledOk = false;
bool mpuOk = false;

// ---------------------------------------------------------------------
// Leituras
// ---------------------------------------------------------------------
void lerMpu() {
  if (!mpuOk) return;

  sensors_event_t a, g, temp;
  mpu.getEvent(&a, &g, &temp);

  // A biblioteca devolve m/s². Dividindo pela gravidade padrão,
  // ficamos em g, a mesma unidade do LIMIAR_IMPACTO_G e do Wokwi.
  acelXg = a.acceleration.x / SENSORS_GRAVITY_STANDARD;
  acelYg = a.acceleration.y / SENSORS_GRAVITY_STANDARD;
  acelZg = a.acceleration.z / SENSORS_GRAVITY_STANDARD;

  // Aceleração total: módulo do vetor (x, y, z).
  // É ela que será comparada com o LIMIAR_IMPACTO_G na etapa 2.
  acelTotalG = sqrt(acelXg * acelXg + acelYg * acelYg + acelZg * acelZg);

  // Inclinação: ângulo entre o vetor medido e o eixo Z.
  // Com o capacete de pé, a gravidade fica toda no Z e o ângulo é 0.
  if (acelTotalG > 0.1f) {
    float cosseno = constrain(acelZg / acelTotalG, -1.0f, 1.0f);
    inclinacaoGraus = degrees(acos(cosseno));
  }
  orientacaoAnormal = inclinacaoGraus > INCLINACAO_ANORMAL_GRAUS;

  // Rotação total, convertida de rad/s para graus/s.
  // Será usada para checar a imobilidade depois da queda.
  float gx = degrees(g.gyro.x), gy = degrees(g.gyro.y), gz = degrees(g.gyro.z);
  giroGrausS = sqrt(gx * gx + gy * gy + gz * gz);

  // Registro dos maiores valores para o teste de calibração.
  picoMaxG = max(picoMaxG, acelTotalG);
  maxAbsX = max(maxAbsX, fabsf(acelXg));
  maxAbsY = max(maxAbsY, fabsf(acelYg));
  maxAbsZ = max(maxAbsZ, fabsf(acelZg));
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
  // Índice de calor (sensação térmica), em Celsius (false = não Fahrenheit).
  indiceCalorC = dht.computeHeatIndex(t, h, false);
}

void lerVelocidade() {
  // O ADC do ESP32 vai de 0 a 4095. Convertemos para 0 a 60 km/h.
  int bruto = analogRead(PINO_VELOCIDADE);
  velocidadeKmh = (int)round(bruto * (float)VELOCIDADE_MAX_SIM_KMH / 4095.0f);
}

void lerPresenca() {
  // INPUT_PULLUP: a chave ligada ao GND dá LOW, ou seja, capacete vestido.
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

  // Só aceita a mudança se o sinal ficou estável por DEBOUNCE_MS.
  if (millis() - mudancaBotaoEm > DEBOUNCE_MS) {
    bool pressionadoAgora = (leitura == LOW);
    if (pressionadoAgora && !botaoPressionado) acabouDeApertar = true;
    botaoPressionado = pressionadoAgora;
  }
  return acabouDeApertar;
}

// ---------------------------------------------------------------------
// Atuadores (teste da etapa 1)
// ---------------------------------------------------------------------
void testarAtuadores(bool acabouDeApertar) {
  // Enquanto o botão está pressionado, o LED acende.
  digitalWrite(PINO_LED, botaoPressionado ? HIGH : LOW);

  // No instante do aperto: bipe curto e reset dos picos do item E.
  if (acabouDeApertar) {
    tone(PINO_BUZZER, 2000, 150);   // 2 kHz por 150 ms, sem travar o loop
    picoMaxG = acelTotalG;
    maxAbsX = fabsf(acelXg);
    maxAbsY = fabsf(acelYg);
    maxAbsZ = fabsf(acelZg);
    Serial.println(">> Botao: picos zerados");
  }
}

// ---------------------------------------------------------------------
// Saídas: OLED e Serial
// ---------------------------------------------------------------------
void atualizarTela() {
  if (!oledOk) return;

  tela.clearDisplay();
  tela.setTextSize(1);                 // 21 colunas x 8 linhas
  tela.setTextColor(SSD1306_WHITE);
  tela.setCursor(0, 0);

  // O OLED não tem acentos, por isso os textos ficam sem eles.
  tela.printf("HelmCare %s\n", CODIGO_DISPOSITIVO);
  tela.printf("Estado: %s\n", nomeEstado(estado));
  tela.printf("Vel:%2d km/h Cab:%s\n", velocidadeKmh, presenca ? "SIM" : "NAO");

  if (isnan(temperaturaC)) {
    tela.println("T:--.-C U:--%");
    tela.println("Ind.calor: --.-C");
  } else {
    tela.printf("T:%.1fC U:%.0f%%\n", temperaturaC, umidadePct);
    tela.printf("Ind.calor: %.1fC\n", indiceCalorC);
  }

  tela.printf("Acel:%.2fg Inc:%3.0f\n", acelTotalG, inclinacaoGraus);
  tela.printf("Pico max: %.2fg\n", picoMaxG);

  bool calor = !isnan(indiceCalorC) && indiceCalorC >= ALERTA_CALOR_C;
  tela.printf("Btn:%s %s", botaoPressionado ? "ON" : "--", calor ? "CALOR! Pausa" : "");

  tela.display();
}

void imprimirSerial() {
  Serial.println("--------------------------------------------------");
  Serial.printf("Estado: %s\n", nomeEstado(estado));
  Serial.printf("Velocidade: %d km/h | Presenca: %s | Botao: %s\n",
                velocidadeKmh, presenca ? "SIM" : "NAO",
                botaoPressionado ? "PRESSIONADO" : "solto");

  if (isnan(temperaturaC)) {
    Serial.println("DHT22: aguardando a primeira leitura");
  } else {
    Serial.printf("Temperatura: %.1f C | Umidade: %.0f %% | Indice de calor: %.1f C\n",
                  temperaturaC, umidadePct, indiceCalorC);
  }

  Serial.printf("Acel (g): X=%.2f Y=%.2f Z=%.2f | Total=%.2f g\n",
                acelXg, acelYg, acelZg, acelTotalG);
  Serial.printf("Inclinacao: %.0f graus (anormal: %s) | Giro: %.0f graus/s\n",
                inclinacaoGraus, orientacaoAnormal ? "SIM" : "NAO", giroGrausS);
  Serial.printf("Teste E | max por eixo: X=%.2f Y=%.2f Z=%.2f | pico total=%.2f g | limiar=%.1f g\n",
                maxAbsX, maxAbsY, maxAbsZ, picoMaxG, LIMIAR_IMPACTO_G);
}

// ---------------------------------------------------------------------
// setup e loop
// ---------------------------------------------------------------------
void setup() {
  Serial.begin(115200);
  Serial.println();
  Serial.println("HelmCare: etapa 1, leitura dos sensores");

  pinMode(PINO_PRESENCA, INPUT_PULLUP);
  pinMode(PINO_BOTAO, INPUT_PULLUP);
  pinMode(PINO_LED, OUTPUT);
  pinMode(PINO_BUZZER, OUTPUT);
  digitalWrite(PINO_LED, LOW);

  Wire.begin(PINO_I2C_SDA, PINO_I2C_SCL);

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
    tela.println("Iniciando sensores...");
    tela.display();
  }

  // MPU6050
  mpuOk = mpu.begin();   // endereço padrão 0x68
  if (!mpuOk) {
    Serial.println("Erro: MPU6050 nao encontrado (confira SDA 21, SCL 22)");
  } else {
    // Faixa de 16 g: uma queda real de moto passa de 8 g, e assim
    // também conseguimos medir até onde o Wokwi deixa a aceleração ir.
    mpu.setAccelerometerRange(MPU6050_RANGE_16_G);
    mpu.setGyroRange(MPU6050_RANGE_500_DEG);
    mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);
  }

  dht.begin();

  // Primeiras leituras para não começar com a tela vazia.
  lerMpu();
  lerVelocidade();
  lerPresenca();
  picoMaxG = acelTotalG;
}

void loop() {
  unsigned long agora = millis();

  // Cada tarefa roda no seu próprio ritmo, sem delay, para o loop
  // nunca travar (na etapa 2 o client.loop() do MQTT precisa disso).
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
  bool acabouDeApertar = lerBotao();
  testarAtuadores(acabouDeApertar);

  if (agora - ultimaTela >= INTERVALO_TELA_MS) {
    ultimaTela = agora;
    atualizarTela();
  }

  if (agora - ultimoSerial >= INTERVALO_SERIAL_MS) {
    ultimoSerial = agora;
    imprimirSerial();
  }
}
