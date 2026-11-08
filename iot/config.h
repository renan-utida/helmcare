// =====================================================================
// HelmCare - Módulo do capacete (ESP32 no Wokwi)
// Renan Dias Utida - RM 558540
// config.h: pinos e constantes num só lugar.
//
// As constantes da seção "CONTRATO" seguem o Contrato de Integração
// (seções 5 e 6). Só mude esses valores junto com o Contrato.
// As da seção "SÓ DO CAPACETE" valem apenas aqui e podem ser ajustadas.
// =====================================================================
#pragma once

// Pinos (plano do circuito aprovado)
// ---------------------------------------------------------------------
// I2C compartilhado: o MPU6050 (endereço 0x68) e o OLED (0x3C)
// ficam no mesmo barramento, cada um com o seu endereço.
constexpr int PINO_I2C_SDA = 21;
constexpr int PINO_I2C_SCL = 22;

// DHT22 no mesmo pino usado na aula de MQTT do Prof. Yan.
constexpr int PINO_DHT = 15;

// Potenciômetro da velocidade simulada. Usa o ADC1 (GPIO 32 a 39),
// porque o ADC2 para de funcionar quando o WiFi está ligado.
constexpr int PINO_VELOCIDADE = 34;

// Slide switch que simula o sensor IR de presença na espuma.
// Com INPUT_PULLUP: LOW = chave ligada ao GND = capacete na cabeça.
constexpr int PINO_PRESENCA = 27;

// Botão de cancelar. Com INPUT_PULLUP: LOW = pressionado.
constexpr int PINO_BOTAO = 26;

// Atuadores.
constexpr int PINO_BUZZER = 25;
constexpr int PINO_LED = 4;      // evita o GPIO 2, ligado ao LED da placa

// Endereços I2C
// ---------------------------------------------------------------------
constexpr uint8_t ENDERECO_OLED = 0x3C;
constexpr int OLED_LARGURA = 128;
constexpr int OLED_ALTURA = 64;

// CONTRATO: constantes compartilhadas (Contrato, seção 5)
// ---------------------------------------------------------------------
const char* const CODIGO_DISPOSITIVO = "HC-0001";

constexpr int JANELA_OBSERVACAO_S = 10;
constexpr int CONTAGEM_CANCELAMENTO_S = 15;
constexpr float LIMIAR_IMPACTO_G = 2.5f;          // Contrato v1.2: calibrado no Wokwi (eixos limitados a 2 g)
constexpr int VELOCIDADE_PILOTAGEM_KMH = 15;
constexpr int INTERVALO_TELEMETRIA_S = 10;

// Coordenadas simuladas do capacete (região da Av. Paulista).
constexpr double LATITUDE_SIMULADA = -23.5641;
constexpr double LONGITUDE_SIMULADA = -46.6525;

// SÓ DO CAPACETE: não trafegam no MQTT, não mudam o Contrato
// ---------------------------------------------------------------------
// Quantos segundos antes do impacto contam como "antes" para
// pilotandoAntes e presencaAntes.
constexpr int JANELA_PRE_IMPACTO_S = 5;

// Escala do potenciômetro: 0 a 4095 vira 0 a 60 km/h.
constexpr int VELOCIDADE_MAX_SIM_KMH = 60;

// A partir deste índice de calor, o OLED mostra o aviso de calor.
constexpr float ALERTA_CALOR_C = 32.0f;

// Acima desta inclinação (em graus, em relação à posição de pé),
// a orientação do capacete é considerada anormal (informativo).
constexpr float INCLINACAO_ANORMAL_GRAUS = 60.0f;

// Ritmo das leituras (em milissegundos)
// ---------------------------------------------------------------------
constexpr unsigned long INTERVALO_MPU_MS = 50;      // rápido para não perder o pico
constexpr unsigned long INTERVALO_DHT_MS = 2000;    // o DHT22 aceita 1 leitura a cada 2 s
constexpr unsigned long INTERVALO_TELA_MS = 500;
constexpr unsigned long INTERVALO_SERIAL_MS = 1000;
constexpr unsigned long DEBOUNCE_MS = 50;
