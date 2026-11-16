import { useSQLiteContext } from 'expo-sqlite';
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useColorScheme } from 'react-native';

import { CHAVES_PREFERENCIA, preferenciaRepositorio } from '@/banco/preferenciaRepositorio';
import { temaClaro, temaEscuro, type Tema } from '@/tema';

/** Aparência escolhida: igual ao celular (padrão), sempre clara ou sempre escura. */
export type PreferenciaTema = 'sistema' | 'claro' | 'escuro';

/** Lista das preferências, na ordem dos botões das Configurações. */
export const PREFERENCIAS_TEMA: readonly PreferenciaTema[] = ['sistema', 'claro', 'escuro'];

/** Confere se o texto salvo no SQLite é uma preferência válida. */
function ehPreferenciaTema(valor: string | null): valor is PreferenciaTema {
  return valor !== null && (PREFERENCIAS_TEMA as readonly string[]).includes(valor);
}

/** Preferência de aparência e a função para trocá-la. */
interface ValorPreferenciaTema {
  preferencia: PreferenciaTema;
  definirPreferencia: (preferencia: PreferenciaTema) => Promise<void>;
}

const TemaContext = createContext<Tema>(temaClaro);
const PreferenciaTemaContext = createContext<ValorPreferenciaTema | null>(null);

/**
 * Fornece o tema claro ou escuro. A escolha das Configurações fica na tabela `preferencia`
 * (chave "tema"); o `useColorScheme` do sistema só vale no modo "Igual ao celular".
 */
export function TemaProvider({ children }: { children: ReactNode }) {
  const db = useSQLiteContext();
  const esquemaSistema = useColorScheme();
  const [preferencia, setPreferencia] = useState<PreferenciaTema>('sistema');

  // Lê a escolha salva ao abrir o app. Até lá, segue o sistema (a leitura leva milissegundos).
  useEffect(() => {
    let ativo = true;
    (async () => {
      const salva = await preferenciaRepositorio.obter(db, CHAVES_PREFERENCIA.tema);
      if (ativo && ehPreferenciaTema(salva)) {
        setPreferencia(salva);
      }
    })();
    return () => {
      ativo = false;
    };
  }, [db]);

  const definirPreferencia = useCallback(
    async (nova: PreferenciaTema) => {
      setPreferencia(nova);
      await preferenciaRepositorio.definir(db, CHAVES_PREFERENCIA.tema, nova);
    },
    [db],
  );

  const escuro = preferencia === 'escuro' || (preferencia === 'sistema' && esquemaSistema === 'dark');
  const tema = escuro ? temaEscuro : temaClaro;
  const valorPreferencia = useMemo(() => ({ preferencia, definirPreferencia }), [preferencia, definirPreferencia]);

  return (
    <PreferenciaTemaContext.Provider value={valorPreferencia}>
      <TemaContext.Provider value={tema}>{children}</TemaContext.Provider>
    </PreferenciaTemaContext.Provider>
  );
}

/**
 * Tema do celular, sem ler o SQLite. Serve só para a abertura, que aparece antes de o
 * banco abrir (e, portanto, antes de a escolha das Configurações poder ser lida).
 */
export function TemaDoSistema({ children }: { children: ReactNode }) {
  const tema = useColorScheme() === 'dark' ? temaEscuro : temaClaro;
  return <TemaContext.Provider value={tema}>{children}</TemaContext.Provider>;
}

/** Devolve o tema atual. Cores, fontes e espaçamentos das telas vêm sempre daqui. */
export function useTema(): Tema {
  return useContext(TemaContext);
}

/** Preferência de aparência das Configurações. Só funciona dentro do `TemaProvider`. */
export function usePreferenciaTema(): ValorPreferenciaTema {
  const valor = useContext(PreferenciaTemaContext);
  if (!valor) {
    throw new Error('usePreferenciaTema precisa estar dentro do TemaProvider.');
  }
  return valor;
}
