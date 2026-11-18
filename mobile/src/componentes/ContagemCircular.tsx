import { useEffect, useState } from 'react';
import { Animated, Easing, useWindowDimensions, View } from 'react-native';
import Svg, { Circle } from 'react-native-svg';

import { useTema } from '@/contextos/TemaContext';
import { useReduzirMovimento } from '@/hooks/useReduzirMovimento';

import { Texto } from './Texto';

/** Círculo do SVG com as props animáveis pelo Animated (o traço que esvazia). */
const CirculoAnimado = Animated.createAnimatedComponent(Circle);

/** Espessura do anel, em dp. */
const ESPESSURA = 16;

/** Props da `ContagemCircular`. */
export interface ContagemCircularProps {
  /** Segundos totais do anel (10 na verificação, 15 na contagem). */
  totalS: number;
  /** Instante (relógio do app, ms) em que a contagem começou. */
  inicioMs: number;
  /** Segundos que faltam, já contados no relógio do app (o número do centro). */
  restante: number;
  /** Cor do anel (texto e ícone da tela continuam dizendo o estado). */
  cor: string;
  /** Cor do número do centro. */
  corTexto: string;
  /** Rótulo do número para o leitor de tela (ex.: "12 segundos para cancelar"). */
  rotulo: string;
}

/**
 * Anel de contagem: começa cheio e esvazia no sentido horário enquanto o tempo passa (o padrão dos
 * timers de celular), com o número de segundos no centro. A animação é contínua; com o "remover
 * animações" do sistema ligado, o anel só pula de segundo em segundo. O anel é só visual e fica fora
 * do leitor de tela: o número tem o rótulo. O tamanho cresce com a fonte, para o número caber.
 */
export function ContagemCircular({ totalS, inicioMs, restante, cor, corTexto, rotulo }: ContagemCircularProps) {
  const { tipografia, espaco } = useTema();
  const { width, fontScale } = useWindowDimensions();
  const reduzirMovimento = useReduzirMovimento();
  // O anel cabe na largura da tela e cresce com a fonte do número (o maior texto do app).
  const lado = Math.min(width - espaco.medio * 2, Math.max(240, tipografia.contagem.fontSize * fontScale * 1.9));
  const raioCirculo = (lado - ESPESSURA) / 2;
  const circunferencia = 2 * Math.PI * raioCirculo;
  // Fração já passada (0 = cheio, 1 = vazio), criada uma vez.
  const [passado] = useState(() => new Animated.Value(0));

  // Animação contínua do instante atual até o fim, no relógio do app.
  useEffect(() => {
    if (reduzirMovimento) {
      return;
    }
    const totalMs = totalS * 1000;
    const decorrido = Math.min(totalMs, Math.max(0, Date.now() - inicioMs));
    passado.setValue(decorrido / totalMs);
    const animacao = Animated.timing(passado, {
      toValue: 1,
      duration: totalMs - decorrido,
      easing: Easing.linear,
      // As props do SVG não passam pelo driver nativo.
      useNativeDriver: false,
    });
    animacao.start();
    return () => animacao.stop();
  }, [inicioMs, totalS, reduzirMovimento, passado]);

  // Sem animação: o anel pula a cada segundo, pelo número que falta.
  useEffect(() => {
    if (reduzirMovimento) {
      passado.setValue((totalS - restante) / totalS);
    }
  }, [reduzirMovimento, restante, totalS, passado]);

  const deslocamento = passado.interpolate({ inputRange: [0, 1], outputRange: [0, circunferencia] });

  return (
    <View style={{ width: lado, height: lado, alignItems: 'center', justifyContent: 'center', alignSelf: 'center' }}>
      <View
        accessible={false}
        importantForAccessibility="no-hide-descendants"
        accessibilityElementsHidden
        style={{ position: 'absolute' }}
      >
        {/* Girado -90 graus: o traço começa no topo e, com o deslocamento crescendo, a parte cheia
            some no sentido horário. A trilha é a mesma cor com transparência. */}
        <Svg width={lado} height={lado} style={{ transform: [{ rotate: '-90deg' }] }}>
          <Circle
            cx={lado / 2}
            cy={lado / 2}
            r={raioCirculo}
            stroke={cor}
            strokeOpacity={0.2}
            strokeWidth={ESPESSURA}
            fill="none"
          />
          <CirculoAnimado
            cx={lado / 2}
            cy={lado / 2}
            r={raioCirculo}
            stroke={cor}
            strokeWidth={ESPESSURA}
            strokeLinecap="butt"
            strokeDasharray={`${circunferencia} ${circunferencia}`}
            strokeDashoffset={deslocamento}
            fill="none"
          />
        </Svg>
      </View>
      <Texto variante="contagem" corPersonalizada={corTexto} centralizado accessibilityLabel={rotulo}>
        {String(restante)}
      </Texto>
    </View>
  );
}
