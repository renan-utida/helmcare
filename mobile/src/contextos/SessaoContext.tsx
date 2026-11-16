import { useSQLiteContext } from 'expo-sqlite';
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

import { perfilRepositorio, type Perfil } from '@/banco/perfilRepositorio';
import { CHAVES_PREFERENCIA, preferenciaRepositorio } from '@/banco/preferenciaRepositorio';
import type { UrlApiResolvida } from '@/config/urlApi';
import { aplicarUrlManual, obterUrlEmUso } from '@/servicos/api';

/** Apelido neutro de quem tem capacete vinculado (nunca o nome da pessoa). */
export const APELIDO_COM_CAPACETE = 'Entregador com capacete';
/** Apelido neutro de quem ainda não tem capacete. */
export const APELIDO_SEM_CAPACETE = 'Voluntário';

/** O que a sessão local entrega para as telas. */
export interface ValorSessao {
  /** Perfis guardados no aparelho. */
  perfis: Perfil[];
  /** Perfil em uso, ou `null` (vai para o cadastro). */
  perfilAtivo: Perfil | null;
  /** Verdadeiro até a primeira leitura do SQLite terminar. */
  carregando: boolean;
  /** URL da API em uso e a origem dela. */
  urlApi: UrlApiResolvida;
  /** Torna ativo o perfil do entregador informado. */
  definirPerfilAtivo: (entregadorId: string) => Promise<void>;
  /** Grava (ou atualiza) um perfil e, com `ativar`, já o deixa em uso. */
  salvarPerfil: (perfil: Perfil, opcoes?: { ativar?: boolean }) => Promise<void>;
  /** Apaga do aparelho um perfil que não está em uso e os dados locais dele (a API não muda). */
  removerPerfil: (entregadorId: string) => Promise<void>;
  /**
   * Escolhe um apelido livre a partir da base: se outro perfil já usa "Voluntário",
   * devolve "Voluntário 2", depois "Voluntário 3", e assim por diante.
   */
  escolherApelido: (base: string, entregadorId: string) => string;
  /** Salva a URL manual da API (ou `null` para voltar à automática) e passa a usá-la. */
  definirUrlManual: (url: string | null) => Promise<void>;
  /** Lê de novo os perfis e o perfil ativo do SQLite. */
  recarregar: () => Promise<void>;
}

const SessaoContext = createContext<ValorSessao | null>(null);

/**
 * Sessão local do aparelho, guardada no SQLite (tabelas `perfil` e `preferencia`).
 * Ao abrir, também aplica a URL manual da API salva nos Ajustes.
 */
export function SessaoProvider({ children }: { children: ReactNode }) {
  const db = useSQLiteContext();
  const [perfis, setPerfis] = useState<Perfil[]>([]);
  const [perfilAtivo, setPerfilAtivo] = useState<Perfil | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [urlApi, setUrlApi] = useState<UrlApiResolvida>(obterUrlEmUso());

  const recarregar = useCallback(async () => {
    const lista = await perfilRepositorio.listar(db);
    const idAtivo = await preferenciaRepositorio.obter(db, CHAVES_PREFERENCIA.perfilAtivo);
    let ativo = lista.find((perfil) => perfil.entregadorId === idAtivo) ?? null;
    // Se o perfil ativo sumiu mas há outros, usa o primeiro, para não mandar de volta ao cadastro.
    if (!ativo && lista.length > 0) {
      ativo = lista[0] ?? null;
      if (ativo) {
        await preferenciaRepositorio.definir(db, CHAVES_PREFERENCIA.perfilAtivo, ativo.entregadorId);
      }
    }
    setPerfis(lista);
    setPerfilAtivo(ativo);
  }, [db]);

  // Primeira carga: URL manual da API e perfis.
  useEffect(() => {
    let ativo = true;
    (async () => {
      const urlManual = await preferenciaRepositorio.obter(db, CHAVES_PREFERENCIA.urlApiManual);
      const resolvida = aplicarUrlManual(urlManual);
      await recarregar();
      if (ativo) {
        setUrlApi(resolvida);
        setCarregando(false);
      }
    })();
    // Evita trocar o estado depois que o provedor saiu da tela.
    return () => {
      ativo = false;
    };
  }, [db, recarregar]);

  const definirPerfilAtivo = useCallback(
    async (entregadorId: string) => {
      await preferenciaRepositorio.definir(db, CHAVES_PREFERENCIA.perfilAtivo, entregadorId);
      await recarregar();
    },
    [db, recarregar],
  );

  const salvarPerfil = useCallback(
    async (perfil: Perfil, opcoes?: { ativar?: boolean }) => {
      await perfilRepositorio.salvar(db, perfil);
      if (opcoes?.ativar) {
        await preferenciaRepositorio.definir(db, CHAVES_PREFERENCIA.perfilAtivo, perfil.entregadorId);
      }
      await recarregar();
    },
    [db, recarregar],
  );

  const removerPerfil = useCallback(
    async (entregadorId: string) => {
      // O perfil em uso nunca é removido por aqui: a tela só oferece a remoção dos outros.
      if (entregadorId === perfilAtivo?.entregadorId) {
        return;
      }
      await perfilRepositorio.removerComDadosLocais(db, entregadorId);
      await recarregar();
    },
    [db, perfilAtivo, recarregar],
  );

  const escolherApelido = useCallback(
    (base: string, entregadorId: string) => {
      const emUso = new Set(
        perfis.filter((perfil) => perfil.entregadorId !== entregadorId).map((perfil) => perfil.apelido),
      );
      if (!emUso.has(base)) {
        return base;
      }
      let numero = 2;
      while (emUso.has(`${base} ${numero}`)) {
        numero += 1;
      }
      return `${base} ${numero}`;
    },
    [perfis],
  );

  const definirUrlManual = useCallback(
    async (url: string | null) => {
      if (url) {
        await preferenciaRepositorio.definir(db, CHAVES_PREFERENCIA.urlApiManual, url);
      } else {
        await preferenciaRepositorio.remover(db, CHAVES_PREFERENCIA.urlApiManual);
      }
      setUrlApi(aplicarUrlManual(url));
    },
    [db],
  );

  const valor = useMemo<ValorSessao>(
    () => ({
      perfis,
      perfilAtivo,
      carregando,
      urlApi,
      definirPerfilAtivo,
      salvarPerfil,
      removerPerfil,
      escolherApelido,
      definirUrlManual,
      recarregar,
    }),
    [
      perfis,
      perfilAtivo,
      carregando,
      urlApi,
      definirPerfilAtivo,
      salvarPerfil,
      removerPerfil,
      escolherApelido,
      definirUrlManual,
      recarregar,
    ],
  );

  return <SessaoContext.Provider value={valor}>{children}</SessaoContext.Provider>;
}

/** Acessa a sessão local. Só funciona dentro do `SessaoProvider`. */
export function useSessao(): ValorSessao {
  const valor = useContext(SessaoContext);
  if (!valor) {
    throw new Error('useSessao precisa estar dentro do SessaoProvider.');
  }
  return valor;
}
