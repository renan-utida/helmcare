// =====================================================================
// HelmCare - Módulo do capacete (ESP32 no Wokwi)
// Renan Dias Utida - RM 558540
// config.h: pinos e constantes num só lugar.
//
// As constantes da seção "CONTRATO" seguem o Contrato de Integração
// v1.6 (seções 5 e 6). Só mude esses valores junto com o Contrato.
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
// Com INPUT_PULLUP: LOW = chave do lado do fio preto (GND) = capacete na cabeça.
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

// Rede (Wokwi)
// ---------------------------------------------------------------------
// No Wokwi a rede é sempre a Wokwi-GUEST, sem senha. O canal 6 é o
// mesmo usado no CP6 e acelera a conexão.
const char* const WIFI_SSID = "Wokwi-GUEST";
const char* const WIFI_SENHA = "";
constexpr int WIFI_CANAL = 6;

// Servidores de hora (NTP). O "ts" dos payloads é UTC, então o fuso é 0.
const char* const NTP_SERVIDOR_1 = "pool.ntp.org";
const char* const NTP_SERVIDOR_2 = "time.google.com";

// CONTRATO: MQTT (Contrato, seção 6)
// ---------------------------------------------------------------------
// O código do capacete é uma macro para poder ser "colado" nos tópicos
// em tempo de compilação ("helmcare-rdu/v1/" "HC-0001" "/evento").
#define CODIGO_HC "HC-0001"
#define RAIZ_TOPICOS "helmcare-rdu/v1/" CODIGO_HC

const char* const CODIGO_DISPOSITIVO = CODIGO_HC;
const char* const MQTT_BROKER = "broker.hivemq.com";
constexpr int MQTT_PORTA = 1883;

// clientId fixo + sessão persistente (cleanSession = false): os comandos
// em QoS 1 ficam guardados no broker se o capacete cair por alguns
// segundos. ATENÇÃO: nunca rode duas simulações ao mesmo tempo, porque
// dois clientes com o mesmo clientId se derrubam no broker.
const char* const MQTT_CLIENT_ID = "helmcare-rdu-" CODIGO_HC;

const char* const TOPICO_TELEMETRIA = RAIZ_TOPICOS "/telemetria";
const char* const TOPICO_EVENTO     = RAIZ_TOPICOS "/evento";
const char* const TOPICO_COMANDO    = RAIZ_TOPICOS "/comando";

// Comandos assinados em QoS 1. A publicação sai em QoS 0 (limite da
// PubSubClient), por isso o SOS é reenviado até a confirmação.
constexpr uint8_t QOS_COMANDOS = 1;

// Keepalive em dois valores, por causa da simulação lenta do Wokwi
// (o ESP32 roda a cerca de 40% da velocidade real):
// - 120 s vai no CONNECT: o broker só derruba o capacete depois de
//   1,5 x 120 = 180 s REAIS sem nenhum pacote.
// - 15 s fica no capacete depois de conectar: ele manda um ping a cada
//   15 s do SEU relógio (cerca de 40 s reais), bem antes dos 180 s.
// Mandar pings mais cedo do que o keepalive combinado é permitido no MQTT.
constexpr uint16_t MQTT_KEEPALIVE_BROKER_S = 120;
constexpr uint16_t MQTT_PING_S = 15;

// Telemetria e evento ficam abaixo de 300 bytes; 512 dá folga.
// Sem isso, a PubSubClient (256 bytes por padrão) falha sem aviso.
constexpr int MQTT_BUFFER = 512;

// CONTRATO: constantes compartilhadas (Contrato, seção 5)
// ---------------------------------------------------------------------
constexpr int JANELA_OBSERVACAO_S = 10;
constexpr int CONTAGEM_CANCELAMENTO_S = 15;
constexpr float LIMIAR_IMPACTO_G = 2.5f;          // maior ou igual; calibrado no Wokwi (eixos até ±2 g)
constexpr int VELOCIDADE_PILOTAGEM_KMH = 15;      // maior ou igual
constexpr int INTERVALO_TELEMETRIA_S = 10;        // telemetria e reenvio do SOS
constexpr float ALERTA_CALOR_C = 32.0f;           // maior ou igual

// Coordenadas simuladas do capacete (região da Av. Paulista).
constexpr double LATITUDE_SIMULADA = -23.5641;
constexpr double LONGITUDE_SIMULADA = -46.6525;

// SÓ DO CAPACETE: não trafegam no MQTT, não mudam o Contrato
// ---------------------------------------------------------------------
// Quantos segundos antes do impacto contam como "antes" para
// pilotandoAntes e presencaAntes.
constexpr int JANELA_PRE_IMPACTO_S = 5;

// Depois de passar do limiar, o capacete fica 1 s em IMPACTO_DETECTADO
// medindo o maior valor (picoG) e a inclinação antes de decidir.
constexpr unsigned long JANELA_PICO_MS = 1000;

// Na observação, a velocidade de moto só conta como "voltou" se ficar
// maior ou igual a 15 km/h por 3 s seguidos. Isso imita o atraso do GPS
// e evita que um pico solto vire falso positivo.
constexpr int VELOCIDADE_SUSTENTADA_S = 3;

// Escala do potenciômetro: 0 a 4095 vira 0 a 60 km/h.
constexpr int VELOCIDADE_MAX_SIM_KMH = 60;

// Acima desta inclinação (em graus, em relação à posição de pé),
// a orientação do capacete é considerada anormal (informativo).
constexpr float INCLINACAO_ANORMAL_GRAUS = 60.0f;

// Abaixo desta rotação, o OLED mostra "imovel" na observação (informativo).
constexpr float GIRO_IMOVEL_GRAUS_S = 10.0f;

// Tempo que CANCELADO, FALSO_POSITIVO e os avisos ficam na tela
// antes de o capacete voltar para ARMADO.
constexpr unsigned long TEMPO_MENSAGEM_MS = 3000;

// Ritmo das tarefas (em milissegundos)
// ---------------------------------------------------------------------
constexpr unsigned long INTERVALO_MPU_MS = 50;         // rápido para não perder o pico
constexpr unsigned long INTERVALO_DHT_MS = 2000;       // o DHT22 aceita 1 leitura a cada 2 s
constexpr unsigned long INTERVALO_HISTORICO_MS = 250;  // amostra de velocidade e presença
constexpr unsigned long INTERVALO_TELA_MS = 250;
constexpr unsigned long INTERVALO_STATUS_MS = 5000;    // linha de status no Serial
constexpr unsigned long INTERVALO_RECONEXAO_MS = 5000; // nova tentativa de MQTT
constexpr unsigned long DEBOUNCE_MS = 50;

// Desempenho no Wokwi
// ---------------------------------------------------------------------
// Velocidade do barramento I2C (MPU6050 e OLED aceitam 400 kHz).
// Mais rápido = menos tempo do processador esperando o barramento.
constexpr uint32_t I2C_FREQUENCIA_HZ = 400000;

// Pausa curta no fim de cada volta do loop(). Sem ela, o loop() gira
// sem parar e o Wokwi precisa simular cada instrução; com ela, o ESP32
// "descansa" e o Wokwi avança o tempo mais rápido. 5 ms não atrapalha
// nada: a tarefa mais rápida (ler o MPU6050) roda a cada 50 ms.
// Para comparar sem a pausa, troque para 0.
constexpr unsigned long PAUSA_LOOP_MS = 5;
