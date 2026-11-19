import { router } from 'expo-router';
import { useState } from 'react';
import { AccessibilityInfo, Alert, View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro } from '@/servicos/erros';
import type { ContatoResposta } from '@/tipos/contrato';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';

import { Aviso } from './Aviso';
import { Botao } from './Botao';
import { LinhaInfo } from './LinhaInfo';

/** Props do `ItemContato`. */
export interface ItemContatoProps {
  entregadorId: string;
  contato: ContatoResposta;
  /** É o único contato: a confirmação da exclusão avisa que ninguém mais será avisado. */
  ultimo: boolean;
  /** Nome e telefone escondidos ("•••••") até a pessoa tocar no olho. */
  oculto: boolean;
  /** Chamado depois de excluir (204) ou quando o contato já não existe (404): recarrega a lista. */
  aoMudar: () => void;
}

/**
 * Um contato de emergência, com "Editar" (abre o formulário com o nome e o parentesco) e
 * "Excluir" (com confirmação, `DELETE` 204). Usado no Perfil e na lista "Todos os contatos".
 * Com os dados escondidos, os botões dizem o parentesco no lugar do nome.
 */
export function ItemContato({ entregadorId, contato, ultimo, oculto, aoMudar }: ItemContatoProps) {
  const { espaco } = useTema();
  const [erro, setErro] = useState<string | null>(null);
  const [excluindo, setExcluindo] = useState(false);
  const quem = oculto ? `de ${contato.parentesco.toLowerCase()}` : contato.nome;

  async function excluir() {
    setErro(null);
    setExcluindo(true);
    try {
      await entregadorServico.excluirContato(entregadorId, contato.id);
      AccessibilityInfo.announceForAccessibility('Contato excluído');
      aoMudar();
    } catch (e) {
      const erroApi = converterErro(e);
      if (erroApi.tipo === 'nao-encontrado') {
        aoMudar();
      } else {
        setErro(erroApi.tipo === 'sem-conexao' ? MENSAGEM_SEM_CONEXAO : mensagemComum(erroApi));
      }
    } finally {
      setExcluindo(false);
    }
  }

  function confirmarExclusao() {
    Alert.alert(
      'Excluir contato?',
      `Excluir o contato ${contato.nome}? Ele deixa de ser avisado num SOS.${
        ultimo ? '\n\nSem contatos, ninguém da sua família é avisado num SOS.' : ''
      }`,
      [
        { text: 'Cancelar', style: 'cancel' },
        { text: 'Excluir', style: 'destructive', onPress: () => void excluir() },
      ],
    );
  }

  return (
    <View style={{ gap: espaco.pequeno }}>
      <LinhaInfo rotulo={contato.parentesco} valor={contato.nome} oculto={oculto} />
      <LinhaInfo rotulo="Telefone" valor={contato.telefone} mascarado oculto={oculto} />
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: espaco.pequeno }}>
        <View style={{ flexGrow: 1, flexBasis: '45%' }}>
          <Botao
            titulo="Editar"
            variante="secundaria"
            icone="create"
            aoPressionar={() => router.push({ pathname: '/contato', params: { contatoId: contato.id } })}
            accessibilityLabel={`Editar o contato ${quem}`}
          />
        </View>
        <View style={{ flexGrow: 1, flexBasis: '45%' }}>
          <Botao
            titulo="Excluir"
            variante="secundaria"
            icone="trash"
            aoPressionar={confirmarExclusao}
            carregando={excluindo}
            accessibilityLabel={`Excluir o contato ${quem}`}
            accessibilityHint="Pede confirmação antes de excluir"
          />
        </View>
      </View>
      {erro ? <Aviso tipo="erro" mensagem={erro} /> : null}
    </View>
  );
}
