import { View } from 'react-native';

import type { PapelOcorrencia } from '@/banco/ocorrenciaRepositorio';
import { useTema } from '@/contextos/TemaContext';
import { ESTADOS_FINAIS } from '@/hooks/useOcorrencia';
import type { OcorrenciaResposta } from '@/tipos/contrato';
import { duracaoFalada, formatarMinutosSegundos } from '@/utils/datas';

import { Cartao } from './Cartao';
import { Icone } from './Icone';
import { Texto } from './Texto';

/** Situação do tempo em relação à meta de 2 minutos (`META_TEMPO_ACEITE_S`). */
export type SituacaoMeta = 'dentro' | 'acima' | 'sem-aceite';

/** Texto de cada situação da meta. */
export const TEXTO_META: Record<SituacaoMeta, string> = {
  dentro: 'Dentro da meta',
  acima: 'Acima da meta',
  'sem-aceite': 'Sem aceite de voluntário',
};

/**
 * Compara um tempo até o aceite com a meta. Sem tempo (encerrada ou cancelada sem aceite),
 * não compara: "sem aceite" (regra do indicador de impacto, seção 7).
 */
export function situacaoDaMeta(segundos: number | null, meta: number): SituacaoMeta {
  if (segundos === null) {
    return 'sem-aceite';
  }
  return segundos <= meta ? 'dentro' : 'acima';
}

/** Props do `CartaoCronometro`. */
export interface CartaoCronometroProps {
  ocorrencia: OcorrenciaResposta;
  /** Segundos do `useCronometro` (o da API mais o relógio do app), ou `null` sem leitura. */
  segundos: number | null;
  papel: PapelOcorrencia;
}

/**
 * Cartão do cronômetro da ocorrência, com a meta de 2 minutos. Ativa: compara o cronômetro com
 * a meta. Terminada: usa o `tempoAteAceiteSegundos` (o indicador do contrato) ou, sem aceite,
 * mostra "Sem aceite de voluntário". Para o voluntário, depois do aceite o cronômetro para e o
 * cartão mostra "Você aceitou em mm:ss" e se ficou dentro da meta.
 */
export function CartaoCronometro({ ocorrencia, segundos, papel }: CartaoCronometroProps) {
  const { cores, espaco, raio } = useTema();
  const ativa = !ESTADOS_FINAIS.includes(ocorrencia.estado);
  const meta = ocorrencia.cronometro.metaSegundos;
  const minutosMeta = meta / 60;
  const aceiteFinal = ocorrencia.tempoAteAceiteSegundos;
  const aceitou = ocorrencia.aceitaEm !== null;

  // Voluntário que já aceitou: o número é o tempo da queda até o aceite dele.
  const mostrarAceite = papel === 'VOLUNTARIO' && aceitou;
  const tempoAceite = aceiteFinal ?? segundos;

  const situacao: SituacaoMeta = mostrarAceite
    ? situacaoDaMeta(tempoAceite, meta)
    : ativa
      ? segundos !== null && segundos <= meta
        ? 'dentro'
        : 'acima'
      : situacaoDaMeta(aceiteFinal, meta);

  const numero = mostrarAceite ? tempoAceite : segundos;
  const legendaNumero = mostrarAceite ? 'da queda até o seu aceite' : 'desde a queda';
  const titulo =
    mostrarAceite && tempoAceite !== null ? `Você aceitou em ${formatarMinutosSegundos(tempoAceite)}` : null;

  const leitura =
    numero === null
      ? 'Cronômetro indisponível'
      : mostrarAceite
        ? `Você aceitou ${duracaoFalada(numero)} depois da queda. Meta: socorro em até ${minutosMeta} minutos. ${TEXTO_META[situacao]}.`
        : `${duracaoFalada(numero)} desde a queda. Meta: socorro em até ${minutosMeta} minutos. ${TEXTO_META[situacao]}.`;

  return (
    <Cartao>
      <View accessible accessibilityLabel={leitura} style={{ gap: espaco.pequeno }}>
        {titulo ? <Texto variante="subtitulo">{titulo}</Texto> : null}
        <Texto variante="destaque">{numero === null ? '--:--' : formatarMinutosSegundos(numero)}</Texto>
        <Texto variante="rotulo">{legendaNumero}</Texto>
        <Texto>{`Meta: socorro em até ${minutosMeta} minutos`}</Texto>
        {/* Barra da meta: quanto do tempo de 2 minutos já passou (só desenho; o texto diz a situação). */}
        <View
          accessible={false}
          importantForAccessibility="no-hide-descendants"
          style={{ height: espaco.pequeno + 4, backgroundColor: cores.trilha, borderRadius: raio.pequeno, overflow: 'hidden' }}
        >
          <View
            style={{
              width: `${Math.min(100, Math.round(((numero ?? 0) / meta) * 100))}%`,
              height: '100%',
              backgroundColor: situacao === 'acima' ? cores.aviso.atencao.icone : cores.aviso.sucesso.icone,
            }}
          />
        </View>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
          <Icone
            nome={situacao === 'dentro' ? 'checkmark-circle' : situacao === 'acima' ? 'time' : 'remove-circle'}
            cor={situacao === 'dentro' ? cores.aviso.sucesso.icone : cores.aviso.atencao.icone}
          />
          <Texto variante="rotulo" style={{ flex: 1 }}>
            {TEXTO_META[situacao]}
          </Texto>
        </View>
        {ocorrencia.cronometro.parado ? <Texto variante="legenda">O cronômetro parou.</Texto> : null}
      </View>
    </Cartao>
  );
}
