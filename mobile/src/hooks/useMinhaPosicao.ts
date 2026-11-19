import {
  Accuracy,
  getCurrentPositionAsync,
  getForegroundPermissionsAsync,
  requestForegroundPermissionsAsync,
} from 'expo-location';
import { useCallback, useEffect, useState } from 'react';
import { Linking } from 'react-native';

/** Situação da posição do aparelho para o cartão "Perto de você". */
export type SituacaoPosicao =
  | { tipo: 'verificando' }
  | { tipo: 'sem-permissao'; podePedir: boolean }
  | { tipo: 'buscando' }
  | { tipo: 'pronta'; latitude: number; longitude: number }
  | { tipo: 'erro' };

/** O que o hook entrega. */
export interface EstadoPosicao {
  situacao: SituacaoPosicao;
  /** Pede a permissão (ou, se o Android não deixa pedir de novo, abre os ajustes do app). */
  pedirPermissao: () => Promise<void>;
  /** Lê a posição de novo (ao puxar para atualizar). */
  atualizar: () => Promise<void>;
}

/**
 * Posição do aparelho pelo GPS (expo-location), só com a permissão de primeiro plano. A posição
 * fica SÓ em memória, no estado deste hook: nunca vai para o SQLite, para log nem para a API. Serve
 * só para ordenar os trechos críticos por distância, no próprio celular. Ao abrir, só confere a
 * permissão: quem pede é a pessoa, pelo botão do cartão, depois de ler o motivo.
 */
export function useMinhaPosicao(): EstadoPosicao {
  const [situacao, setSituacao] = useState<SituacaoPosicao>({ tipo: 'verificando' });

  const lerPosicao = useCallback(async () => {
    setSituacao({ tipo: 'buscando' });
    try {
      const { coords } = await getCurrentPositionAsync({ accuracy: Accuracy.Balanced });
      setSituacao({ tipo: 'pronta', latitude: coords.latitude, longitude: coords.longitude });
    } catch {
      // GPS desligado ou sem sinal: o cartão explica e oferece tentar de novo.
      setSituacao({ tipo: 'erro' });
    }
  }, []);

  // Ao abrir: com a permissão já dada, lê a posição; sem ela, o cartão mostra o motivo e o botão.
  useEffect(() => {
    let vivo = true;
    (async () => {
      const { granted, canAskAgain } = await getForegroundPermissionsAsync();
      if (!vivo) {
        return;
      }
      if (granted) {
        await lerPosicao();
      } else {
        setSituacao({ tipo: 'sem-permissao', podePedir: canAskAgain });
      }
    })();
    return () => {
      vivo = false;
    };
  }, [lerPosicao]);

  const pedirPermissao = useCallback(async () => {
    const atual = await getForegroundPermissionsAsync();
    if (!atual.granted && !atual.canAskAgain) {
      // O Android não mostra mais o pedido: só os ajustes do app liberam a localização.
      await Linking.openSettings();
      return;
    }
    const { granted, canAskAgain } = await requestForegroundPermissionsAsync();
    if (granted) {
      await lerPosicao();
    } else {
      setSituacao({ tipo: 'sem-permissao', podePedir: canAskAgain });
    }
  }, [lerPosicao]);

  const atualizar = useCallback(async () => {
    const { granted } = await getForegroundPermissionsAsync();
    if (granted) {
      await lerPosicao();
    }
  }, [lerPosicao]);

  return { situacao, pedirPermissao, atualizar };
}
