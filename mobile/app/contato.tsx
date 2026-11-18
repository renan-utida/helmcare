import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useCallback, useEffect, useState } from 'react';
import { AccessibilityInfo } from 'react-native';

import { Carregando } from '@/componentes/Carregando';
import { EstadoErro } from '@/componentes/EstadoErro';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { FormularioContato } from '@/componentes/FormularioContato';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro } from '@/servicos/erros';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';

/** Contato a editar: o nome e o parentesco atuais (o telefone só vem mascarado e fica de fora). */
type Edicao =
  | { tipo: 'carregando' }
  | { tipo: 'pronto'; nome: string; parentesco: string }
  | { tipo: 'nao-encontrado' }
  | { tipo: 'erro'; mensagem: string };

/**
 * Modal do contato de emergência. Sem parâmetro, adiciona um contato (o mesmo formulário do passo 2
 * do cadastro). Com `contatoId` (só o id, nunca dado pessoal na rota), edita: lê o contato no
 * `GET /entregadores/{id}` para preencher o nome e o parentesco e envia o `PUT` com o corpo inteiro.
 * Depois do 200 ou do 201, volta ao Perfil, que recarrega a lista ao ganhar foco.
 */
export default function Contato() {
  const { contaAtiva } = useSessao();
  const { contatoId } = useLocalSearchParams<{ contatoId?: string }>();
  const [edicao, setEdicao] = useState<Edicao>({ tipo: 'carregando' });

  const carregar = useCallback(async () => {
    if (!contaAtiva || !contatoId) {
      return;
    }
    try {
      const entregador = await entregadorServico.obter(contaAtiva);
      const contato = entregador.contatos.find((item) => item.id === contatoId);
      setEdicao(contato ? { tipo: 'pronto', nome: contato.nome, parentesco: contato.parentesco } : { tipo: 'nao-encontrado' });
    } catch (e) {
      const erro = converterErro(e);
      setEdicao({ tipo: 'erro', mensagem: erro.tipo === 'sem-conexao' ? MENSAGEM_SEM_CONEXAO : mensagemComum(erro) });
    }
  }, [contaAtiva, contatoId]);

  useEffect(() => {
    let vivo = true;
    (async () => {
      // Espera um passo antes de mexer no estado (regra react-hooks/set-state-in-effect).
      await Promise.resolve();
      if (vivo) {
        await carregar();
      }
    })();
    return () => {
      vivo = false;
    };
  }, [carregar]);

  if (!contaAtiva) {
    return (
      <Tela>
        <EstadoVazio titulo="Nenhuma conta neste aparelho" icone="person-outline" />
      </Tela>
    );
  }

  if (!contatoId) {
    return (
      <Tela>
        <Texto>Quem devemos avisar se você cair? Essa pessoa recebe o aviso do SOS.</Texto>
        <FormularioContato
          entregadorId={contaAtiva}
          aoSalvar={() => {
            AccessibilityInfo.announceForAccessibility('Contato salvo');
            router.back();
          }}
          aoPular={() => router.back()}
          textoPular="Cancelar"
        />
      </Tela>
    );
  }

  return (
    <>
      <Stack.Screen options={{ title: 'Editar contato' }} />
      {edicao.tipo === 'carregando' ? <Carregando mensagem="Abrindo o contato..." /> : null}
      {edicao.tipo === 'erro' ? (
        <Tela>
          <EstadoErro
            mensagem={edicao.mensagem}
            aoTentarDeNovo={() => {
              setEdicao({ tipo: 'carregando' });
              void carregar();
            }}
          />
        </Tela>
      ) : null}
      {edicao.tipo === 'nao-encontrado' ? (
        <Tela>
          <EstadoVazio titulo="Este contato não existe mais." mensagem="Volte ao Perfil para ver a lista atualizada." />
        </Tela>
      ) : null}
      {edicao.tipo === 'pronto' ? (
        <Tela>
          <FormularioContato
            entregadorId={contaAtiva}
            contatoId={contatoId}
            inicial={{ nome: edicao.nome, parentesco: edicao.parentesco }}
            aoSalvar={() => {
              AccessibilityInfo.announceForAccessibility('Contato atualizado');
              router.back();
            }}
            aoPular={() => router.back()}
            textoPular="Cancelar"
          />
        </Tela>
      ) : null}
    </>
  );
}
