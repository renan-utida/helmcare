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
import { APELIDO_COM_CAPACETE, APELIDO_SEM_CAPACETE, useSessao } from '@/contextos/SessaoContext';
import { useTema } from '@/contextos/TemaContext';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro } from '@/servicos/erros';
import { gerarEntregadorSimulado } from '@/utils/dadosSimulados';
import { errosDosCampos, MENSAGEM_CONFIRA_CAMPOS, mensagemComum } from '@/utils/mensagensErro';
import {
  normalizarCodigoCapacete,
  validarCnh,
  validarCodigoCapacete,
  validarCpf,
  validarGuid,
  validarNome,
  validarTelefone,
} from '@/utils/validacao';

/** Etapas da tela: os 3 passos, a conclusão e o atalho "Já tenho cadastro". */
type Passo = 'dados' | 'contato' | 'capacete' | 'pronto' | 'existente';

/** Número e título de cada passo numerado, para o indicador "Passo 1 de 3". */
const PASSOS_NUMERADOS: Partial<Record<Passo, { numero: number; titulo: string }>> = {
  dados: { numero: 1, titulo: 'Seus dados' },
  contato: { numero: 2, titulo: 'Contato de emergência' },
  capacete: { numero: 3, titulo: 'Capacete' },
};

/**
 * Cadastro do entregador em 3 passos: dados (`POST /entregadores`), contato de emergência
 * (`POST /entregadores/{id}/contatos`) e capacete (`POST /dispositivos`). O perfil local é
 * gravado logo depois do 201 do passo 1, para o id não se perder se o app fechar.
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
          aoJaTenhoCadastro={() => setPasso('existente')}
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

      {passo === 'existente' ? <PassoExistente aoVoltar={() => setPasso('dados')} /> : null}
    </Tela>
  );
}

/** Campos do passo 1, com os nomes do JSON do contrato. */
const CAMPOS_DADOS = ['nome', 'cpf', 'cnh', 'telefone'] as const;
type CampoDados = (typeof CAMPOS_DADOS)[number];

/** Passo 1: dados pessoais simulados e `POST /entregadores`. */
function PassoDados({ aoCriar, aoJaTenhoCadastro }: { aoCriar: (id: string) => void; aoJaTenhoCadastro: () => void }) {
  const { salvarPerfil, escolherApelido } = useSessao();
  const [valores, setValores] = useState<Record<CampoDados, string>>({ nome: '', cpf: '', cnh: '', telefone: '' });
  const [erros, setErros] = useState<Partial<Record<CampoDados, string>>>({});
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  function mudar(campo: CampoDados, valor: string) {
    setValores((atual) => ({ ...atual, [campo]: valor }));
  }

  /** Preenche com dados fictícios: o contrato não aceita dados de pessoas reais. */
  function preencherSimulado() {
    setValores(gerarEntregadorSimulado());
    setErros({});
    setMensagem(null);
  }

  async function continuar() {
    // Validação local com as regras da seção 3, antes de gastar uma chamada à API.
    const novosErros: Partial<Record<CampoDados, string>> = {};
    const validadores: Record<CampoDados, (v: string) => string | null> = {
      nome: validarNome,
      cpf: validarCpf,
      cnh: validarCnh,
      telefone: validarTelefone,
    };
    for (const campo of CAMPOS_DADOS) {
      const erro = validadores[campo](valores[campo]);
      if (erro) novosErros[campo] = erro;
    }
    setErros(novosErros);
    if (Object.keys(novosErros).length > 0) {
      setMensagem(MENSAGEM_CONFIRA_CAMPOS);
      return;
    }

    setEnviando(true);
    setMensagem(null);
    try {
      const entregador = await entregadorServico.criar({ ...valores, nome: valores.nome.trim() });
      // Grava já o perfil local (só id e apelido neutro) e o deixa ativo.
      await salvarPerfil(
        {
          entregadorId: entregador.id,
          apelido: escolherApelido(APELIDO_SEM_CAPACETE, entregador.id),
          codigoCapacete: null,
          voluntarioId: null,
          criadoEm: new Date().toISOString(),
        },
        { ativar: true },
      );
      aoCriar(entregador.id);
    } catch (e) {
      const erro = converterErro(e);
      if (erro.tipo === 'conflito') {
        setMensagem('Este CPF já tem cadastro. Gere outros dados simulados ou use Já tenho cadastro.');
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
      {mensagem ? <Aviso tipo="erro" mensagem={mensagem} /> : null}
      <Botao titulo="Continuar" icone="arrow-forward" aoPressionar={continuar} carregando={enviando} />
      <Botao
        titulo="Já tenho cadastro"
        variante="secundaria"
        aoPressionar={aoJaTenhoCadastro}
        desabilitado={enviando}
        accessibilityHint="Entra com o código de um cadastro que já existe"
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

/** Atalho "Já tenho cadastro": entra com o id de um entregador criado antes (`GET /entregadores/{id}`). */
function PassoExistente({ aoVoltar }: { aoVoltar: () => void }) {
  const { salvarPerfil, escolherApelido } = useSessao();
  const [id, setId] = useState('');
  const [codigo, setCodigo] = useState('');
  const [erroId, setErroId] = useState<string | null>(null);
  const [erroCodigo, setErroCodigo] = useState<string | null>(null);
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function entrar() {
    const novoErroId = validarGuid(id);
    // O código é opcional: só é conferido quando foi preenchido.
    const novoErroCodigo = codigo.trim() === '' ? null : validarCodigoCapacete(codigo);
    setErroId(novoErroId);
    setErroCodigo(novoErroCodigo);
    if (novoErroId || novoErroCodigo) {
      setMensagem(MENSAGEM_CONFIRA_CAMPOS);
      return;
    }
    setEnviando(true);
    setMensagem(null);
    try {
      const entregador = await entregadorServico.obter(id.trim());
      const codigoCapacete = codigo.trim() === '' ? null : normalizarCodigoCapacete(codigo);
      await salvarPerfil(
        {
          entregadorId: entregador.id,
          apelido: escolherApelido(codigoCapacete ? APELIDO_COM_CAPACETE : APELIDO_SEM_CAPACETE, entregador.id),
          codigoCapacete,
          voluntarioId: null,
          criadoEm: new Date().toISOString(),
        },
        { ativar: true },
      );
      router.dismissTo('/turno');
    } catch (e) {
      const erro = converterErro(e);
      if (erro.tipo === 'nao-encontrado') {
        setMensagem('Cadastro não encontrado.');
      } else if (erro.tipo === 'validacao') {
        setErroId('Confira o código do cadastro.');
      } else {
        setMensagem(mensagemComum(erro));
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <>
      <Texto variante="titulo">Já tenho cadastro</Texto>
      <Texto>Digite o código do seu cadastro. Ele aparece quando o cadastro é feito pela API.</Texto>
      <Campo
        rotulo="Código do cadastro"
        dica="36 caracteres, como 3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90"
        valor={id}
        aoMudar={setId}
        tipo="codigo"
        erro={erroId}
        maxLength={36}
      />
      <Campo
        rotulo="Código do capacete (opcional)"
        dica="Deixe em branco se você não tem capacete"
        valor={codigo}
        aoMudar={setCodigo}
        tipo="codigo"
        autoCapitalize="characters"
        erro={erroCodigo}
        maxLength={7}
      />
      {mensagem ? <Aviso tipo="erro" mensagem={mensagem} /> : null}
      <Botao titulo="Entrar" icone="log-in" aoPressionar={entrar} carregando={enviando} />
      <Botao titulo="Voltar ao cadastro" variante="secundaria" aoPressionar={aoVoltar} desabilitado={enviando} />
    </>
  );
}
