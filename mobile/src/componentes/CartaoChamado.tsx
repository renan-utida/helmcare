import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import { useCronometro } from '@/hooks/useCronometro';
import type { OcorrenciaProximaResposta } from '@/tipos/contrato';
import { duracaoFalada, formatarDecimal, formatarMinutosSegundos } from '@/utils/datas';

import { Botao } from './Botao';
import { Cartao } from './Cartao';
import { Icone } from './Icone';
import { Texto } from './Texto';

/** Camada do SOS em texto simples (1: os mais perto; 2: toda a rede). */
export function textoCamada(camada: number): string {
  return camada === 1 ? 'Primeiros voluntários' : 'Toda a rede';
}

/** Props do `CartaoChamado`. */
export interface CartaoChamadoProps {
  chamado: OcorrenciaProximaResposta;
  /** Instante (relógio do app, ms) da leitura da lista, para somar os segundos do cronômetro. */
  lidoEm: number | null;
  aoAceitar: (ocorrenciaId: string) => void;
  /** Este chamado está sendo aceito agora. */
  aceitando: boolean;
  /** Outro aceite está em andamento: os botões ficam bloqueados. */
  bloqueado: boolean;
}

/**
 * Um chamado próximo: "Alguém caiu a cerca de 0,6 km", a camada em texto, o cronômetro desde a
 * queda e o botão "Aceitar e ir ajudar". Antes do aceite, nenhuma coordenada aparece na tela:
 * só a `distanciaKm`, que a API já calcula até a localização aproximada (seção 9).
 */
export function CartaoChamado({ chamado, lidoEm, aoAceitar, aceitando, bloqueado }: CartaoChamadoProps) {
  const { cores, espaco } = useTema();
  const segundos = useCronometro(chamado.cronometro, lidoEm);
  const distancia = formatarDecimal(chamado.distanciaKm);
  const camada = textoCamada(chamado.camada);
  const tempo = segundos === null ? null : formatarMinutosSegundos(segundos);
  return (
    <Cartao>
      <View
        accessible
        accessibilityLabel={`Alguém caiu a cerca de ${distancia} quilômetros. Chamado para: ${camada}.${
          segundos === null ? '' : ` ${duracaoFalada(segundos)} desde a queda.`
        }`}
        style={{ gap: espaco.pequeno }}
      >
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
          <Icone nome="alert-circle" cor={cores.aviso.erro.icone} tamanho={32} />
          <Texto variante="subtitulo" style={{ flex: 1 }}>{`Alguém caiu a cerca de ${distancia} km`}</Texto>
        </View>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
          <Icone nome={chamado.camada === 1 ? 'radio' : 'megaphone'} cor={cores.textoSecundario} />
          <Texto variante="rotulo" style={{ flex: 1 }}>
            {camada}
          </Texto>
        </View>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
          <Icone nome="time" cor={cores.textoSecundario} />
          <Texto style={{ flex: 1 }}>{tempo === null ? 'Tempo desde a queda indisponível' : `${tempo} desde a queda`}</Texto>
        </View>
      </View>
      <Botao
        titulo="Aceitar e ir ajudar"
        icone="walk"
        alturaMinima={64}
        aoPressionar={() => aoAceitar(chamado.ocorrenciaId)}
        carregando={aceitando}
        desabilitado={bloqueado && !aceitando}
        accessibilityLabel={`Aceitar e ir ajudar: chamado a ${distancia} quilômetros`}
        accessibilityHint="Avisa o entregador que você está a caminho e mostra onde ele está"
      />
    </Cartao>
  );
}
