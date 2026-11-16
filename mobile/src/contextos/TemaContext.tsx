import { createContext, useContext, type ReactNode } from 'react';
import { useColorScheme } from 'react-native';

import { temaClaro, temaEscuro, type Tema } from '@/tema';

const TemaContext = createContext<Tema>(temaClaro);

/** Fornece o tema claro ou escuro, seguindo a configuração do sistema (`useColorScheme`). */
export function TemaProvider({ children }: { children: ReactNode }) {
  const esquema = useColorScheme();
  const tema = esquema === 'dark' ? temaEscuro : temaClaro;
  return <TemaContext.Provider value={tema}>{children}</TemaContext.Provider>;
}

/** Devolve o tema atual. Cores, fontes e espaçamentos das telas vêm sempre daqui. */
export function useTema(): Tema {
  return useContext(TemaContext);
}
