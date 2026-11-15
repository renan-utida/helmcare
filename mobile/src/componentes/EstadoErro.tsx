import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Botao } from './Botao';
import { Icone } from './Icone';
import { Texto } from './Texto';

/** Props do `EstadoErro`. */
export interface EstadoErroProps {
  /** Frase simples explicando o que deu errado. */
  mensagem: string;
  /** Ação do botão "Tentar de novo". */
  aoTentarDeNovo: () => void;
  /** Mostra o botão em modo carregando enquanto tenta. */
  tentando?: boolean;
}

/** Tela de erro com ícone, mensagem anunciada e o botão "Tentar de novo". */
export function EstadoErro({ mensagem, aoTentarDeNovo, tentando = false }: EstadoErroProps) {
  const { cores, espaco } = useTema();
  return (
    <View style={{ alignItems: 'center', gap: espaco.medio, padding: espaco.grande }}>
      <Icone nome="cloud-offline" cor={cores.erro} tamanho={48} />
      <Texto
        centralizado
        accessibilityRole="alert"
        accessibilityLiveRegion="assertive"
        accessibilityLabel={`Erro: ${mensagem}`}
      >
        {mensagem}
      </Texto>
      <View style={{ alignSelf: 'stretch' }}>
        <Botao titulo="Tentar de novo" icone="refresh" aoPressionar={aoTentarDeNovo} carregando={tentando} />
      </View>
    </View>
  );
}
