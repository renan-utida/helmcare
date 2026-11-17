/**
 * Permite `import Logo from '.../logo.svg'` com tipo. O react-native-svg-transformer
 * (configurado no metro.config.js) transforma cada SVG num componente React do react-native-svg.
 */
declare module '*.svg' {
  import type { FC } from 'react';
  import type { SvgProps } from 'react-native-svg';

  const componente: FC<SvgProps>;
  export default componente;
}
