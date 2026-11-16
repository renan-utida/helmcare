import { useEffect, useState } from 'react';

import type { CronometroResposta } from '@/tipos/contrato';

/**
 * Cronômetro da ocorrência (seção 7 do contrato): usa o `segundosDesdeQueda` da última
 * consulta e soma 1 por segundo no relógio do app até a próxima. Com `parado` verdadeiro
 * (depois do aceite ou do fim), fica no valor da API. `lidoEm` é o instante (ms, relógio
 * do app) em que a consulta chegou. Sem leitura, devolve `null`.
 */
export function useCronometro(cronometro: CronometroResposta | null, lidoEm: number | null): number | null {
  const [agora, setAgora] = useState<number | null>(null);
  const contando = cronometro !== null && lidoEm !== null && !cronometro.parado;

  useEffect(() => {
    if (!contando) {
      return;
    }
    const id = setInterval(() => setAgora(Date.now()), 1000);
    return () => clearInterval(id);
  }, [contando, lidoEm]);

  if (!cronometro || lidoEm === null) {
    return null;
  }
  if (cronometro.parado || agora === null) {
    return cronometro.segundosDesdeQueda;
  }
  // `agora` pode ser de antes da última leitura (o relógio só anda de 1 em 1 s): nunca soma negativo.
  return cronometro.segundosDesdeQueda + Math.max(0, Math.floor((agora - lidoEm) / 1000));
}
