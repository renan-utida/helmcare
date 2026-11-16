/**
 * Datas do app. A API recebe e devolve ISO 8601 em UTC (seção 2 do contrato);
 * a tela mostra no horário do aparelho.
 */

/** Milissegundos de uma hora. */
const UMA_HORA_MS = 60 * 60 * 1000;

/** Data e hora de agora em ISO UTC, no formato que a API espera. */
export function agoraIso(): string {
  return new Date().toISOString();
}

/** Soma horas a uma data e devolve em ISO UTC (usada para o `fimPrevisto`). */
export function somarHorasIso(inicio: Date, horas: number): string {
  return new Date(inicio.getTime() + horas * UMA_HORA_MS).toISOString();
}

/** Verdadeiro quando o horário (ISO) já passou no relógio do aparelho. */
export function jaPassou(iso: string): boolean {
  return new Date(iso).getTime() <= Date.now();
}

/**
 * Dia e hora curtos no horário do aparelho, como "sáb., 15/11 às 14:30".
 * Usa o `Intl` do Hermes com o português do Brasil.
 */
export function formatarDiaHora(iso: string): string {
  const data = new Date(iso);
  const dia = data.toLocaleDateString('pt-BR', { weekday: 'short', day: '2-digit', month: '2-digit' });
  const hora = data.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
  return `${dia} às ${hora}`;
}
