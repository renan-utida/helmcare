# HelmCare - API principal (C# Software Development)

O núcleo do sistema, em ASP.NET Core. Recebe os eventos do capacete via MQTT, orquestra o SOS escalonado, gerencia a rede de voluntários, mede o indicador de impacto e serve a página web do Cofre médico. Também funciona como porta de entrada do app, repassando as chamadas ao Serviço de Risco.

> Status: em construção.

## Tecnologias

ASP.NET Core - Entity Framework Core (com migrations) - LINQ - async/await - injeção de dependência - MSTest - Scalar/Swagger - PostgreSQL

## Como executar
*(a preencher)*

## Documentação dos endpoints
*(a preencher: link do Scalar/Swagger)*

## Evidências
*(a preencher)*

## Endpoints

Seção 7 do [contrato de integração](../docs/contrato-integracao.md). A documentação interativa fica em *(a preencher)*.

## Decisões de projeto

- O indicador de impacto mede cada trecho no próprio relógio: o capacete mede da queda ao fim da contagem, e o servidor mede do SOS recebido até o aceite.
- Assim, um relógio atrasado no capacete não infla o indicador; no teste de 12/11, o Wokwi rodou a 47% da velocidade e o relógio dele atrasou 1 min 55 s em cerca de 2 min.
