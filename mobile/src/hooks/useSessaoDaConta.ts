import { useEffect, useState } from 'react';

import { autenticacaoServico } from '@/servicos/autenticacaoServico';
import { converterErro, type TipoErroApi } from '@/servicos/erros';
import type { SessaoEntregadorResposta } from '@/tipos/contrato';

/**
 * Sessões já buscadas nesta abertura do app, por conta. Ficam SÓ em memória: o nome é dado pessoal
 * e nunca vai para o SQLite nem para o secure-store.
 */
const sessoesEmMemoria = new Map<string, SessaoEntregadorResposta>();

/** O que o hook entrega: a sessão da conta (ou `null`) e o tipo do último erro. */
export interface SessaoDaConta {
  sessao: SessaoEntregadorResposta | null;
  erro: TipoErroApi | null;
}

/**
 * Sessão de uma conta salva no aparelho, pelo `GET /entregadores/sessao` com o token DESSA conta
 * (não da conta ativa). Usada nos cartões de "Contas neste aparelho" para mostrar o nome, o
 * capacete e se é voluntário. Enquanto carrega ou sem conexão, a tela mostra "Conta salva".
 */
export function useSessaoDaConta(entregadorId: string, comSessao: boolean): SessaoDaConta {
  const [estado, setEstado] = useState<SessaoDaConta>({ sessao: sessoesEmMemoria.get(entregadorId) ?? null, erro: null });

  useEffect(() => {
    if (!comSessao) {
      return;
    }
    let vivo = true;
    (async () => {
      try {
        const sessao = await autenticacaoServico.obterSessao(entregadorId);
        sessoesEmMemoria.set(entregadorId, sessao);
        if (vivo) {
          setEstado({ sessao, erro: null });
        }
      } catch (e) {
        if (vivo) {
          setEstado((atual) => ({ sessao: atual.sessao, erro: converterErro(e).tipo }));
        }
      }
    })();
    return () => {
      vivo = false;
    };
  }, [entregadorId, comSessao]);

  return estado;
}
