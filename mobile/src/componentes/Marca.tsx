import { useWindowDimensions, View } from 'react-native';

import LogoClaro from '../../assets/marca/logo-horizontal.svg';
import LogoEscuro from '../../assets/marca/logo-horizontal-escuro.svg';
import SimboloClaro from '../../assets/marca/simbolo.svg';
import SimboloEscuro from '../../assets/marca/simbolo-escuro.svg';
import { useTema } from '@/contextos/TemaContext';

/*
 * Exceção à regra "cores só pelo tema": as cores do logo e do símbolo são as da arte
 * da marca e ficam dentro dos próprios SVGs (azul-marinho #001D48, laranja #FE7801 e,
 * nas versões escuras, branco). O tema só escolhe qual arquivo usar.
 */

/** Proporção do viewBox do logo claro (1526 x 304) e do escuro (1526 x 302). */
const PROPORCAO_LOGO = { claro: 1526 / 304, escuro: 1526 / 302 };
/** Proporção do viewBox do símbolo claro (419 x 304) e do escuro (419 x 302). */
const PROPORCAO_SIMBOLO = { claro: 419 / 304, escuro: 419 / 302 };
/** Largura máxima do logo, em dp. */
const LARGURA_MAXIMA_LOGO = 320;
/** Altura do símbolo no cabeçalho, em dp. */
const ALTURA_SIMBOLO = 32;

/**
 * Logo horizontal do HelmCare, claro ou escuro pelo tema, sem fundo próprio.
 * O tamanho sai em números (não em porcentagem): largura = mín(80% da janela, 320 dp)
 * e altura = largura ÷ proporção do viewBox. Assim o logo ocupa só o espaço dele,
 * sem sobra em cima e embaixo. O desenho não muda com a fonte do sistema (não é texto).
 */
export function LogoHorizontal() {
  const { esquema } = useTema();
  const { width } = useWindowDimensions();
  const largura = Math.min(width * 0.8, LARGURA_MAXIMA_LOGO);
  const altura = largura / PROPORCAO_LOGO[esquema];
  const Logo = esquema === 'escuro' ? LogoEscuro : LogoClaro;
  return (
    <View accessible accessibilityRole="image" accessibilityLabel="HelmCare" style={{ alignSelf: 'center' }}>
      <Logo width={largura} height={altura} />
    </View>
  );
}

/**
 * Símbolo do capacete com 32 dp de altura, para o cabeçalho. No tema escuro, o capacete é
 * branco e aparece direto sobre a superfície do cabeçalho; no claro, é azul-marinho.
 */
export function SimboloMarca() {
  const { esquema } = useTema();
  const Simbolo = esquema === 'escuro' ? SimboloEscuro : SimboloClaro;
  return (
    <View accessible accessibilityRole="image" accessibilityLabel="HelmCare">
      <Simbolo width={ALTURA_SIMBOLO * PROPORCAO_SIMBOLO[esquema]} height={ALTURA_SIMBOLO} />
    </View>
  );
}
