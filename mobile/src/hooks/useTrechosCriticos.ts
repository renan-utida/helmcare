import { useSQLiteContext } from 'expo-sqlite';
import { useCallback, useEffect, useState } from 'react';

import { trechoRepositorio, type TrechosEmCache } from '@/banco/trechoRepositorio';
import { trechoServico } from '@/servicos/trechoServico';

/**
 * Quantos trechos a API devolve numa chamada só: os 50 com mais acidentes graves da cidade. O
 * cartão "Mais perigosos" mostra os 3 primeiros, e o "Perto de você", os 3 mais próximos deles.
 */
const LIMITE_TRECHOS = 50;

/** Verdadeiro quando o cache foi salvo hoje, no horário do aparelho. */
function salvoHoje(atualizadoEm: string): boolean {
  return new Date(atualizadoEm).toDateString() === new Date().toDateString();
}

/** Situação da lista: da API agora, do cache (com a data) ou indisponível. */
export type OrigemTrechos = 'api' | 'cache' | 'indisponivel';

/** O que o hook entrega para a tela. */
export interface EstadoTrechos {
  lista: TrechosEmCache | null;
  origem: OrigemTrechos | null;
  carregando: boolean;
  /** Busca de novo na API (ao puxar para atualizar). */
  atualizar: () => Promise<void>;
}

/**
 * Trechos críticos do Turno em andamento: o `GET /trechos-criticos?limite=50`, gravado no
 * `trecho_cache`. Uma chamada só por dia: com o cache de hoje, a tela usa o cache e não chama a
 * API (o puxar para atualizar busca de novo). No 503 (Serviço de Risco fora) ou sem rede, entrega
 * a lista salva (origem "cache", com a data) ou nada ("indisponivel"). Os trechos são dado público.
 */
export function useTrechosCriticos(): EstadoTrechos {
  const db = useSQLiteContext();
  const [lista, setLista] = useState<TrechosEmCache | null>(null);
  const [origem, setOrigem] = useState<OrigemTrechos | null>(null);
  const [carregando, setCarregando] = useState(true);

  // A busca só muda o estado depois do primeiro `await`, então pode rodar dentro do efeito.
  const buscar = useCallback(async () => {
    try {
      const resposta = await trechoServico.listar(LIMITE_TRECHOS);
      await trechoRepositorio.substituir(db, resposta.periodo, resposta.trechos);
      setLista(await trechoRepositorio.listar(db));
      setOrigem('api');
    } catch {
      // Qualquer falha (503, sem rede, outro erro) cai no cache: a lista é só de apoio.
      const salva = await trechoRepositorio.listar(db);
      setLista(salva);
      setOrigem(salva ? 'cache' : 'indisponivel');
    } finally {
      setCarregando(false);
    }
  }, [db]);

  const atualizar = useCallback(async () => {
    setCarregando(true);
    await buscar();
  }, [buscar]);

  // Ao abrir: com o cache de hoje (e completo, com os 50), usa o cache; senão, busca na API.
  useEffect(() => {
    let ativo = true;
    (async () => {
      const salva = await trechoRepositorio.listar(db);
      if (ativo && salva && salvoHoje(salva.atualizadoEm) && salva.trechos.length >= LIMITE_TRECHOS) {
        setLista(salva);
        setOrigem('api');
        setCarregando(false);
        return;
      }
      if (ativo && salva) {
        setLista(salva);
      }
      if (ativo) {
        await buscar();
      }
    })();
    // Evita trocar o estado depois que a tela saiu.
    return () => {
      ativo = false;
    };
  }, [db, buscar]);

  return { lista, origem, carregando, atualizar };
}
