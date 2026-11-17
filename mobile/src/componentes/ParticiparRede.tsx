import { useState } from 'react';
import { AccessibilityInfo, View } from 'react-native';

import { useChamados } from '@/contextos/ChamadosContext';
import { useTema } from '@/contextos/TemaContext';
import { converterErro, type ErroApi } from '@/servicos/erros';
import { mensagemComum } from '@/utils/mensagensErro';

import { Aviso } from './Aviso';
import { Botao } from './Botao';
import { Texto } from './Texto';

/** Mensagem de cada erro do `POST /voluntarios`, decidida pelo status HTTP. */
function mensagemDoErro(erro: ErroApi): string {
  switch (erro.tipo) {
    case 'conflito':
      // A API não tem rota para recuperar o voluntarioId (limitação documentada: o login resolve).
      return 'Este cadastro já é voluntário, mas o id não está neste aparelho. Por enquanto, não dá para recuperar a participação neste celular. Isso muda quando o app tiver login.';
    case 'nao-encontrado':
      return 'Este cadastro não foi encontrado na API. Confira o perfil nos Ajustes.';
    default:
      return mensagemComum(erro);
  }
}

/**
 * Explicação simples da rede de socorro e o botão "Fazer parte da rede de socorro"
 * (`POST /voluntarios`, tipo ENTREGADOR). No 201, o `voluntarioId` vai para o perfil local.
 * Usado na aba Chamados e no Perfil.
 */
export function ParticiparRede() {
  const { espaco } = useTema();
  const { participar } = useChamados();
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function entrar() {
    setEnviando(true);
    setErro(null);
    try {
      await participar();
      AccessibilityInfo.announceForAccessibility('Pronto: você faz parte da rede de socorro.');
    } catch (e) {
      setErro(mensagemDoErro(converterErro(e)));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <View style={{ gap: espaco.medio }}>
      <Texto>
        Quando um entregador cai perto de você, o HelmCare pede ajuda à rede. Você recebe o chamado, decide se pode
        ir e, se aceitar, vê onde a pessoa está.
      </Texto>
      <Texto cor="textoSecundario">
        Primeiro são chamados os voluntários a até 2 km. Se ninguém aceitar em 1 minuto, toda a rede é chamada.
      </Texto>
      {erro ? <Aviso tipo="erro" mensagem={erro} /> : null}
      <Botao
        titulo="Fazer parte da rede de socorro"
        icone="people"
        aoPressionar={() => void entrar()}
        carregando={enviando}
        accessibilityHint="Cadastra você como voluntário. Você começa indisponível."
      />
    </View>
  );
}
