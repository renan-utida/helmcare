import { coresClaras, coresEscuras, PALETAS, type Cores, type PaletaClara, type PaletaEscura } from './cores';
import { espaco, medidas, raio } from './espacamentos';
import { tipografia } from './tipografia';

export type { Cores, CoresAviso, CoresFaixa } from './cores';
export type { VarianteTexto } from './tipografia';

/** Tema completo entregue pelo `useTema()`. */
export interface Tema {
  esquema: 'claro' | 'escuro';
  cores: Cores;
  tipografia: typeof tipografia;
  espaco: typeof espaco;
  raio: typeof raio;
  medidas: typeof medidas;
}

/** Tema claro. */
export const temaClaro: Tema = { esquema: 'claro', cores: coresClaras, tipografia, espaco, raio, medidas };

/** Tema escuro. */
export const temaEscuro: Tema = { esquema: 'escuro', cores: coresEscuras, tipografia, espaco, raio, medidas };

/** Monta o tema do modo com a paleta escolhida (as fontes e os espaços são os mesmos). */
export function montarTema(modo: 'claro' | 'escuro', paleta: PaletaClara | PaletaEscura): Tema {
  const lista: readonly { id: string; cores: Cores }[] = PALETAS[modo];
  const cores = lista.find((item) => item.id === paleta)?.cores ?? (modo === 'claro' ? coresClaras : coresEscuras);
  return { esquema: modo, cores, tipografia, espaco, raio, medidas };
}
