import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import type { ClimaResposta } from '@/tipos/contrato';
import { formatarDecimal, formatarHoraComH } from '@/utils/datas';

import { Cartao } from './Cartao';
import { Icone, type NomeIcone } from './Icone';
import { Texto } from './Texto';

/** Chuva em palavras: zero comparado como número (a API manda 0, não "0.0"). */
function textoChuva(chuvaMmHora: number): string {
  return chuvaMmHora === 0 ? 'Sem chuva' : `${formatarDecimal(chuvaMmHora)} mm por hora`;
}

/** Uma medida do cartão: ícone, rótulo e valor. */
function Medida({ icone, rotulo, valor }: { icone: NomeIcone; rotulo: string; valor: string }) {
  const { cores, espaco } = useTema();
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno, flexBasis: '45%', flexGrow: 1 }}>
      <Icone nome={icone} cor={cores.textoSecundario} />
      <View style={{ flex: 1 }}>
        <Texto variante="legenda">{rotulo}</Texto>
        <Texto variante="rotulo">{valor}</Texto>
      </View>
    </View>
  );
}

/** Props do `CartaoClima`. */
export interface CartaoClimaProps {
  clima: ClimaResposta;
}

/**
 * Cartão "Clima agora" (`GET /clima`, contrato 1.17): temperatura, sensação pelo índice de calor,
 * umidade e chuva, com o horário da medição. Com `alertaCalor` verdadeiro (decidido pela API, o
 * app não recalcula), o destaque do calor forte. No rodapé, sempre, o crédito "Dados: Open-Meteo"
 * (licença CC BY 4.0). Para o leitor de tela, uma frase só com tudo.
 */
export function CartaoClima({ clima }: CartaoClimaProps) {
  const { cores, espaco, raio } = useTema();
  const temperatura = `${formatarDecimal(clima.temperaturaC)} °C`;
  const sensacao = `${formatarDecimal(clima.indiceCalorC)} °C`;
  const umidade = `${clima.umidadePct}%`;
  const chuva = textoChuva(clima.chuvaMmHora);
  const hora = formatarHoraComH(clima.atualizadoEm);
  const leitura =
    `Clima agora: ${formatarDecimal(clima.temperaturaC)} graus, sensação de ${formatarDecimal(clima.indiceCalorC)} graus, ` +
    `umidade de ${clima.umidadePct} por cento, ${chuva.toLowerCase().replace('mm', 'milímetros')}. ` +
    `${clima.alertaCalor ? 'Calor forte: faça pausas e beba água. ' : ''}Atualizado às ${hora}. Dados: ${clima.fonte}.`;
  return (
    <Cartao>
      <View accessible accessibilityLabel={leitura} style={{ gap: espaco.medio }}>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}>
          <Icone nome="partly-sunny" cor={cores.textoDestaque} tamanho={28} />
          <Texto variante="subtitulo" style={{ flex: 1 }}>
            Clima agora
          </Texto>
          <Texto variante="destaque">{temperatura}</Texto>
        </View>
        {clima.alertaCalor ? (
          <View
            style={{
              flexDirection: 'row',
              alignItems: 'center',
              gap: espaco.pequeno,
              backgroundColor: cores.aviso.atencao.fundo,
              borderRadius: raio.pequeno,
              padding: espaco.medio,
            }}
          >
            <Icone nome="sunny" cor={cores.aviso.atencao.icone} />
            <Texto variante="rotulo" corPersonalizada={cores.aviso.atencao.texto} style={{ flex: 1 }}>
              Calor forte: faça pausas e beba água
            </Texto>
          </View>
        ) : null}
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: espaco.medio }}>
          <Medida icone="thermometer" rotulo="Sensação" valor={sensacao} />
          <Medida icone="water" rotulo="Umidade" valor={umidade} />
          <Medida icone="rainy" rotulo="Chuva" valor={chuva} />
        </View>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between', gap: espaco.pequeno }}>
          <Texto variante="legenda">{`Atualizado às ${hora}`}</Texto>
          <Texto variante="legenda">{`Dados: ${clima.fonte}`}</Texto>
        </View>
      </View>
    </Cartao>
  );
}
