import { router } from 'expo-router';
import { useState } from 'react';
import { AccessibilityInfo } from 'react-native';

import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Campo } from '@/componentes/Campo';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro } from '@/servicos/erros';
import { guardarParaProximaTela } from '@/utils/memoriaDoFluxo';
import { MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';
import { validarTelefone } from '@/utils/validacao';

/**
 * Troca do telefone do entregador (`PUT /entregadores/{id}/telefone`), com a regra da seção 3
 * (10 ou 11 números, com DDD). O 200 traz o corpo do `GET /entregadores/{id}`, que volta para o
 * Perfil pela memória do fluxo (nunca pela rota). 409 telefone de outro cadastro; 400 no campo.
 */
export default function Telefone() {
  const { contaAtiva } = useSessao();
  const [telefone, setTelefone] = useState('');
  const [erroCampo, setErroCampo] = useState<string | null>(null);
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  if (!contaAtiva) {
    return (
      <Tela>
        <EstadoVazio titulo="Nenhuma conta neste aparelho" icone="person-outline" />
      </Tela>
    );
  }
  const entregadorId = contaAtiva;

  async function salvar() {
    const erro = validarTelefone(telefone);
    setErroCampo(erro);
    setMensagem(null);
    if (erro) {
      return;
    }
    setEnviando(true);
    try {
      const atualizado = await entregadorServico.trocarTelefone(entregadorId, { telefone });
      // O Perfil usa estes dados (já mascarados) ao ganhar foco, sem consultar de novo.
      guardarParaProximaTela('entregadorAtualizado', atualizado);
      AccessibilityInfo.announceForAccessibility('Telefone trocado');
      router.back();
    } catch (e) {
      const erroApi = converterErro(e);
      if (erroApi.tipo === 'conflito') {
        setMensagem('Este telefone já está em outro cadastro.');
      } else if (erroApi.tipo === 'validacao') {
        setErroCampo('Confira o telefone: 10 ou 11 números, com DDD.');
      } else if (erroApi.tipo === 'sem-conexao') {
        setMensagem(`O telefone não foi trocado. ${MENSAGEM_SEM_CONEXAO}`);
      } else {
        setMensagem(mensagemComum(erroApi));
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Tela>
      <Texto>Digite o seu telefone novo. O antigo deixa de valer no cadastro.</Texto>
      <Campo
        rotulo="Telefone novo"
        dica="Com DDD, só números ou com máscara"
        valor={telefone}
        aoMudar={setTelefone}
        tipo="telefone"
        erro={erroCampo}
        maxLength={20}
        exemplo="(11) 98765-4321"
      />
      {mensagem ? <Aviso tipo="erro" mensagem={mensagem} /> : null}
      <Botao titulo="Salvar telefone" icone="save" aoPressionar={() => void salvar()} carregando={enviando} />
      <Botao titulo="Cancelar" variante="secundaria" aoPressionar={() => router.back()} desabilitado={enviando} />
    </Tela>
  );
}
