import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import { regrasDaSenha } from '@/utils/validacao';

import { Icone } from './Icone';
import { Texto } from './Texto';

/** Props do `RegrasSenha`. */
export interface RegrasSenhaProps {
  senha: string;
}

/**
 * Regras da senha (seção 3) marcadas enquanto a pessoa digita: de 8 a 64 caracteres, uma letra
 * maiúscula, uma minúscula e um número. Cada regra tem ícone e texto diferentes quando é atendida,
 * e o leitor de tela diz "atendida" ou "ainda não atendida": a cor nunca é a única pista.
 */
export function RegrasSenha({ senha }: RegrasSenhaProps) {
  const { cores, espaco } = useTema();
  const regras = regrasDaSenha(senha);
  return (
    <View style={{ gap: espaco.pequeno }}>
      <Texto variante="legenda">A senha precisa ter:</Texto>
      {regras.map((regra) => (
        <View
          key={regra.texto}
          accessible
          accessibilityLabel={`${regra.texto}: ${regra.atendida ? 'atendida' : 'ainda não atendida'}`}
          style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}
        >
          <Icone
            nome={regra.atendida ? 'checkmark-circle' : 'ellipse-outline'}
            cor={regra.atendida ? cores.aviso.sucesso.icone : cores.textoSecundario}
          />
          <Texto style={{ flex: 1 }} variante={regra.atendida ? 'rotulo' : 'corpo'}>
            {regra.atendida ? `${regra.texto} (pronto)` : regra.texto}
          </Texto>
        </View>
      ))}
    </View>
  );
}
