import { router } from 'expo-router';
import { useState } from 'react';
import { AccessibilityInfo } from 'react-native';

import { Aviso } from '@/componentes/Aviso';
import { Botao } from '@/componentes/Botao';
import { Campo } from '@/componentes/Campo';
import { Cartao } from '@/componentes/Cartao';
import { EstadoVazio } from '@/componentes/EstadoVazio';
import { GrupoOpcoes } from '@/componentes/GrupoOpcoes';
import { LinhaInfo } from '@/componentes/LinhaInfo';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';
import { entregadorServico } from '@/servicos/entregadorServico';
import { converterErro } from '@/servicos/erros';
import { TIPOS_SANGUINEOS, type FichaMedicaResposta, type TipoSanguineo } from '@/tipos/contrato';
import { errosDosCampos, MENSAGEM_CONFIRA_CAMPOS, mensagemComum } from '@/utils/mensagensErro';
import { TAMANHO_MAXIMO_TEXTO_FICHA, validarTextoFicha, validarTipoSanguineo } from '@/utils/validacao';

/** Campos da ficha, com os nomes do JSON do contrato. */
const CAMPOS = ['tipoSanguineo', 'alergias', 'condicoesCriticas', 'medicamentos'] as const;
type CampoFicha = (typeof CAMPOS)[number];
type CampoTexto = Exclude<CampoFicha, 'tipoSanguineo'>;

/** Texto opcional: vazio não vai na requisição (o contrato marca os três como opcionais). */
function opcional(texto: string): string | undefined {
  const limpo = texto.trim();
  return limpo === '' ? undefined : limpo;
}

/**
 * Ficha médica (`PUT /entregadores/{id}/ficha-medica`). O `GET` do entregador só diz se a ficha
 * existe, então o formulário começa sempre vazio e o `PUT` envia a ficha completa.
 * A ficha devolvida fica só na memória desta tela: nada vai para o SQLite nem para o log.
 */
export default function FichaMedica() {
  const { perfilAtivo } = useSessao();
  const [tipoSanguineo, setTipoSanguineo] = useState<TipoSanguineo | null>(null);
  const [textos, setTextos] = useState<Record<CampoTexto, string>>({ alergias: '', condicoesCriticas: '', medicamentos: '' });
  const [erros, setErros] = useState<Partial<Record<CampoFicha, string>>>({});
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [salva, setSalva] = useState<FichaMedicaResposta | null>(null);

  if (!perfilAtivo) {
    return (
      <Tela>
        <EstadoVazio titulo="Nenhum perfil neste aparelho" icone="person-outline" />
      </Tela>
    );
  }
  const entregadorId = perfilAtivo.entregadorId;

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
      setSalva(ficha);
      AccessibilityInfo.announceForAccessibility('Ficha médica salva');
    } catch (e) {
      const erro = converterErro(e);
      if (erro.tipo === 'validacao') {
        setErros(errosDosCampos(erro.campos, CAMPOS));
        setMensagem(MENSAGEM_CONFIRA_CAMPOS);
      } else if (erro.tipo === 'nao-encontrado') {
        setMensagem('Cadastro não encontrado. Confira o perfil em uso nas Configurações.');
      } else {
        setMensagem(mensagemComum(erro));
      }
    } finally {
      setEnviando(false);
    }
  }

  // Depois do 200: mostra a ficha que a API devolveu (só em memória) e o botão de voltar.
  if (salva) {
    return (
      <Tela>
        <Aviso tipo="sucesso" titulo="Ficha médica salva" mensagem="Esta é a ficha que ficou guardada." />
        <Cartao titulo="Sua ficha médica">
          <LinhaInfo rotulo="Tipo sanguíneo" valor={salva.tipoSanguineo} />
          <LinhaInfo rotulo="Alergias" valor={salva.alergias || 'Nenhuma informada'} />
          <LinhaInfo rotulo="Condições críticas" valor={salva.condicoesCriticas || 'Nenhuma informada'} />
          <LinhaInfo rotulo="Medicamentos (só você vê)" valor={salva.medicamentos || 'Nenhum informado'} />
        </Cartao>
        <Botao titulo="Voltar" icone="arrow-back" aoPressionar={() => router.back()} />
      </Tela>
    );
  }

  return (
    <Tela>
      <Aviso tipo="informacao" mensagem="Para atualizar, preencha a ficha completa de novo." />
      <Texto>
        Só um socorrista verificado vê o tipo sanguíneo, as alergias e as condições críticas, e só durante uma
        emergência. Os medicamentos ficam só com você.
      </Texto>
      <GrupoOpcoes
        rotulo="Tipo sanguíneo"
        opcoes={TIPOS_SANGUINEOS}
        selecionada={tipoSanguineo}
        aoSelecionar={setTipoSanguineo}
        erro={erros.tipoSanguineo}
        leituraDe={(tipo) => tipo.replace('+', ' positivo').replace('-', ' negativo')}
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
      <Botao titulo="Salvar ficha" icone="save" aoPressionar={salvar} carregando={enviando} />
    </Tela>
  );
}
