import { useEffect, useState } from 'react';
import { AccessibilityInfo } from 'react-native';

/**
 * Verdadeiro quando o sistema pede para reduzir as animações ("Remover animações" no Android).
 * Acompanha a mudança enquanto o app está aberto. Até a primeira leitura, considera falso.
 */
export function useReduzirMovimento(): boolean {
  const [reduzir, setReduzir] = useState(false);
  useEffect(() => {
    let vivo = true;
    void AccessibilityInfo.isReduceMotionEnabled().then((valor) => {
      if (vivo) setReduzir(valor);
    });
    const assinatura = AccessibilityInfo.addEventListener('reduceMotionChanged', setReduzir);
    return () => {
      vivo = false;
      assinatura.remove();
    };
  }, []);
  return reduzir;
}
