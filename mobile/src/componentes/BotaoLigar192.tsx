import { Linking } from 'react-native';

import { Botao } from './Botao';

/** Abre o discador com o 192; a ligação só começa se a pessoa tocar em ligar. */
function ligar192() {
  void Linking.openURL('tel:192');
}

/**
 * Botão "Ligar 192 (SAMU)", grande (72 dp) e vermelho, para a vítima e para o voluntário,
 * sempre visível enquanto a ocorrência está ativa.
 */
export function BotaoLigar192() {
  return (
    <Botao
      titulo="Ligar 192 (SAMU)"
      variante="perigo"
      icone="call"
      alturaMinima={72}
      aoPressionar={ligar192}
      accessibilityHint="Abre o discador com o número 192. A ligação só começa se você tocar em ligar."
    />
  );
}
