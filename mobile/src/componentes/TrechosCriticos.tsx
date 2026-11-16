import { Linking, View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import type { EstadoTrechos } from '@/hooks/useTrechosCriticos';
import { formatarDiaHora } from '@/utils/datas';

import { Aviso } from './Aviso';
import { Botao } from './Botao';
import { Cartao } from './Cartao';
import { Texto } from './Texto';

/** Abre o trecho no app de mapas pelo esquema `geo:` do Android (trechos são dado público). */
function abrirNoMapa(latitude: number, longitude: number, logradouro: string) {
  const rotulo = encodeURIComponent(logradouro);
  void Linking.openURL(`geo:${latitude},${longitude}?q=${latitude},${longitude}(${rotulo})`);
}

/** Props do `TrechosCriticos`. */
export interface TrechosCriticosProps {
  estado: EstadoTrechos;
}

/** Cartão "Trechos críticos perto de você": os 5 trechos com mais sinistros graves. */
export function TrechosCriticos({ estado }: TrechosCriticosProps) {
  const { espaco, cores } = useTema();
  const { lista, origem, carregando } = estado;

  return (
    <Cartao titulo="Trechos críticos perto de você">
      {origem === 'cache' && lista ? (
        <Aviso tipo="atencao" mensagem={`Lista salva em ${formatarDiaHora(lista.atualizadoEm)}.`} />
      ) : null}
      {origem === 'indisponivel' ? <Aviso tipo="atencao" mensagem="Lista indisponível agora." /> : null}
      {carregando && !lista ? <Texto cor="textoSecundario">Buscando os trechos...</Texto> : null}
      {lista ? (
        <>
          <Texto variante="legenda">{`Sinistros graves com moto de ${lista.periodo}.`}</Texto>
          {lista.trechos.map((trecho) => (
            <View
              key={trecho.posicao}
              style={{ gap: espaco.pequeno, borderTopWidth: 1, borderTopColor: cores.trilha, paddingTop: espaco.medio }}
            >
              <View
                accessible
                accessibilityLabel={`${trecho.posicao}º trecho: ${trecho.logradouro}, ${trecho.quantidadeSinistrosGraves} sinistros graves`}
              >
                <Texto variante="rotulo">{`${trecho.posicao}. ${trecho.logradouro}`}</Texto>
                <Texto variante="legenda">{`${trecho.quantidadeSinistrosGraves} sinistros graves`}</Texto>
              </View>
              <Botao
                titulo="Ver no mapa"
                variante="secundaria"
                icone="map"
                aoPressionar={() => abrirNoMapa(trecho.latitude, trecho.longitude, trecho.logradouro)}
                accessibilityLabel={`Ver ${trecho.logradouro} no mapa`}
                accessibilityHint="Abre o app de mapas do celular"
              />
            </View>
          ))}
        </>
      ) : null}
    </Cartao>
  );
}
