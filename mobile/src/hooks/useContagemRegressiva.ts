import { useEffect, useState } from 'react';

/** De quanto em quanto tempo o relógio do app é relido (menos que 1 s, para o número virar em dia). */
const PASSO_MS = 250;

/**
 * Contagem regressiva no relógio do app (seção 7 do contrato, alerta de queda): parte de
 * `inicioMs` (a primeira leitura com `estadoCapacete` = CONTAGEM, guardada pelo TurnoContext)
 * e devolve os segundos que faltam, de `totalS` até 0. O `contagemTerminaEm` da API não entra
 * aqui, porque está no relógio do capacete, que atrasa no Wokwi. Sem início, devolve `null`.
 */
export function useContagemRegressiva(inicioMs: number | null, totalS: number): number | null {
  const [agora, setAgora] = useState<number | null>(null);

  useEffect(() => {
    if (inicioMs === null) {
      return;
    }
    // Relê o relógio no próprio temporizador; o primeiro valor sai já no primeiro passo.
    const id = setInterval(() => setAgora(Date.now()), PASSO_MS);
    return () => clearInterval(id);
  }, [inicioMs]);

  if (inicioMs === null) {
    return null;
  }
  // Antes do primeiro passo do relógio, mostra a contagem cheia.
  const decorridoS = agora === null ? 0 : (agora - inicioMs) / 1000;
  return Math.min(totalS, Math.max(0, Math.ceil(totalS - decorridoS)));
}
