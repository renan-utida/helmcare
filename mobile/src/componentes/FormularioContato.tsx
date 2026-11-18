import { useState } from 'react';
import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro } from '@/servicos/erros';
import type { ContatoResposta } from '@/tipos/contrato';
import { ehParentesco, gerarContatoSimulado, PARENTESCOS, type Parentesco } from '@/utils/dadosSimulados';
import { errosDosCampos, MENSAGEM_CONFIRA_CAMPOS, mensagemComum } from '@/utils/mensagensErro';
import { validarNome, validarParentesco, validarTelefone } from '@/utils/validacao';

import { Aviso } from './Aviso';
import { Botao } from './Botao';
import { Campo } from './Campo';
import { GrupoOpcoes } from './GrupoOpcoes';

/** Campos do formulário, com os nomes do JSON do contrato. */
const CAMPOS = ['nome', 'telefone', 'parentesco'] as const;
type CampoContato = (typeof CAMPOS)[number];

/** Props do `FormularioContato`. */
export interface FormularioContatoProps {
  /** Entregador dono do contato. */
  entregadorId: string;
  /** Chamado depois do 201, com o contato devolvido (telefone mascarado). */
  aoSalvar: (contato: ContatoResposta) => void;
  /** Ação do botão secundário (ex.: "Adicionar depois"). Sem ela, o botão não aparece. */
  aoPular?: () => void;
  textoPular?: string;
  /** Contato que está sendo editado (`PUT`). Sem ele, o formulário adiciona um novo (`POST`). */
  contatoId?: string;
  /** Nome e parentesco atuais do contato editado. O telefone começa vazio (a API só o devolve mascarado). */
  inicial?: { nome: string; parentesco: string };
}

/**
 * Formulário do contato de emergência, usado no passo 2 do cadastro e no modal do contato.
 * Sem `contatoId`, adiciona (`POST /entregadores/{id}/contatos`); com ele, edita
 * (`PUT .../contatos/{contatoId}`, com o corpo inteiro). Os dados ficam só na API, nunca no SQLite.
 */
export function FormularioContato({
  entregadorId,
  aoSalvar,
  aoPular,
  textoPular = 'Adicionar depois',
  contatoId,
  inicial,
}: FormularioContatoProps) {
  const { espaco } = useTema();
  const editando = contatoId !== undefined;
  const [nome, setNome] = useState(inicial?.nome ?? '');
  // O telefone nunca vem preenchido: a API só devolve o número mascarado, e o PUT pede o número inteiro.
  const [telefone, setTelefone] = useState('');
  // Parentesco fora da lista de botões (cadastrado por outra via) fica sem escolha, para a pessoa marcar.
  const [parentesco, setParentesco] = useState<Parentesco | null>(
    inicial && ehParentesco(inicial.parentesco) ? inicial.parentesco : null,
  );
  const [erros, setErros] = useState<Partial<Record<CampoContato, string>>>({});
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  /** Preenche com um contato fictício (o contrato só aceita dados simulados). */
  function preencherSimulado() {
    const simulado = gerarContatoSimulado();
    setNome(simulado.nome);
    setTelefone(simulado.telefone);
    setParentesco(simulado.parentesco);
    setErros({});
    setMensagem(null);
  }

  async function salvar() {
    const novosErros: Partial<Record<CampoContato, string>> = {};
    const erroNome = validarNome(nome);
    const erroTelefone = validarTelefone(telefone);
    const erroParentesco = validarParentesco(parentesco ?? '');
    if (erroNome) novosErros.nome = erroNome;
    if (erroTelefone) novosErros.telefone = erroTelefone;
    if (erroParentesco) novosErros.parentesco = erroParentesco;
    setErros(novosErros);
    if (Object.keys(novosErros).length > 0) {
      setMensagem(MENSAGEM_CONFIRA_CAMPOS);
      return;
    }

    setEnviando(true);
    setMensagem(null);
    try {
      const corpo = { nome: nome.trim(), telefone, parentesco: parentesco ?? '' };
      const contato = contatoId
        ? await entregadorServico.editarContato(entregadorId, contatoId, corpo)
        : await entregadorServico.adicionarContato(entregadorId, corpo);
      aoSalvar(contato);
    } catch (e) {
      const erro = converterErro(e);
      if (erro.tipo === 'validacao') {
        setErros(errosDosCampos(erro.campos, CAMPOS));
        setMensagem(MENSAGEM_CONFIRA_CAMPOS);
      } else if (erro.tipo === 'nao-encontrado') {
        setMensagem(
          editando
            ? 'Este contato não existe mais. Volte ao Perfil para ver a lista atualizada.'
            : 'Cadastro não encontrado na API. Confira a conta em uso no menu da conta.',
        );
      } else {
        setMensagem(mensagemComum(erro));
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <View style={{ gap: espaco.grande }}>
      {editando ? (
        <Aviso
          tipo="informacao"
          mensagem="Por segurança, o telefone aparece mascarado. Digite o telefone completo de novo."
        />
      ) : (
        <Botao
          titulo="Preencher com dados simulados"
          variante="secundaria"
          icone="shuffle"
          aoPressionar={preencherSimulado}
          accessibilityHint="Preenche o contato com dados fictícios, como pede o projeto"
        />
      )}
      <Campo rotulo="Nome do contato" valor={nome} aoMudar={setNome} erro={erros.nome} maxLength={120} />
      <Campo
        rotulo="Telefone do contato"
        dica="Com DDD, só números ou com máscara"
        valor={telefone}
        aoMudar={setTelefone}
        tipo="telefone"
        erro={erros.telefone}
        maxLength={20}
      />
      <GrupoOpcoes
        rotulo="Parentesco"
        opcoes={PARENTESCOS}
        selecionada={parentesco}
        aoSelecionar={setParentesco}
        erro={erros.parentesco}
      />
      {mensagem ? <Aviso tipo="erro" mensagem={mensagem} /> : null}
      <Botao
        titulo={editando ? 'Salvar alterações' : 'Salvar contato'}
        icone={editando ? 'save' : 'person-add'}
        aoPressionar={salvar}
        carregando={enviando}
      />
      {aoPular ? <Botao titulo={textoPular} variante="secundaria" aoPressionar={aoPular} desabilitado={enviando} /> : null}
    </View>
  );
}
