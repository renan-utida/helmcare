import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import type { FaixaRisco as Faixa } from '@/tipos/contrato';

import { Icone, type NomeIcone } from './Icone';
import { Texto } from './Texto';

/** Texto de cada faixa do contrato (BAIXO, MODERADO, ALTO, MUITO_ALTO). */
export const TEXTO_FAIXA: Record<Faixa, string> = {
  BAIXO: 'Risco baixo',
  MODERADO: 'Risco moderado',
  ALTO: 'Risco alto',
  MUITO_ALTO: 'Risco muito alto',
};

/** Ícone de cada faixa: forma diferente para quem não distingue as cores. */
const ICONE_FAIXA: Record<Faixa, NomeIcone> = {
  BAIXO: 'shield-checkmark',
  MODERADO: 'alert-circle',
  ALTO: 'warning',
  MUITO_ALTO: 'flame',
};

/** Props do selo da faixa. */
export interface FaixaRiscoProps {
  faixa: Faixa;
}

/** Selo da faixa de risco, sempre com ícone e texto (a cor nunca é a única informação). */
export function FaixaRisco({ faixa }: FaixaRiscoProps) {
  const { cores, espaco, raio } = useTema();
  const cor = cores.faixa[faixa];
  return (
    <View
      accessible
      accessibilityLabel={TEXTO_FAIXA[faixa]}
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        alignSelf: 'flex-start',
        gap: espaco.pequeno,
        backgroundColor: cor.fundo,
        borderRadius: raio.pilula,
        paddingHorizontal: espaco.medio,
        paddingVertical: espaco.pequeno,
      }}
    >
      <Icone nome={ICONE_FAIXA[faixa]} cor={cor.texto} />
      <Texto variante="rotulo" corPersonalizada={cor.texto}>
        {TEXTO_FAIXA[faixa]}
      </Texto>
    </View>
  );
}
