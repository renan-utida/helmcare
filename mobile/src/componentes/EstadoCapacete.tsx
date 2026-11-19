import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import type { EstadoDispositivo } from '@/tipos/contrato';

import { Cartao } from './Cartao';
import { Icone, type NomeIcone } from './Icone';
import type { TomCirculo } from './IconeEmCirculo';
import { Texto } from './Texto';

/** Tom do estado, que escolhe a cor do ícone (o texto sempre diz o estado). */
type Tom = 'neutro' | 'bom' | 'atencao' | 'perigo';

/** Texto simples, ícone e tom de cada estado do capacete (`EstadoDispositivo` da seção 4). */
const APARENCIA: Record<EstadoDispositivo | 'SEM_CAPACETE', { texto: string; icone: NomeIcone; tom: Tom }> = {
  DESARMADO: { texto: 'Proteção desligada', icone: 'shield-outline', tom: 'neutro' },
  ARMADO: { texto: 'Protegendo você', icone: 'shield-checkmark', tom: 'bom' },
  IMPACTO_DETECTADO: { texto: 'Verificando um impacto', icone: 'alert-circle', tom: 'atencao' },
  OBSERVACAO: { texto: 'Verificando um impacto', icone: 'alert-circle', tom: 'atencao' },
  CONTAGEM: { texto: 'Contagem do alerta', icone: 'timer', tom: 'perigo' },
  SOS_ENVIADO: { texto: 'SOS enviado', icone: 'medkit', tom: 'perigo' },
  CANCELADO: { texto: 'Alerta cancelado', icone: 'close-circle', tom: 'neutro' },
  FALSO_POSITIVO: { texto: 'Alarme falso: tudo certo', icone: 'checkmark-circle', tom: 'bom' },
  SEM_CAPACETE: { texto: 'Sem capacete neste turno', icone: 'remove-circle-outline', tom: 'neutro' },
};

/** Tom do círculo do ícone para cada tom do estado. */
const TOM_CIRCULO: Record<Tom, TomCirculo> = { neutro: 'informacao', bom: 'sucesso', atencao: 'atencao', perigo: 'erro' };

/**
 * Texto e ícone do estado do capacete, a cor do ícone e o tom do círculo pelo tom do estado
 * (o texto sempre diz o estado: a cor nunca é a única pista).
 */
export function useAparenciaCapacete(estado: EstadoDispositivo | null): {
  texto: string;
  icone: NomeIcone;
  cor: string;
  tomCirculo: TomCirculo;
} {
  const { cores } = useTema();
  const { texto, icone, tom } = APARENCIA[estado ?? 'SEM_CAPACETE'];
  const cor = {
    neutro: cores.textoSecundario,
    bom: cores.aviso.sucesso.icone,
    atencao: cores.aviso.atencao.icone,
    perigo: cores.aviso.erro.icone,
  }[tom];
  return { texto, icone, cor, tomCirculo: TOM_CIRCULO[tom] };
}

/** Props do `EstadoCapacete`. */
export interface EstadoCapaceteProps {
  /** Estado do último `GET /turnos/{id}/alerta`, ou `null` (turno sem capacete ou sem leitura). */
  estado: EstadoDispositivo | null;
  /** Mostra a nota de que a leitura pode estar velha (sem conexão). */
  semConexao?: boolean;
}

/** Cartão "Seu capacete": o estado em linguagem simples, com ícone e texto. */
export function EstadoCapacete({ estado, semConexao = false }: EstadoCapaceteProps) {
  const { espaco } = useTema();
  const { texto, icone, cor } = useAparenciaCapacete(estado);
  return (
    <Cartao titulo="Seu capacete">
      <View
        accessible
        accessibilityLabel={`Seu capacete: ${texto}${semConexao ? '. Sem conexão: pode estar desatualizado' : ''}`}
        accessibilityLiveRegion="polite"
        style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}
      >
        <Icone nome={icone} cor={cor} tamanho={32} />
        <Texto variante="subtitulo" style={{ flex: 1 }}>
          {texto}
        </Texto>
      </View>
      {semConexao ? (
        <Texto variante="legenda">Sem conexão: o estado pode estar desatualizado.</Texto>
      ) : null}
    </Cartao>
  );
}
