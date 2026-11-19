import { router, useFocusEffect, useLocalSearchParams } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { AccessibilityInfo, View, type ScrollViewInstance } from 'react-native';

import { Botao } from '@/componentes/Botao';
import { BotaoOlho } from '@/componentes/BotaoOlho';
import { Carregando } from '@/componentes/Carregando';
import { Cartao } from '@/componentes/Cartao';
import { EstadoErro } from '@/componentes/EstadoErro';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { FormularioCapacete } from '@/componentes/FormularioCapacete';
import { Icone } from '@/componentes/Icone';
import { ItemContato } from '@/componentes/ItemContato';
import { LinhaInfo } from '@/componentes/LinhaInfo';
import { ParticiparRede } from '@/componentes/ParticiparRede';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useChamados } from '@/contextos/ChamadosContext';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro, type ErroApi } from '@/servicos/erros';
import type { EntregadorResposta } from '@/tipos/contrato';
import { retirarDoFluxo } from '@/utils/memoriaDoFluxo';
import { mensagemComum } from '@/utils/mensagensErro';
import { comecarPilhaEm } from '@/utils/navegacao';

/** Mensagem de erro do Perfil para cada tipo de erro da API. */
function mensagemDoErro(erro: ErroApi): string {
  if (erro.tipo === 'nao-encontrado') {
    return 'Este cadastro não foi encontrado na API. Troque de conta pelo menu da conta.';
  }
  return mensagemComum(erro);
}

/**
 * Perfil do entregador: dados do `GET /entregadores/{id}` como a API devolve (mascarados, com o
 * e-mail), "Trocar telefone", o capacete da sessão, a ficha médica ("Ver ficha médica" e "Quem
 * acessou minha ficha"), a rede de socorro e os contatos (adicionar, editar e excluir).
 * Recarrega ao voltar dos modais; depois da troca de telefone, usa o corpo do 200 sem nova consulta.
 */
export default function Perfil() {
  const { cores, espaco } = useTema();
  const { contaAtiva, sessao } = useSessao();
  const { voluntarioId, rede } = useChamados();
  const entregadorId = contaAtiva;
  const [resposta, setResposta] = useState<EntregadorResposta | null>(null);
  // O erro guarda de qual entregador ele é, para não aparecer no perfil seguinte.
  const [falha, setFalha] = useState<{ entregadorId: string; erro: ErroApi } | null>(null);
  const [atualizando, setAtualizando] = useState(false);
  // Os contatos começam escondidos ("•••••"); o olho do cartão mostra como a API devolve.
  const [contatosVisiveis, setContatosVisiveis] = useState(false);
  // Atalho "Contatos de emergência" da Início: rola até a seção dos contatos, uma vez por pedido.
  const { secao, pedido } = useLocalSearchParams<{ secao?: string; pedido?: string }>();
  const rolagem = useRef<ScrollViewInstance | null>(null);
  const [posicaoContatos, setPosicaoContatos] = useState<number | null>(null);
  const pedidoAtendido = useRef<string | null>(null);
  useEffect(() => {
    if (secao !== 'contatos' || !pedido || pedido === pedidoAtendido.current || posicaoContatos === null) {
      return;
    }
    pedidoAtendido.current = pedido;
    rolagem.current?.scrollTo({ y: posicaoContatos, animated: true });
  }, [secao, pedido, posicaoContatos]);

  // Só vale o que é da conta em uso: ao trocar de conta, a tela volta a "Carregando".
  const primeiroContato = resposta && resposta.id === entregadorId ? (resposta.contatos[0] ?? null) : null;
  const dados = resposta && resposta.id === entregadorId ? resposta : null;
  const erro = falha && falha.entregadorId === entregadorId ? falha.erro : null;

  const carregar = useCallback(async () => {
    if (!entregadorId) {
      return;
    }
    try {
      setResposta(await entregadorServico.obter(entregadorId));
      setFalha(null);
    } catch (e) {
      setFalha({ entregadorId, erro: converterErro(e) });
    } finally {
      setAtualizando(false);
    }
  }, [entregadorId]);

  // Recarrega sempre que a aba ganha foco: ao abrir e ao voltar da ficha ou do contato. Na volta
  // da troca de telefone, o 200 já trouxe os dados (pela memória do fluxo, nunca pela rota).
  useFocusEffect(
    useCallback(() => {
      const atualizado = retirarDoFluxo('entregadorAtualizado');
      if (atualizado && atualizado.id === entregadorId) {
        setResposta(atualizado);
        setFalha(null);
        return;
      }
      void carregar();
    }, [carregar, entregadorId]),
  );

  function atualizar() {
    setAtualizando(true);
    void carregar();
  }

  if (!entregadorId) {
    return (
      <Tela dentroDeAbas>
        <EstadoVazio titulo="Nenhuma conta neste aparelho" icone="person-outline" />
        <Botao titulo="Entrar" aoPressionar={() => comecarPilhaEm('/login')} />
      </Tela>
    );
  }

  if (!dados && !erro) {
    return <Carregando mensagem="Carregando seu perfil..." />;
  }

  if (erro && !dados) {
    return (
      <Tela dentroDeAbas aoAtualizar={atualizar} atualizando={atualizando}>
        <EstadoErro mensagem={mensagemDoErro(erro)} aoTentarDeNovo={atualizar} tentando={atualizando} />
        <Botao titulo="Ver as contas" variante="secundaria" aoPressionar={() => router.push('/contas')} />
      </Tela>
    );
  }

  return (
    <Tela dentroDeAbas aoAtualizar={atualizar} atualizando={atualizando} refRolagem={rolagem}>
      <Texto variante="titulo">{dados?.nome ?? sessao?.nome ?? 'Seu perfil'}</Texto>
      {erro ? (
        <Texto variante="legenda" cor="erro" accessibilityLiveRegion="polite">
          {`Não foi possível atualizar. ${mensagemDoErro(erro)}`}
        </Texto>
      ) : null}

      {dados ? (
        <>
          {/* Sem olho: a API só devolve CPF, CNH, telefone e e-mail mascarados (seção 9), e o app
              nunca desmascara, então não há o que mostrar a mais. Ver os dados completos precisa
              de rota nova na API (melhoria futura). */}
          <Cartao titulo="Seus dados">
            <Texto variante="legenda">
              Por segurança, o app mostra só uma parte dos seus documentos, do telefone e do e-mail.
            </Texto>
            <LinhaInfo rotulo="Nome" valor={dados.nome} />
            <LinhaInfo rotulo="CPF" valor={dados.cpf} mascarado />
            <LinhaInfo rotulo="CNH" valor={dados.cnh} mascarado />
            <LinhaInfo rotulo="Telefone" valor={dados.telefone} mascarado />
            <LinhaInfo rotulo="E-mail" valor={dados.email ?? 'Sem e-mail'} mascarado={dados.email !== null} />
            <Botao
              titulo="Trocar telefone"
              variante="secundaria"
              icone="call"
              aoPressionar={() => router.push('/telefone')}
              accessibilityHint="Abre a troca do seu telefone"
            />
          </Cartao>

          <Cartao titulo="Capacete">
            {!sessao ? (
              <Texto>Não deu para carregar o capacete agora. Puxe a tela para atualizar.</Texto>
            ) : sessao.codigoDispositivo ? (
              <LinhaInfo rotulo="Capacete vinculado" valor={sessao.codigoDispositivo} />
            ) : (
              <>
                <Texto>Nenhum capacete vinculado. Vincule para ligar a proteção contra quedas no turno.</Texto>
                <FormularioCapacete
                  entregadorId={entregadorId}
                  aoVincular={(codigo) => AccessibilityInfo.announceForAccessibility(`Capacete ${codigo} vinculado`)}
                />
              </>
            )}
          </Cartao>

          <Cartao titulo="Ficha médica">
            <View
              accessible
              accessibilityLabel={dados.possuiFichaMedica ? 'Ficha médica cadastrada' : 'Sem ficha médica'}
              style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}
            >
              <Icone
                nome={dados.possuiFichaMedica ? 'checkmark-circle' : 'alert-circle'}
                cor={dados.possuiFichaMedica ? cores.aviso.sucesso.icone : cores.aviso.atencao.icone}
              />
              <Texto variante="rotulo" style={{ flex: 1 }}>
                {dados.possuiFichaMedica ? 'Ficha médica cadastrada' : 'Sem ficha médica'}
              </Texto>
            </View>
            <Botao
              titulo={dados.possuiFichaMedica ? 'Ver ficha médica' : 'Cadastrar ficha'}
              variante="secundaria"
              icone="medkit"
              aoPressionar={() => router.push('/ficha-medica')}
            />
            <Botao
              titulo="Quem acessou minha ficha"
              variante="secundaria"
              icone="eye"
              aoPressionar={() => router.push('/acessos-ficha')}
              accessibilityHint="Mostra os socorristas que tentaram abrir sua ficha"
            />
          </Cartao>

          <Cartao titulo="Rede de socorro">
            {voluntarioId ? (
              <>
                <View
                  accessible
                  accessibilityLabel={`Você faz parte da rede de socorro. ${rede.disponivel ? 'Disponível para ajudar' : 'Indisponível agora'}`}
                  style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno }}
                >
                  <Icone nome="people" cor={cores.aviso.sucesso.icone} />
                  <View style={{ flex: 1 }}>
                    <Texto variante="rotulo">Você faz parte da rede de socorro</Texto>
                    <Texto variante="legenda">{rede.disponivel ? 'Disponível para ajudar' : 'Indisponível agora'}</Texto>
                  </View>
                </View>
                <Botao
                  titulo="Abrir os chamados"
                  variante="secundaria"
                  icone="people"
                  aoPressionar={() => router.navigate('/chamados')}
                />
              </>
            ) : (
              <ParticiparRede />
            )}
          </Cartao>

          <Cartao
            titulo="Contatos de emergência"
            acao={<BotaoOlho visivel={contatosVisiveis} aoAlternar={() => setContatosVisiveis((atual) => !atual)} />}
            onLayout={(evento) => setPosicaoContatos(evento.nativeEvent.layout.y)}
          >
            {primeiroContato ? (
              <ItemContato
                entregadorId={entregadorId}
                contato={primeiroContato}
                ultimo={dados.contatos.length === 1}
                oculto={!contatosVisiveis}
                aoMudar={() => void carregar()}
              />
            ) : (
              <Texto>Nenhum contato ainda. Ele é avisado se você cair.</Texto>
            )}
            {dados.contatos.length > 1 ? (
              <Botao
                titulo={`Ver todos os contatos (${dados.contatos.length})`}
                variante="secundaria"
                icone="people"
                aoPressionar={() => router.push('/contatos')}
              />
            ) : null}
            <Botao
              titulo="Adicionar contato"
              variante="secundaria"
              icone="person-add"
              aoPressionar={() => router.push('/contato')}
            />
          </Cartao>
        </>
      ) : null}
    </Tela>
  );
}
