import { router, useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';

import { BotaoOlho } from '@/componentes/BotaoOlho';
import { Botao } from '@/componentes/Botao';
import { Carregando } from '@/componentes/Carregando';
import { Cartao } from '@/componentes/Cartao';
import { EstadoErro } from '@/componentes/EstadoErro';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { ItemContato } from '@/componentes/ItemContato';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro, type ErroApi } from '@/servicos/erros';
import type { ContatoResposta } from '@/tipos/contrato';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';

/**
 * Todos os contatos de emergência (aberta pelo "Ver todos os contatos" do Perfil): um cartão por
 * contato, bem separados, cada um com "Editar" e "Excluir". Os dados começam escondidos, e o olho
 * do topo mostra os dados como a API devolve (telefone mascarado). Recarrega ao ganhar foco.
 */
export default function Contatos() {
  const { contaAtiva } = useSessao();
  const [contatos, setContatos] = useState<ContatoResposta[] | null>(null);
  const [erro, setErro] = useState<ErroApi | null>(null);
  const [visivel, setVisivel] = useState(false);
  const [atualizando, setAtualizando] = useState(false);

  const carregar = useCallback(async () => {
    if (!contaAtiva) {
      return;
    }
    try {
      setContatos((await entregadorServico.obter(contaAtiva)).contatos);
      setErro(null);
    } catch (e) {
      setErro(converterErro(e));
    } finally {
      setAtualizando(false);
    }
  }, [contaAtiva]);

  // Ao abrir e ao voltar da edição de um contato.
  useFocusEffect(
    useCallback(() => {
      void carregar();
    }, [carregar]),
  );

  if (!contaAtiva) {
    return (
      <Tela>
        <EstadoVazio titulo="Nenhuma conta neste aparelho" icone="person-outline" />
      </Tela>
    );
  }
  if (contatos === null && !erro) {
    return <Carregando mensagem="Abrindo seus contatos..." />;
  }

  return (
    <Tela
      aoAtualizar={() => {
        setAtualizando(true);
        void carregar();
      }}
      atualizando={atualizando}
    >
      <Cartao
        titulo={contatos ? `${contatos.length} ${contatos.length === 1 ? 'contato' : 'contatos'}` : 'Contatos'}
        acao={<BotaoOlho visivel={visivel} aoAlternar={() => setVisivel((atual) => !atual)} />}
      >
        <Texto cor="textoSecundario">Eles são avisados se você cair.</Texto>
      </Cartao>
      {erro ? (
        <EstadoErro
          mensagem={erro.tipo === 'sem-conexao' ? MENSAGEM_SEM_CONEXAO : mensagemComum(erro)}
          aoTentarDeNovo={() => void carregar()}
        />
      ) : null}
      {contatos !== null && contatos.length === 0 ? (
        <EstadoVazio titulo="Nenhum contato ainda" mensagem="Sem contatos, ninguém da sua família é avisado num SOS." />
      ) : null}
      {contatos?.map((contato) => (
        <Cartao key={contato.id}>
          <ItemContato
            entregadorId={contaAtiva}
            contato={contato}
            ultimo={contatos.length === 1}
            oculto={!visivel}
            aoMudar={() => void carregar()}
          />
        </Cartao>
      ))}
      <Botao titulo="Adicionar contato" icone="person-add" aoPressionar={() => router.push('/contato')} />
    </Tela>
  );
}
