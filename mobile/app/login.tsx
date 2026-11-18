import { router, Stack, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { AccessibilityInfo, View } from 'react-native';

import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Campo } from '@/componentes/Campo';
import { LogoHorizontal } from '@/componentes/Marca';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { MAXIMO_CONTAS_APARELHO } from '@/config/constantes';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { autenticacaoServico } from '@/servicos/autenticacaoServico';
import { converterErro, type ErroApi } from '@/servicos/erros';
import { retirarDoFluxo } from '@/utils/memoriaDoFluxo';
import { comecarPilhaEm } from '@/utils/navegacao';
import { MENSAGEM_MUITAS_TENTATIVAS, MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';
import { validarEmail } from '@/utils/validacao';

/**
 * Como o login foi aberto:
 * - `entrar` (padrão): sem conta no aparelho, ou depois de sair; ao entrar, vai para as abas;
 * - `adicionar`: "Adicionar outra conta", em Contas; ao entrar, volta para as abas com a conta nova;
 * - `reentrar`: a sessão terminou no meio de um alerta ou de um SOS; ao entrar, volta para a mesma tela.
 */
type ModoLogin = 'entrar' | 'adicionar' | 'reentrar';

/** Lê o modo da rota; qualquer outro valor é `entrar`. */
function modoDaRota(valor: string | string[] | undefined): ModoLogin {
  return valor === 'adicionar' || valor === 'reentrar' ? valor : 'entrar';
}

/** Mensagem de cada erro do login, pelo status HTTP (nunca pelo texto da API). */
function mensagemDoErro(erro: ErroApi): string {
  switch (erro.tipo) {
    case 'nao-autorizado':
      // O contrato dá o mesmo 401 para e-mail inexistente, senha errada e conta bloqueada.
      return 'E-mail ou senha incorretos.';
    case 'muitas-tentativas':
      return MENSAGEM_MUITAS_TENTATIVAS;
    case 'sem-conexao':
      return MENSAGEM_SEM_CONEXAO;
    case 'validacao':
      return 'Confira o e-mail e a senha.';
    default:
      return mensagemComum(erro);
  }
}

/**
 * Login com e-mail e senha (`POST /entregadores/login`, contrato 1.15). Os tokens da resposta vão
 * direto para o expo-secure-store; a senha some do estado depois de cada tentativa e nunca vai
 * para log. Parâmetros da rota: `modo` (entrar, adicionar ou reentrar) e `motivo` (sessao ou
 * conta-criada), que só escolhem textos: nenhum dado pessoal passa pela rota.
 */
export default function Login() {
  const { espaco } = useTema();
  const { adicionarConta, podeAdicionarConta, contas } = useSessao();
  const parametros = useLocalSearchParams<{ modo?: string; motivo?: string }>();
  const modo = modoDaRota(parametros.modo);
  // O e-mail do cadastro chega pela memória do fluxo (nunca pela rota), uma vez só.
  const [email, setEmail] = useState(() => retirarDoFluxo('emailLogin') ?? '');
  const [senha, setSenha] = useState('');
  const [erroEmail, setErroEmail] = useState<string | null>(null);
  const [erroSenha, setErroSenha] = useState<string | null>(null);
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const cheio = modo === 'adicionar' && !podeAdicionarConta();

  async function entrar() {
    const novoErroEmail = validarEmail(email);
    const novoErroSenha = senha === '' ? 'Digite a senha.' : null;
    setErroEmail(novoErroEmail);
    setErroSenha(novoErroSenha);
    if (novoErroEmail || novoErroSenha) {
      return;
    }
    setEnviando(true);
    setMensagem(null);
    try {
      const resposta = await autenticacaoServico.entrar({ email: email.trim(), senha });
      if (!podeAdicionarConta(resposta.entregadorId)) {
        // A conta é nova e o aparelho já tem o máximo: nada é salvo.
        setMensagem(`Este celular já tem ${MAXIMO_CONTAS_APARELHO} contas. Saia de uma delas em Contas para entrar com outra.`);
        return;
      }
      await adicionarConta(resposta);
      AccessibilityInfo.announceForAccessibility(`Você entrou como ${resposta.nome}`);
      if (modo === 'reentrar' && router.canGoBack()) {
        // Volta para a mesma tela (alerta ou ocorrência) que pediu para entrar de novo.
        router.back();
      } else {
        // Pilha nova nas abas: o voltar do Android nunca reabre o login.
        comecarPilhaEm('/inicio');
      }
    } catch (e) {
      setMensagem(mensagemDoErro(converterErro(e)));
    } finally {
      // A senha nunca fica guardada na tela depois de uma tentativa, com ou sem sucesso.
      setSenha('');
      setEnviando(false);
    }
  }

  const titulo = modo === 'adicionar' ? 'Adicionar conta' : 'Entrar';

  return (
    <>
      <Stack.Screen options={{ title: titulo, headerShown: modo !== 'entrar' }} />
      <Tela>
        {modo === 'entrar' ? <LogoHorizontal /> : null}
        <View style={{ gap: espaco.pequeno }}>
          <Texto variante="titulo">{modo === 'adicionar' ? 'Adicionar outra conta' : 'Entrar na sua conta'}</Texto>
          {modo === 'adicionar' ? (
            <Texto cor="textoSecundario">
              {`A conta fica salva neste celular para trocar sem digitar a senha de novo (até ${MAXIMO_CONTAS_APARELHO} contas).`}
            </Texto>
          ) : null}
        </View>

        {parametros.motivo === 'sessao' ? (
          <Aviso tipo="atencao" mensagem="Sua sessão terminou. Entre de novo." />
        ) : null}
        {parametros.motivo === 'conta-criada' ? (
          <Aviso tipo="informacao" mensagem="Sua conta foi criada. Entre com seu e-mail e senha." />
        ) : null}
        {cheio ? (
          <Aviso
            tipo="atencao"
            mensagem={`Este celular já tem ${contas.length} contas, o máximo. Saia de uma delas em Contas para adicionar outra.`}
          />
        ) : null}

        <Campo
          rotulo="E-mail"
          valor={email}
          aoMudar={setEmail}
          tipo="email"
          erro={erroEmail}
          maxLength={254}
          exemplo="nome@exemplo.com"
        />
        <Campo rotulo="Senha" valor={senha} aoMudar={setSenha} tipo="senha" erro={erroSenha} maxLength={64} />
        {mensagem ? <Aviso tipo="erro" mensagem={mensagem} /> : null}
        <Botao titulo="Entrar" icone="log-in" aoPressionar={() => void entrar()} carregando={enviando} desabilitado={cheio} />
        <Texto variante="legenda">Depois de 5 tentativas erradas, a conta fica bloqueada por 15 minutos.</Texto>
        {modo === 'reentrar' ? null : (
          <Botao
            titulo="Criar conta"
            variante="secundaria"
            icone="person-add"
            aoPressionar={() => router.push('/cadastro')}
            desabilitado={enviando || cheio}
            accessibilityHint="Abre o cadastro de um novo entregador"
          />
        )}
      </Tela>
    </>
  );
}
