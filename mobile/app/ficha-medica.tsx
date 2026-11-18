import { useCallback, useEffect, useState } from 'react';
import { AccessibilityInfo } from 'react-native';

import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Campo } from '@/componentes/Campo';
import { Carregando } from '@/componentes/Carregando';
import { Cartao } from '@/componentes/Cartao';
import { EstadoErro } from '@/componentes/EstadoErro';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { GrupoOpcoes } from '@/componentes/GrupoOpcoes';
import { LinhaInfo } from '@/componentes/LinhaInfo';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro, type ErroApi } from '@/servicos/erros';
import { TIPOS_SANGUINEOS, type FichaMedicaResposta, type TipoSanguineo } from '@/tipos/contrato';
import { errosDosCampos, MENSAGEM_CONFIRA_CAMPOS, MENSAGEM_SEM_CONEXAO, mensagemComum } from '@/utils/mensagensErro';
import {
  ehTipoSanguineo,
  TAMANHO_MAXIMO_TEXTO_FICHA,
  validarTextoFicha,
  validarTipoSanguineo,
} from '@/utils/validacao';

/** Campos da ficha, com os nomes do JSON do contrato. */
const CAMPOS = ['tipoSanguineo', 'alergias', 'condicoesCriticas', 'medicamentos'] as const;
type CampoFicha = (typeof CAMPOS)[number];
type CampoTexto = Exclude<CampoFicha, 'tipoSanguineo'>;

/** Texto opcional: vazio não vai na requisição (o contrato marca os três como opcionais). */
function opcional(texto: string): string | undefined {
  const limpo = texto.trim();
  return limpo === '' ? undefined : limpo;
}

/** Como o leitor de tela fala o tipo sanguíneo ("A+" vira "A positivo"). */
function tipoFalado(tipo: string): string {
  return tipo.replace('+', ' positivo').replace('-', ' negativo');
}

/**
 * Situação da tela: carregando o `GET`, sem ficha (404), erro, lendo a ficha ou editando.
 * A ficha fica SÓ neste estado da tela (o `GET` e o `PUT` respondem com `no-store`): nada vai
 * para o SQLite, para log ou para a rota, e tudo some ao sair da tela.
 */
type Situacao =
  | { tipo: 'carregando' }
  | { tipo: 'sem-ficha' }
  | { tipo: 'erro'; erro: ErroApi }
  | { tipo: 'ver'; ficha: FichaMedicaResposta; salvaAgora: boolean }
  | { tipo: 'editar'; inicial: FichaMedicaResposta | null };

/**
 * Ficha médica (seção 7): abre lendo a ficha do `GET /entregadores/{id}/ficha-medica`, com
 * "Editar ficha" para o formulário já preenchido. Sem ficha (404): "Cadastrar ficha", com o
 * formulário vazio. O `PUT` envia a ficha inteira, e a tela mostra a ficha devolvida.
 */
export default function FichaMedica() {
  const { contaAtiva } = useSessao();
  const [situacao, setSituacao] = useState<Situacao>({ tipo: 'carregando' });

  const carregar = useCallback(async () => {
    if (!contaAtiva) {
      return;
    }
    try {
      const ficha = await entregadorServico.obterFichaMedica(contaAtiva);
      setSituacao({ tipo: 'ver', ficha, salvaAgora: false });
    } catch (e) {
      const erro = converterErro(e);
      setSituacao(erro.tipo === 'nao-encontrado' ? { tipo: 'sem-ficha' } : { tipo: 'erro', erro });
    }
  }, [contaAtiva]);

  // Carrega ao abrir a tela.
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

  switch (situacao.tipo) {
    case 'carregando':
      return <Carregando mensagem="Abrindo sua ficha médica..." />;
    case 'erro':
      return (
        <Tela>
          <EstadoErro
            mensagem={situacao.erro.tipo === 'sem-conexao' ? MENSAGEM_SEM_CONEXAO : mensagemComum(situacao.erro)}
            aoTentarDeNovo={() => {
              setSituacao({ tipo: 'carregando' });
              void carregar();
            }}
          />
        </Tela>
      );
    case 'sem-ficha':
      return (
        <Tela>
          <EstadoVazio
            titulo="Você ainda não cadastrou sua ficha médica."
            mensagem="Ela ajuda o socorro a cuidar de você numa emergência."
            icone="medkit-outline"
          />
          <Botao titulo="Cadastrar ficha" icone="add-circle" aoPressionar={() => setSituacao({ tipo: 'editar', inicial: null })} />
        </Tela>
      );
    case 'ver':
      return (
        <VerFicha
          ficha={situacao.ficha}
          salvaAgora={situacao.salvaAgora}
          aoEditar={() => setSituacao({ tipo: 'editar', inicial: situacao.ficha })}
        />
      );
    case 'editar':
      return (
        <FormularioFicha
          entregadorId={contaAtiva}
          inicial={situacao.inicial}
          aoSalvar={(ficha) => setSituacao({ tipo: 'ver', ficha, salvaAgora: true })}
          aoCancelar={() =>
            setSituacao(situacao.inicial ? { tipo: 'ver', ficha: situacao.inicial, salvaAgora: false } : { tipo: 'sem-ficha' })
          }
        />
      );
  }
}

/** Leitura da ficha, com o botão "Editar ficha". */
function VerFicha({
  ficha,
  salvaAgora,
  aoEditar,
}: {
  ficha: FichaMedicaResposta;
  salvaAgora: boolean;
  aoEditar: () => void;
}) {
  return (
    <Tela>
      {salvaAgora ? (
        <Aviso tipo="sucesso" titulo="Ficha médica salva" mensagem="Esta é a ficha que ficou guardada." />
      ) : null}
      <Texto>
        Só um socorrista verificado vê o tipo sanguíneo, as alergias e as condições críticas, e só durante uma
        emergência. Os medicamentos ficam só com você.
      </Texto>
      <Cartao titulo="Sua ficha médica">
        <LinhaInfo rotulo="Tipo sanguíneo" valor={ficha.tipoSanguineo} leitura={tipoFalado(ficha.tipoSanguineo)} />
        <LinhaInfo rotulo="Alergias" valor={ficha.alergias || 'Nenhuma informada'} />
        <LinhaInfo rotulo="Condições críticas" valor={ficha.condicoesCriticas || 'Nenhuma informada'} />
        <LinhaInfo rotulo="Medicamentos (só você vê)" valor={ficha.medicamentos || 'Nenhum informado'} />
      </Cartao>
      <Botao titulo="Editar ficha" icone="create" aoPressionar={aoEditar} />
    </Tela>
  );
}

/** Props do formulário da ficha. */
interface FormularioFichaProps {
  entregadorId: string;
  /** Ficha atual (para editar) ou `null` (para cadastrar, com tudo vazio). */
  inicial: FichaMedicaResposta | null;
  aoSalvar: (ficha: FichaMedicaResposta) => void;
  aoCancelar: () => void;
}

/** Formulário da ficha (`PUT /entregadores/{id}/ficha-medica`), que substitui a ficha inteira. */
function FormularioFicha({ entregadorId, inicial, aoSalvar, aoCancelar }: FormularioFichaProps) {
  const tipoInicial = inicial && ehTipoSanguineo(inicial.tipoSanguineo) ? inicial.tipoSanguineo : null;
  const [tipoSanguineo, setTipoSanguineo] = useState<TipoSanguineo | null>(tipoInicial);
  const [textos, setTextos] = useState<Record<CampoTexto, string>>({
    alergias: inicial?.alergias ?? '',
    condicoesCriticas: inicial?.condicoesCriticas ?? '',
    medicamentos: inicial?.medicamentos ?? '',
  });
  const [erros, setErros] = useState<Partial<Record<CampoFicha, string>>>({});
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  function mudar(campo: CampoTexto, valor: string) {
    setTextos((atual) => ({ ...atual, [campo]: valor }));
  }

  async function salvar() {
    const novosErros: Partial<Record<CampoFicha, string>> = {};
    const erroTipo = validarTipoSanguineo(tipoSanguineo);
    if (erroTipo) novosErros.tipoSanguineo = erroTipo;
    for (const campo of ['alergias', 'condicoesCriticas', 'medicamentos'] as const) {
      const erro = validarTextoFicha(textos[campo]);
      if (erro) novosErros[campo] = erro;
    }
    setErros(novosErros);
    if (Object.keys(novosErros).length > 0 || !tipoSanguineo) {
      setMensagem(MENSAGEM_CONFIRA_CAMPOS);
      return;
    }

    setEnviando(true);
    setMensagem(null);
    try {
      const ficha = await entregadorServico.salvarFichaMedica(entregadorId, {
        tipoSanguineo,
        alergias: opcional(textos.alergias),
        condicoesCriticas: opcional(textos.condicoesCriticas),
        medicamentos: opcional(textos.medicamentos),
      });
      AccessibilityInfo.announceForAccessibility('Ficha médica salva');
      aoSalvar(ficha);
    } catch (e) {
      const erro = converterErro(e);
      if (erro.tipo === 'validacao') {
        setErros(errosDosCampos(erro.campos, CAMPOS));
        setMensagem(MENSAGEM_CONFIRA_CAMPOS);
      } else if (erro.tipo === 'nao-encontrado') {
        setMensagem('Cadastro não encontrado. Confira a conta em uso no menu da conta.');
      } else {
        setMensagem(mensagemComum(erro));
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Tela>
      <Aviso tipo="informacao" mensagem="Ao salvar, a ficha inteira é trocada por esta." />
      <GrupoOpcoes
        rotulo="Tipo sanguíneo"
        opcoes={TIPOS_SANGUINEOS}
        selecionada={tipoSanguineo}
        aoSelecionar={setTipoSanguineo}
        erro={erros.tipoSanguineo}
        leituraDe={tipoFalado}
        colunas={4}
      />
      <Campo
        rotulo="Alergias (opcional)"
        dica={`Até ${TAMANHO_MAXIMO_TEXTO_FICHA} letras`}
        valor={textos.alergias}
        aoMudar={(v) => mudar('alergias', v)}
        erro={erros.alergias}
        maxLength={TAMANHO_MAXIMO_TEXTO_FICHA}
        autoCapitalize="sentences"
        multilinha
      />
      <Campo
        rotulo="Condições críticas (opcional)"
        dica="Por exemplo: diabetes, epilepsia, problema no coração"
        valor={textos.condicoesCriticas}
        aoMudar={(v) => mudar('condicoesCriticas', v)}
        erro={erros.condicoesCriticas}
        maxLength={TAMANHO_MAXIMO_TEXTO_FICHA}
        autoCapitalize="sentences"
        multilinha
      />
      <Campo
        rotulo="Medicamentos (opcional)"
        dica="Só você vê. O socorrista não recebe esta parte."
        valor={textos.medicamentos}
        aoMudar={(v) => mudar('medicamentos', v)}
        erro={erros.medicamentos}
        maxLength={TAMANHO_MAXIMO_TEXTO_FICHA}
        autoCapitalize="sentences"
        multilinha
      />
      {mensagem ? <Aviso tipo="erro" mensagem={mensagem} /> : null}
      <Botao titulo="Salvar ficha" icone="save" aoPressionar={() => void salvar()} carregando={enviando} />
      <Botao titulo="Cancelar" variante="secundaria" aoPressionar={aoCancelar} desabilitado={enviando} />
    </Tela>
  );
}
