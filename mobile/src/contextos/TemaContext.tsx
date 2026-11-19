import { useSQLiteContext } from 'expo-sqlite';
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useColorScheme } from 'react-native';

import { CHAVES_PREFERENCIA, preferenciaRepositorio } from '@/banco/preferenciaRepositorio';
import { montarTema, temaClaro, temaEscuro, type Tema } from '@/tema';
import { PALETAS, type PaletaClara, type PaletaEscura } from '@/tema/cores';

/** Modo de cor escolhido nos Ajustes. */
export type ModoTema = 'claro' | 'escuro';

/** Confere se o texto salvo é uma paleta do modo claro. */
function ehPaletaClara(valor: string | null): valor is PaletaClara {
  return PALETAS.claro.some((paleta) => paleta.id === valor);
}

/** Confere se o texto salvo é uma paleta do modo escuro. */
function ehPaletaEscura(valor: string | null): valor is PaletaEscura {
  return PALETAS.escuro.some((paleta) => paleta.id === valor);
}

/** Escolhas de aparência e as funções para trocá-las (aba Ajustes). */
interface ValorAparencia {
  modo: ModoTema;
  paletaClara: PaletaClara;
  paletaEscura: PaletaEscura;
  definirModo: (modo: ModoTema) => Promise<void>;
  definirPaletaClara: (paleta: PaletaClara) => Promise<void>;
  definirPaletaEscura: (paleta: PaletaEscura) => Promise<void>;
}

const TemaContext = createContext<Tema>(temaClaro);
const AparenciaContext = createContext<ValorAparencia | null>(null);

/**
 * Fornece o tema: o modo (claro ou escuro) e a paleta de cada modo, escolhidos nos Ajustes e
 * guardados na tabela `preferencia` (chaves "tema", "paleta_claro" e "paleta_escuro"). Quem tinha
 * "Igual ao celular" salvo (a opção antiga), ou nunca escolheu, passa uma vez para o modo atual do
 * celular, que fica salvo; daí em diante vale a escolha. Até a leitura terminar, segue o celular.
 */
export function TemaProvider({ children }: { children: ReactNode }) {
  const db = useSQLiteContext();
  const esquemaSistema = useColorScheme();
  const [modo, setModo] = useState<ModoTema | null>(null);
  const [paletaClara, setPaletaClara] = useState<PaletaClara>('helmcare');
  const [paletaEscura, setPaletaEscura] = useState<PaletaEscura>('helmcare');

  // Lê as escolhas salvas ao abrir o app (a leitura leva milissegundos).
  useEffect(() => {
    let ativo = true;
    (async () => {
      const salvo = await preferenciaRepositorio.obter(db, CHAVES_PREFERENCIA.tema);
      const clara = await preferenciaRepositorio.obter(db, CHAVES_PREFERENCIA.paletaClara);
      const escura = await preferenciaRepositorio.obter(db, CHAVES_PREFERENCIA.paletaEscura);
      let modoSalvo: ModoTema;
      if (salvo === 'claro' || salvo === 'escuro') {
        modoSalvo = salvo;
      } else {
        // "Igual ao celular" (ou nada salvo): vira o modo atual do celular, uma vez só.
        modoSalvo = esquemaSistema === 'dark' ? 'escuro' : 'claro';
        await preferenciaRepositorio.definir(db, CHAVES_PREFERENCIA.tema, modoSalvo);
      }
      if (!ativo) {
        return;
      }
      setModo(modoSalvo);
      if (ehPaletaClara(clara)) setPaletaClara(clara);
      if (ehPaletaEscura(escura)) setPaletaEscura(escura);
    })();
    return () => {
      ativo = false;
    };
    // O esquema do sistema só vale na primeira leitura (a migração); não relê a cada mudança dele.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [db]);

  const definirModo = useCallback(
    async (novo: ModoTema) => {
      setModo(novo);
      await preferenciaRepositorio.definir(db, CHAVES_PREFERENCIA.tema, novo);
    },
    [db],
  );

  const definirPaletaClara = useCallback(
    async (nova: PaletaClara) => {
      setPaletaClara(nova);
      await preferenciaRepositorio.definir(db, CHAVES_PREFERENCIA.paletaClara, nova);
    },
    [db],
  );

  const definirPaletaEscura = useCallback(
    async (nova: PaletaEscura) => {
      setPaletaEscura(nova);
      await preferenciaRepositorio.definir(db, CHAVES_PREFERENCIA.paletaEscura, nova);
    },
    [db],
  );

  const modoEfetivo: ModoTema = modo ?? (esquemaSistema === 'dark' ? 'escuro' : 'claro');
  const tema = useMemo(
    () => montarTema(modoEfetivo, modoEfetivo === 'claro' ? paletaClara : paletaEscura),
    [modoEfetivo, paletaClara, paletaEscura],
  );
  const valorAparencia = useMemo(
    () => ({ modo: modoEfetivo, paletaClara, paletaEscura, definirModo, definirPaletaClara, definirPaletaEscura }),
    [modoEfetivo, paletaClara, paletaEscura, definirModo, definirPaletaClara, definirPaletaEscura],
  );

  return (
    <AparenciaContext.Provider value={valorAparencia}>
      <TemaContext.Provider value={tema}>{children}</TemaContext.Provider>
    </AparenciaContext.Provider>
  );
}

/**
 * Tema do celular, sem ler o SQLite. Serve só para a abertura, que aparece antes de o
 * banco abrir (e, portanto, antes de a escolha dos Ajustes poder ser lida).
 */
export function TemaDoSistema({ children }: { children: ReactNode }) {
  const tema = useColorScheme() === 'dark' ? temaEscuro : temaClaro;
  return <TemaContext.Provider value={tema}>{children}</TemaContext.Provider>;
}

/** Devolve o tema atual. Cores, fontes e espaçamentos das telas vêm sempre daqui. */
export function useTema(): Tema {
  return useContext(TemaContext);
}

/** Escolhas de aparência dos Ajustes. Só funciona dentro do `TemaProvider`. */
export function useAparencia(): ValorAparencia {
  const valor = useContext(AparenciaContext);
  if (!valor) {
    throw new Error('useAparencia precisa estar dentro do TemaProvider.');
  }
  return valor;
}
