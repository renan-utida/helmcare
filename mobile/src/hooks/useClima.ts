import { useCallback, useState } from 'react';

import { climaServico } from '@/servicos/climaServico';
import type { ClimaResposta } from '@/tipos/contrato';

/** O que o hook entrega para o cartão "Clima agora". */
export interface EstadoClima {
  /** Clima da última consulta, ou `null` (ainda sem leitura ou o cartão está escondido). */
  clima: ClimaResposta | null;
  /** Consulta o `GET /clima` (ao abrir a aba Início e ao puxar para atualizar). */
  atualizar: () => Promise<void>;
}

/**
 * Clima da cidade pelo `GET /clima`, só em memória. É consultado ao abrir a aba Início e ao puxar
 * para atualizar, fora do polling de 5 s (contrato, seção 7). No 503 (Open-Meteo fora), sem
 * conexão ou em qualquer outra falha, o cartão some, sem mensagem de erro: nunca um valor velho
 * como se fosse atual.
 */
export function useClima(): EstadoClima {
  const [clima, setClima] = useState<ClimaResposta | null>(null);

  const atualizar = useCallback(async () => {
    try {
      setClima(await climaServico.obter());
    } catch {
      setClima(null);
    }
  }, []);

  return { clima, atualizar };
}
