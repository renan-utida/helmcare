import { useCallback, useEffect, useState } from 'react';

import { converterErro, type TipoErroApi } from '@/servicos/erros';
import { entregadorServico } from '@/servicos/entregadorServico';

/**
 * Nomes já buscados nesta sessão do app, por id do entregador. Ficam SÓ em memória:
 * o nome é dado pessoal e nunca vai para o SQLite (CLAUDE.md, "Banco local").
 */
const nomesEmMemoria = new Map<string, string>();

/** O que o hook entrega: o nome (ou `null`), se está buscando e o tipo do último erro. */
export interface NomeEntregador {
  nome: string | null;
  carregando: boolean;
  erro: TipoErroApi | null;
  recarregar: () => Promise<void>;
}

/**
 * Nome do entregador pelo `GET /entregadores/{id}` (a API devolve o nome sem máscara para o
 * próprio entregador). Usado no menu da conta e nos cartões de perfil. Sem id, nada é buscado.
 */
export function useNomeEntregador(entregadorId: string | null): NomeEntregador {
  const [nome, setNome] = useState<string | null>(entregadorId ? (nomesEmMemoria.get(entregadorId) ?? null) : null);
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<TipoErroApi | null>(null);

  const buscar = useCallback(async () => {
    if (!entregadorId) {
      return;
    }
    try {
      const entregador = await entregadorServico.obter(entregadorId);
      nomesEmMemoria.set(entregadorId, entregador.nome);
      setNome(entregador.nome);
      setErro(null);
    } catch (e) {
      setErro(converterErro(e).tipo);
    } finally {
      setCarregando(false);
    }
  }, [entregadorId]);

  const recarregar = useCallback(async () => {
    setCarregando(true);
    await buscar();
  }, [buscar]);

  // Ao abrir (ou trocar de entregador): usa o nome em memória na hora e confirma na API.
  useEffect(() => {
    let ativo = true;
    (async () => {
      // Espera um passo antes de mexer no estado: trocar estado direto no corpo do efeito
      // causa renderizações em cascata (regra react-hooks/set-state-in-effect).
      await Promise.resolve();
      if (!ativo) {
        return;
      }
      setNome(entregadorId ? (nomesEmMemoria.get(entregadorId) ?? null) : null);
      await buscar();
    })();
    return () => {
      ativo = false;
    };
  }, [entregadorId, buscar]);

  return { nome, carregando, erro, recarregar };
}
