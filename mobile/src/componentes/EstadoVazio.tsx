import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Icone, type NomeIcone } from './Icone';
import { Texto } from './Texto';

/** Props do `EstadoVazio`. */
export interface EstadoVazioProps {
  titulo: string;
  mensagem?: string;
  icone?: NomeIcone;
}

/** Tela ou bloco sem conteúdo, com ícone, título e uma frase de explicação. */
export function EstadoVazio({ titulo, mensagem, icone = 'file-tray-outline' }: EstadoVazioProps) {
  const { cores, espaco } = useTema();
  return (
    <View style={{ alignItems: 'center', gap: espaco.medio, padding: espaco.grande }}>
      <Icone nome={icone} cor={cores.textoSecundario} tamanho={48} />
      <Texto variante="subtitulo" centralizado>
        {titulo}
      </Texto>
      {mensagem ? (
        <Texto cor="textoSecundario" centralizado>
          {mensagem}
        </Texto>
      ) : null}
    </View>
  );
}
