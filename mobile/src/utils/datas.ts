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

/** Número com uma casa decimal e vírgula, como "37,1" (índice de calor). */
export function formatarDecimal(valor: number, casas = 1): string {
  return valor.toFixed(casas).replace('.', ',');
}

/** Duração em minutos e segundos, como "01:05" (cronômetro da ocorrência). */
export function formatarMinutosSegundos(totalSegundos: number): string {
  const seguro = Math.max(0, Math.floor(totalSegundos));
  const minutos = Math.floor(seguro / 60);
  const segundos = seguro % 60;
  return `${String(minutos).padStart(2, '0')}:${String(segundos).padStart(2, '0')}`;
}

/** Hora com segundos no horário do aparelho, como "21:16:17". */
export function formatarHoraComSegundos(iso: string): string {
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
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

/** Duração falada para o leitor de tela, como "1 minuto e 5 segundos". */
export function duracaoFalada(totalSegundos: number): string {
  const seguro = Math.max(0, Math.floor(totalSegundos));
  const minutos = Math.floor(seguro / 60);
  const segundos = seguro % 60;
  const partes: string[] = [];
  if (minutos > 0) partes.push(`${minutos} ${minutos === 1 ? 'minuto' : 'minutos'}`);
  partes.push(`${segundos} ${segundos === 1 ? 'segundo' : 'segundos'}`);
  return partes.join(' e ');
}

/** Data curta no horário do aparelho, como "sáb., 15/11". */
export function formatarDia(iso: string): string {
  return new Date(iso).toLocaleDateString('pt-BR', { weekday: 'short', day: '2-digit', month: '2-digit' });
}

/** Hora curta no horário do aparelho, como "14:30". */
export function formatarHora(iso: string): string {
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

/**
 * Duração entre dois horários ISO em horas e minutos, como "3 h 05 min" ou "45 min". Com
 * `falada`, sai por extenso para o leitor de tela ("3 horas e 5 minutos").
 */
export function formatarDuracaoEntre(inicioIso: string, fimIso: string, falada = false): string {
  const minutosTotais = Math.max(0, Math.round((new Date(fimIso).getTime() - new Date(inicioIso).getTime()) / 60000));
  const horas = Math.floor(minutosTotais / 60);
  const minutos = minutosTotais % 60;
  if (falada) {
    const textoMinutos = `${minutos} ${minutos === 1 ? 'minuto' : 'minutos'}`;
    return horas > 0 ? `${horas} ${horas === 1 ? 'hora' : 'horas'} e ${textoMinutos}` : textoMinutos;
  }
  return horas > 0 ? `${horas} h ${String(minutos).padStart(2, '0')} min` : `${minutos} min`;
}
