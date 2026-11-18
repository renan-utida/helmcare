import { router, useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';
import { AccessibilityInfo, View } from 'react-native';

import { Botao } from '@/componentes/Botao';
import { Carregando } from '@/componentes/Carregando';
import { Cartao } from '@/componentes/Cartao';
import { EstadoErro } from '@/componentes/EstadoErro';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { FormularioCapacete } from '@/componentes/FormularioCapacete';
import { Icone } from '@/componentes/Icone';
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
import { mensagemComum } from '@/utils/mensagensErro';

/** Mensagem de erro do Perfil para cada tipo de erro da API. */
function mensagemDoErro(erro: ErroApi): string {
  if (erro.tipo === 'nao-encontrado') {
    return 'Este cadastro não foi encontrado na API. Troque de conta pelo menu da conta.';
  }
  return mensagemComum(erro);
}

/**
 * Perfil do entregador: dados do `GET /entregadores/{id}` como a API devolve (mascarados),
 * o capacete da sessão, os contatos, se há ficha médica e a participação na rede de
 * socorro (com o botão "Fazer parte da rede de socorro"). Recarrega ao voltar dos modais.
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

  // Só vale o que é da conta em uso: ao trocar de conta, a tela volta a "Carregando".
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

  // Recarrega sempre que a aba ganha foco: ao abrir e ao voltar da ficha ou do contato.
  useFocusEffect(
    useCallback(() => {
      void carregar();
    }, [carregar]),
  );

  function atualizar() {
    setAtualizando(true);
    void carregar();
  }

  if (!entregadorId) {
    return (
      <Tela dentroDeAbas>
        <EstadoVazio titulo="Nenhuma conta neste aparelho" icone="person-outline" />
        <Botao titulo="Entrar" aoPressionar={() => router.replace('/login')} />
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
    <Tela dentroDeAbas aoAtualizar={atualizar} atualizando={atualizando}>
      <Texto variante="titulo">{dados?.nome ?? sessao?.nome ?? 'Seu perfil'}</Texto>
      {erro ? (
        <Texto variante="legenda" cor="erro" accessibilityLiveRegion="polite">
          {`Não foi possível atualizar. ${mensagemDoErro(erro)}`}
        </Texto>
      ) : null}

      {dados ? (
        <>
          <Cartao titulo="Seus dados">
            <Texto variante="legenda">Os números aparecem escondidos para proteger você.</Texto>
            <LinhaInfo rotulo="Nome" valor={dados.nome} />
            <LinhaInfo rotulo="CPF" valor={dados.cpf} mascarado />
            <LinhaInfo rotulo="CNH" valor={dados.cnh} mascarado />
            <LinhaInfo rotulo="Telefone" valor={dados.telefone} mascarado />
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
              titulo={dados.possuiFichaMedica ? 'Atualizar ficha' : 'Cadastrar ficha'}
              variante="secundaria"
              icone="medkit"
              aoPressionar={() => router.push('/ficha-medica')}
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

          <Cartao titulo="Contatos de emergência">
            {dados.contatos.length === 0 ? (
              <Texto>Nenhum contato ainda. Ele é avisado se você cair.</Texto>
            ) : (
              dados.contatos.map((contato) => (
                <View key={contato.id} style={{ gap: espaco.pequeno }}>
                  <LinhaInfo rotulo={contato.parentesco} valor={contato.nome} />
                  <LinhaInfo rotulo="Telefone" valor={contato.telefone} mascarado />
                </View>
              ))
            )}
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
