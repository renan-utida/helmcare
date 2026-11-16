import { Image, View } from 'react-native';

import logoClaro from '../../assets/marca/logo-horizontal.png';
import logoEscuro from '../../assets/marca/logo-horizontal-escuro.png';
import simbolo from '../../assets/marca/simbolo-transparente.png';
import { useTema } from '@/contextos/TemaContext';

/** Proporção do logo horizontal (2243 x 349 px), para a altura acompanhar a largura sem distorcer. */
const PROPORCAO_LOGO = 2243 / 349;
/** Proporção do símbolo (491 x 391 px). */
const PROPORCAO_SIMBOLO = 491 / 391;
/** Altura do símbolo no cabeçalho, em dp. */
const ALTURA_SIMBOLO = 32;

/**
 * Logo horizontal do HelmCare: a versão clara no tema claro e a escura no tema escuro.
 * Os dois PNGs têm fundo próprio (quase branco e azul-marinho #001C45), iguais ao fundo
 * da tela em cada tema, por isso o logo fica direto sobre o fundo.
 * Ocupa 80% da largura disponível, até 360 dp, e a altura sai da proporção da imagem.
 */
export function LogoHorizontal() {
  const { esquema } = useTema();
  return (
    <Image
      source={esquema === 'escuro' ? logoEscuro : logoClaro}
      accessible
      accessibilityRole="image"
      accessibilityLabel="HelmCare"
      resizeMode="contain"
      style={{ width: '80%', maxWidth: 360, aspectRatio: PROPORCAO_LOGO, alignSelf: 'center' }}
    />
  );
}

/**
 * Símbolo do capacete com cerca de 32 dp, para o cabeçalho. O azul-marinho do símbolo
 * fica com 1,21:1 sobre o cabeçalho escuro (#0B2C5E), quase invisível; por isso, no tema
 * escuro, ele ganha um fundo branco arredondado (símbolo 16,54:1 sobre o branco e o
 * branco 13,67:1 sobre o cabeçalho). No tema claro, o símbolo fica direto (15,14:1).
 */
export function SimboloMarca() {
  const { cores, raio, espaco } = useTema();
  const comFundo = cores.fundoSimbolo !== null;
  return (
    <View
      accessible
      accessibilityRole="image"
      accessibilityLabel="HelmCare"
      style={{
        backgroundColor: cores.fundoSimbolo ?? 'transparent',
        borderRadius: raio.pequeno,
        padding: comFundo ? espaco.pequeno / 2 : 0,
      }}
    >
      <Image
        source={simbolo}
        resizeMode="contain"
        style={{ height: ALTURA_SIMBOLO, width: ALTURA_SIMBOLO * PROPORCAO_SIMBOLO }}
      />
    </View>
  );
}
