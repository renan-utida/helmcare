# HelmCare - IoT - capacete inteligente (Physical Computing: IoT & IoB)

O módulo do capacete, simulado no Wokwi com ESP32. Detecta a queda, confirma que a pessoa estava pilotando ou com o capacete na cabeça, faz a janela de observação e a contagem de cancelamento, e publica os eventos via MQTT para a API.

> Status: em construção.

## Componentes

| Componente | Função |
|---|---|
| ESP32 | Microcontrolador do capacete |
| MPU6050 | Impacto, orientação anormal e imobilidade após a queda |
| Slide switch | Simula o sensor IR de presença na espuma do capacete |
| Potenciômetro | Simula a velocidade (representa o GPS do celular) |
| Botão | Cancela o alerta durante a contagem |
| Buzzer e LED | Sinalizam a contagem e o SOS |
| OLED SSD1306 | Mostra o status e a contagem regressiva |
| DHT22 | Temperatura e umidade para o alerta de calor |

## Entregáveis

- Link do projeto no Wokwi: *(a preencher)*
- Vídeo de até 4 minutos: *(a preencher)*
- [`integrantes.txt`](integrantes.txt)

## Link do projeto no Wokwi
*(a preencher)*

## Como funciona
*(a preencher)*

## Vídeo de demonstração (até 4 min)
*(a preencher)*

## Tópicos MQTT

Seção 6 do [contrato de integração](../docs/contrato-integracao.md).
