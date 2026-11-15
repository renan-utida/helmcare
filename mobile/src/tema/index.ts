import { coresClaras, coresEscuras, type Cores } from './cores';
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
