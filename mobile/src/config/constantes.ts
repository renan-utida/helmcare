/**
 * Constantes compartilhadas da seção 5 do contrato que o app usa,
 * mais as constantes próprias do app.
 */

/**
 * Segundos da janela de observação do capacete depois do impacto (seção 5). O app conta no
 * próprio relógio, a partir da primeira leitura em IMPACTO_DETECTADO ou OBSERVACAO, só para o anel.
 */
export const JANELA_OBSERVACAO_S = 10;

/** Segundos da contagem de cancelamento do alerta de queda, contados no relógio do app. */
export const CONTAGEM_CANCELAMENTO_S = 15;

/** Índice de calor (°C) a partir do qual o app sugere pausa (maior ou igual). */
export const ALERTA_CALOR_C = 32;

/** Intervalo do polling do app (chamados, status e alerta), em segundos. */
export const INTERVALO_POLLING_APP_S = 5;

/** Meta do tempo da queda até o aceite, em segundos. */
export const META_TEMPO_ACEITE_S = 120;

/** Raio da camada 1 do SOS, em km (inclusivo). */
export const RAIO_CAMADA_1_KM = 2;

/** Tempo da camada 1 antes de a ocorrência escalar para a camada 2, em segundos. */
export const TEMPO_ACEITE_CAMADA_1_S = 60;

/** Minutos desde a criação em que a API encerra sozinha uma ocorrência ainda aberta (seção 5). */
export const TEMPO_ENCERRAMENTO_AUTOMATICO_MIN = 120;

/** Código do capacete do protótipo no Wokwi, sugerido no cadastro. */
export const CODIGO_CAPACETE_PADRAO = 'HC-0001';

/** Tempo limite de cada requisição à API, em milissegundos (8 s). */
export const TEMPO_LIMITE_REQUISICAO_MS = 8000;

/**
 * Tempo limite só do `POST /turnos`, em milissegundos (15 s). No pior caso (Open-Meteo e Serviço
 * de Risco esgotando o tempo), a rota leva cerca de 6 s, e o contrato pede pelo menos 10 s.
 */
export const TEMPO_LIMITE_TURNO_MS = 15000;

/** Quantas contas logadas o aparelho guarda no `expo-secure-store` (troca rápida, sem senha). */
export const MAXIMO_CONTAS_APARELHO = 3;

/** Porta da API principal no computador de desenvolvimento (seção 1 do contrato). */
export const PORTA_API = 5000;

/** Caminho base das rotas da API principal. */
export const CAMINHO_API = '/api/v1';
