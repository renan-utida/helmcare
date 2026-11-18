import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import { AccessibilityInfo, View } from 'react-native';

import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Campo } from '@/componentes/Campo';
import { FormularioCapacete } from '@/componentes/FormularioCapacete';
import { FormularioContato } from '@/componentes/FormularioContato';
import { LogoHorizontal } from '@/componentes/Marca';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { RegrasSenha } from '@/componentes/RegrasSenha';
import { useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { autenticacaoServico } from '@/servicos/autenticacaoServico';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro } from '@/servicos/erros';
import { gerarEntregadorSimulado } from '@/utils/dadosSimulados';
import { guardarParaProximaTela } from '@/utils/memoriaDoFluxo';
import { errosDosCampos, MENSAGEM_CONFIRA_CAMPOS, mensagemComum } from '@/utils/mensagensErro';
import { validarCnh, validarCpf, validarEmail, validarNome, validarSenha, validarTelefone } from '@/utils/validacao';

/** Etapas da tela: os 3 passos e a conclusão. */
type Passo = 'dados' | 'contato' | 'capacete' | 'pronto';

/** Número e título de cada passo numerado, para o indicador "Passo 1 de 3". */
const PASSOS_NUMERADOS: Partial<Record<Passo, { numero: number; titulo: string }>> = {
  dados: { numero: 1, titulo: 'Seus dados' },
  contato: { numero: 2, titulo: 'Contato de emergência' },
  capacete: { numero: 3, titulo: 'Capacete' },
};

/**
 * Cadastro do entregador em 3 passos: dados e acesso (`POST /entregadores` e o login logo
 * depois), contato de emergência (`POST /entregadores/{id}/contatos`) e capacete
 * (`POST /dispositivos`). A conta é salva no secure-store logo depois do login do passo 1, para
 * não se perder se o app fechar.
 */
export default function Cadastro() {
  const { espaco } = useTema();
  const [passo, setPasso] = useState<Passo>('dados');
  const [entregadorId, setEntregadorId] = useState<string | null>(null);
  const [comCapacete, setComCapacete] = useState(false);

  const numerado = PASSOS_NUMERADOS[passo];

  // Anuncia a troca de passo, porque o conteúdo da tela muda sem trocar de rota.
  useEffect(() => {
    if (numerado) {
      AccessibilityInfo.announceForAccessibility(`Passo ${numerado.numero} de 3: ${numerado.titulo}`);
    }
  }, [numerado]);

  return (
    // A `key` recria a rolagem a cada passo, para o novo passo começar do topo.
    <Tela key={passo}>
      {numerado ? (
        // Logo e indicador do passo juntos, com espaço médio entre eles (a Tela usa o grande).
        <View style={{ gap: espaco.medio }}>
          {/* A marca aparece só na primeira tela do cadastro, a porta de entrada do app. */}
          {passo === 'dados' ? <LogoHorizontal /> : null}
          <View style={{ gap: espaco.pequeno }}>
            <Texto variante="legenda">{`Passo ${numerado.numero} de 3`}</Texto>
            <Texto variante="titulo">{numerado.titulo}</Texto>
          </View>
        </View>
      ) : null}

      {passo === 'dados' ? (
        <PassoDados
          aoCriar={(id) => {
            setEntregadorId(id);
            setPasso('contato');
          }}
        />
      ) : null}

      {passo === 'contato' && entregadorId ? (
        <>
          <Texto>Quem devemos avisar se você cair? Essa pessoa recebe o aviso do SOS.</Texto>
          <FormularioContato
            entregadorId={entregadorId}
            aoSalvar={() => setPasso('capacete')}
            aoPular={() => setPasso('capacete')}
            textoPular="Adicionar depois"
          />
        </>
      ) : null}

      {passo === 'capacete' && entregadorId ? (
        <PassoCapacete
          entregadorId={entregadorId}
          aoConcluir={(vinculou) => {
            setComCapacete(vinculou);
            setPasso('pronto');
          }}
        />
      ) : null}

      {passo === 'pronto' ? <Pronto comCapacete={comCapacete} /> : null}

    </Tela>
  );
}

/** Campos do passo 1, com os nomes do JSON do contrato. */
const CAMPOS_DADOS = ['nome', 'cpf', 'cnh', 'telefone', 'email', 'senha'] as const;
type CampoDados = (typeof CAMPOS_DADOS)[number];

/** Volta para o login (de onde o cadastro costuma ser aberto) ou abre o login. */
function irParaLogin(motivo?: 'conta-criada') {
  if (!motivo && router.canGoBack()) {
    router.back();
    return;
  }
  router.replace(motivo ? { pathname: '/login', params: { motivo } } : '/login');
}

/**
 * Passo 1: dados pessoais simulados e o acesso (e-mail e senha), `POST /entregadores`. O cadastro
 * não devolve token: logo depois, o app faz o `POST /entregadores/login` com o mesmo e-mail e a
 * mesma senha, e os tokens vão para o secure-store. Só então seguem o contato e o capacete.
 */
function PassoDados({ aoCriar }: { aoCriar: (id: string) => void }) {
  const { espaco } = useTema();
  const { adicionarConta, podeAdicionarConta, contas } = useSessao();
  const [valores, setValores] = useState<Record<Exclude<CampoDados, 'senha'>, string>>({
    nome: '',
    cpf: '',
    cnh: '',
    telefone: '',
    email: '',
  });
  const [senha, setSenha] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [erros, setErros] = useState<Partial<Record<CampoDados | 'confirmacao', string>>>({});
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const cheio = !podeAdicionarConta();

  function mudar(campo: Exclude<CampoDados, 'senha'>, valor: string) {
    setValores((atual) => ({ ...atual, [campo]: valor }));
  }

  /** Preenche os dados pessoais com dados fictícios: o contrato não aceita dados de pessoas reais. */
  function preencherSimulado() {
    setValores((atual) => ({ ...atual, ...gerarEntregadorSimulado() }));
    setErros({});
    setMensagem(null);
  }

  async function continuar() {
    // Validação local com as regras da seção 3, antes de gastar uma chamada à API.
    const novosErros: Partial<Record<CampoDados | 'confirmacao', string>> = {};
    const validadores: Record<Exclude<CampoDados, 'senha'>, (v: string) => string | null> = {
      nome: validarNome,
      cpf: validarCpf,
      cnh: validarCnh,
      telefone: validarTelefone,
      email: validarEmail,
    };
    for (const campo of Object.keys(validadores) as (keyof typeof validadores)[]) {
      const erro = validadores[campo](valores[campo]);
      if (erro) novosErros[campo] = erro;
    }
    const erroSenha = validarSenha(senha);
    if (erroSenha) novosErros.senha = erroSenha;
    if (confirmacao !== senha) novosErros.confirmacao = 'As duas senhas estão diferentes.';
    setErros(novosErros);
    if (Object.keys(novosErros).length > 0) {
      setMensagem(MENSAGEM_CONFIRA_CAMPOS);
      return;
    }

    setEnviando(true);
    setMensagem(null);
    const email = valores.email.trim();
    let criado = false;
    try {
      await entregadorServico.criar({ ...valores, nome: valores.nome.trim(), email, senha });
      criado = true;
      // Login logo depois, com o mesmo e-mail e a mesma senha (o cadastro não devolve token).
      const login = await autenticacaoServico.entrar({ email, senha });
      await adicionarConta(login);
      setSenha('');
      setConfirmacao('');
      aoCriar(login.entregadorId);
    } catch (e) {
      const erro = converterErro(e);
      if (criado) {
        // A conta existe, mas o login automático falhou (por exemplo, sem conexão): leva ao
        // login com o e-mail preenchido pela memória do fluxo (nunca pela rota).
        guardarParaProximaTela('emailLogin', email);
        irParaLogin('conta-criada');
      } else if (erro.tipo === 'conflito') {
        // O app não lê o texto da API para saber qual campo repetiu.
        setMensagem('CPF, CNH, telefone ou e-mail já usado em outro cadastro.');
      } else if (erro.tipo === 'validacao') {
        setErros(errosDosCampos(erro.campos, CAMPOS_DADOS));
        setMensagem(MENSAGEM_CONFIRA_CAMPOS);
      } else {
        setMensagem(mensagemComum(erro));
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <>
      {cheio ? (
        <Aviso
          tipo="atencao"
          mensagem={`Este celular já tem ${contas.length} contas, o máximo. Saia de uma delas em Contas para criar outra.`}
        />
      ) : null}
      <Texto>Use dados simulados: o HelmCare é um protótipo e não guarda dados de pessoas reais.</Texto>
      <Botao
        titulo="Preencher com dados simulados"
        variante="secundaria"
        icone="shuffle"
        aoPressionar={preencherSimulado}
        accessibilityHint="Preenche nome, CPF, CNH e telefone com dados fictícios"
      />
      <Campo rotulo="Nome" valor={valores.nome} aoMudar={(v) => mudar('nome', v)} erro={erros.nome} maxLength={120} />
      <Campo
        rotulo="CPF"
        dica="11 números, com ou sem pontos"
        valor={valores.cpf}
        aoMudar={(v) => mudar('cpf', v)}
        tipo="numero"
        erro={erros.cpf}
        maxLength={14}
      />
      <Campo
        rotulo="CNH"
        dica="11 números"
        valor={valores.cnh}
        aoMudar={(v) => mudar('cnh', v)}
        tipo="numero"
        erro={erros.cnh}
        maxLength={11}
      />
      <Campo
        rotulo="Telefone"
        dica="Com DDD"
        valor={valores.telefone}
        aoMudar={(v) => mudar('telefone', v)}
        tipo="telefone"
        erro={erros.telefone}
        maxLength={20}
      />

      <View style={{ gap: espaco.medio }}>
        <Texto variante="subtitulo">Acesso</Texto>
        <Texto cor="textoSecundario">Você entra no app com este e-mail e esta senha.</Texto>
        <Campo
          rotulo="E-mail"
          valor={valores.email}
          aoMudar={(v) => mudar('email', v)}
          tipo="email"
          erro={erros.email}
          maxLength={254}
          exemplo="nome@exemplo.com"
        />
        <Campo
          rotulo="Senha"
          valor={senha}
          aoMudar={setSenha}
          tipo="senha"
          autoComplete="new-password"
          erro={erros.senha}
          maxLength={64}
        />
        <RegrasSenha senha={senha} />
        <Campo
          rotulo="Confirmar senha"
          valor={confirmacao}
          aoMudar={setConfirmacao}
          tipo="senha"
          autoComplete="new-password"
          erro={erros.confirmacao}
          maxLength={64}
        />
      </View>

      {mensagem ? <Aviso tipo="erro" mensagem={mensagem} /> : null}
      <Botao
        titulo="Continuar"
        icone="arrow-forward"
        aoPressionar={() => void continuar()}
        carregando={enviando}
        desabilitado={cheio}
      />
      <Botao
        titulo="Já tenho conta"
        variante="secundaria"
        icone="log-in"
        aoPressionar={() => irParaLogin()}
        desabilitado={enviando}
        accessibilityHint="Abre o login com e-mail e senha"
      />
    </>
  );
}

/** Passo 3: vincular o capacete (`POST /dispositivos`), com o código HC-0001 já preenchido. */
function PassoCapacete({ entregadorId, aoConcluir }: { entregadorId: string; aoConcluir: (vinculou: boolean) => void }) {
  return (
    <>
      <Texto variante="subtitulo">Você usa o capacete HelmCare?</Texto>
      <Texto>Com o capacete, o HelmCare percebe uma queda e chama ajuda se você não responder.</Texto>
      <FormularioCapacete
        entregadorId={entregadorId}
        aoVincular={() => aoConcluir(true)}
        aoPular={() => aoConcluir(false)}
        textoPular="Não tenho capacete"
      />
    </>
  );
}

/** Conclusão do cadastro, com o atalho para a ficha médica. */
function Pronto({ comCapacete }: { comCapacete: boolean }) {
  return (
    <>
      <Texto variante="titulo">Cadastro pronto</Texto>
      <Aviso
        tipo="sucesso"
        mensagem={
          comCapacete
            ? 'Seu cadastro e o capacete estão prontos.'
            : 'Seu cadastro está pronto. Você pode vincular um capacete depois.'
        }
      />
      <Texto>A ficha médica é opcional. Ela ajuda o socorro a cuidar de você numa emergência.</Texto>
      <Botao
        titulo="Cadastrar ficha médica agora"
        variante="secundaria"
        icone="medkit"
        aoPressionar={() => router.push('/ficha-medica')}
      />
      <Botao titulo="Ir para o turno" icone="arrow-forward" aoPressionar={() => router.dismissTo('/turno')} />
    </>
  );
}
