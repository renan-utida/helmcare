import { Linking, View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import type { EstadoPosicao } from '@/hooks/useMinhaPosicao';
import type { EstadoTrechos } from '@/hooks/useTrechosCriticos';
import type { TrechoCriticoResposta } from '@/tipos/contrato';
import { formatarDecimal, formatarDiaHora } from '@/utils/datas';
import { distanciaKm } from '@/utils/distancia';

import { Aviso } from './Aviso';
import { Botao } from './Botao';
import { Cartao } from './Cartao';
import { Texto } from './Texto';

/** Quantos trechos cada cartão mostra. */
const POR_CARTAO = 3;

/** Abre o trecho no app de mapas pelo esquema `geo:` do Android (trechos são dado público). */
function abrirNoMapa(latitude: number, longitude: number, logradouro: string) {
  const rotulo = encodeURIComponent(logradouro);
  void Linking.openURL(`geo:${latitude},${longitude}?q=${latitude},${longitude}(${rotulo})`);
}

/** Um trecho: posição na lista da cidade, nome, acidentes graves, distância (opcional) e o mapa. */
function ItemTrecho({ trecho, km }: { trecho: TrechoCriticoResposta; km: number | null }) {
  const { cores, espaco } = useTema();
  const distancia = km === null ? null : `a ${formatarDecimal(km)} km`;
  return (
    <View style={{ gap: espaco.pequeno, borderTopWidth: 1, borderTopColor: cores.trilha, paddingTop: espaco.medio }}>
      <View
        accessible
        accessibilityLabel={`${trecho.posicao}º trecho da cidade: ${trecho.logradouro}, ${trecho.quantidadeSinistrosGraves} acidentes graves${
          km === null ? '' : `, a ${formatarDecimal(km)} quilômetros de você`
        }`}
      >
        <Texto variante="rotulo">{`${trecho.posicao}. ${trecho.logradouro}`}</Texto>
        <Texto variante="legenda">
          {`${trecho.quantidadeSinistrosGraves} acidentes graves${distancia ? `, ${distancia}` : ''}`}
        </Texto>
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
  );
}

/** Props do `TrechosCriticos`. */
export interface TrechosCriticosProps {
  estado: EstadoTrechos;
  posicao: EstadoPosicao;
}

/**
 * Dois cartões com a lista de trechos críticos (`GET /trechos-criticos?limite=50`, uma chamada só):
 * "Mais perigosos de São Paulo" (os 3 primeiros da cidade) e "Perto de você" (os 3 mais próximos
 * da posição do aparelho, entre os 50). A posição vem do GPS, só em primeiro plano, e fica só em
 * memória: a conta da distância é feita no celular, e nada vai para a API, o SQLite ou o log.
 */
export function TrechosCriticos({ estado, posicao }: TrechosCriticosProps) {
  const { espaco } = useTema();
  const { lista, origem, carregando } = estado;
  const { situacao } = posicao;

  const proximos =
    lista && situacao.tipo === 'pronta'
      ? lista.trechos
          .map((trecho) => ({
            trecho,
            km: distanciaKm(situacao.latitude, situacao.longitude, trecho.latitude, trecho.longitude),
          }))
          .sort((a, b) => a.km - b.km)
          .slice(0, POR_CARTAO)
      : null;

  return (
    <View style={{ gap: espaco.grande }}>
      <Cartao titulo="Mais perigosos de São Paulo">
        {origem === 'cache' && lista ? (
          <Aviso tipo="atencao" mensagem={`Lista salva em ${formatarDiaHora(lista.atualizadoEm)}.`} />
        ) : null}
        {origem === 'indisponivel' ? <Aviso tipo="atencao" mensagem="Lista indisponível agora." /> : null}
        {carregando && !lista ? <Texto cor="textoSecundario">Buscando os trechos...</Texto> : null}
        {lista ? (
          <>
            <Texto variante="legenda">{`Acidentes graves com moto de ${lista.periodo}.`}</Texto>
            {lista.trechos.slice(0, POR_CARTAO).map((trecho) => (
              <ItemTrecho key={trecho.posicao} trecho={trecho} km={null} />
            ))}
          </>
        ) : null}
      </Cartao>

      <Cartao titulo="Perto de você (entre os 50 trechos com mais acidentes graves da cidade)">
        {situacao.tipo === 'verificando' || situacao.tipo === 'buscando' ? (
          <Texto cor="textoSecundario">Procurando sua posição...</Texto>
        ) : null}
        {situacao.tipo === 'sem-permissao' ? (
          <>
            <Texto>
              Para mostrar os trechos perigosos perto de você, o HelmCare usa a localização do celular. Ela fica só neste
              aparelho: não vai para a API nem é guardada.
            </Texto>
            <Botao
              titulo={situacao.podePedir ? 'Permitir localização' : 'Abrir os ajustes do celular'}
              icone="location"
              aoPressionar={() => void posicao.pedirPermissao()}
              accessibilityHint={
                situacao.podePedir
                  ? 'Mostra o pedido de permissão do celular'
                  : 'A permissão foi negada: libere a localização nos ajustes do app'
              }
            />
          </>
        ) : null}
        {situacao.tipo === 'erro' ? (
          <>
            <Texto>Não deu para achar sua posição. Confira se a localização do celular está ligada.</Texto>
            <Botao titulo="Tentar de novo" variante="secundaria" icone="refresh" aoPressionar={() => void posicao.atualizar()} />
          </>
        ) : null}
        {situacao.tipo === 'pronta' && !lista ? <Texto cor="textoSecundario">Lista indisponível agora.</Texto> : null}
        {proximos?.map(({ trecho, km }) => (
          <ItemTrecho key={trecho.posicao} trecho={trecho} km={km} />
        ))}
      </Cartao>
    </View>
  );
}
