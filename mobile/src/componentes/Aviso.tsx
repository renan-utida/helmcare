import { useFocusEffect } from 'expo-router';
import { useCallback, useLayoutEffect, useRef, useState } from 'react';
import { Pressable, View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Icone, type NomeIcone } from './Icone';
import { Texto } from './Texto';

/** Tipo do aviso. */
export type TipoAviso = 'informacao' | 'sucesso' | 'atencao' | 'erro';

/** Ícone e palavra lida antes da mensagem, para a cor nunca ser a única pista. */
const APARENCIA: Record<TipoAviso, { icone: NomeIcone; prefixo: string }> = {
  informacao: { icone: 'information-circle', prefixo: 'Informação' },
  sucesso: { icone: 'checkmark-circle', prefixo: 'Pronto' },
  atencao: { icone: 'warning', prefixo: 'Atenção' },
  erro: { icone: 'close-circle', prefixo: 'Erro' },
};

/** Props do `Aviso`. */
export interface AvisoProps {
  tipo: TipoAviso;
  mensagem: string;
  /** Título curto em negrito (opcional). */
  titulo?: string;
  /** Faixa compacta (menos espaço e letra menor), para avisos que não podem empurrar a tela. */
  compacto?: boolean;
  /**
   * Aviso de confirmação de uma ação (turno encerrado, ficha salva...): ganha o X para fechar e
   * some sozinho quando a pessoa sai da tela ou da aba, sem voltar ao retornar. Uma mensagem nova
   * aparece normalmente. Avisos de erro e avisos fixos de informação não usam.
   */
  fechavel?: boolean;
  /** Chamado quando o aviso fecha (pelo X ou ao sair da tela), para a tela limpar o estado dela. */
  aoFechar?: () => void;
}

/**
 * Faixa de aviso com ícone e texto. É uma região viva: quando aparece ou muda,
 * o leitor de tela anuncia (erros e atenção com prioridade). Com `fechavel`, o X (48 dp,
 * "Fechar aviso") fica fora do bloco lido, como um botão próprio.
 */
export function Aviso({ tipo, mensagem, titulo, compacto = false, fechavel = false, aoFechar }: AvisoProps) {
  const { cores, espaco, raio, medidas } = useTema();
  const corAviso = cores.aviso[tipo];
  const { icone, prefixo } = APARENCIA[tipo];
  const urgente = tipo === 'erro' || tipo === 'atencao';
  // Guarda QUAL mensagem foi fechada: a mesma não volta; uma nova aparece.
  const [fechada, setFechada] = useState<string | null>(null);

  // A mensagem, a opção e o retorno mais recentes, para a limpeza do foco (que roda uma vez só).
  const atual = useRef({ mensagem, fechavel, aoFechar });
  useLayoutEffect(() => {
    atual.current = { mensagem, fechavel, aoFechar };
  }, [mensagem, fechavel, aoFechar]);

  // Ao sair da tela (ou da aba), o aviso de confirmação some e não reaparece ao voltar.
  useFocusEffect(
    useCallback(
      () => () => {
        if (atual.current.fechavel) {
          setFechada(atual.current.mensagem);
          atual.current.aoFechar?.();
        }
      },
      [],
    ),
  );

  if (fechavel && fechada === mensagem) {
    return null;
  }

  function fechar() {
    setFechada(mensagem);
    aoFechar?.();
  }

  return (
    <View
      style={{
        flexDirection: 'row',
        gap: espaco.pequeno,
        backgroundColor: corAviso.fundo,
        borderLeftColor: corAviso.icone,
        borderLeftWidth: compacto ? 4 : 6,
        borderRadius: raio.pequeno,
        padding: compacto ? espaco.pequeno : espaco.medio,
        paddingRight: fechavel ? 0 : compacto ? espaco.pequeno : espaco.medio,
        paddingVertical: fechavel ? 0 : compacto ? espaco.pequeno : espaco.medio,
        alignItems: compacto || fechavel ? 'center' : 'flex-start',
      }}
    >
      <View
        accessible
        accessibilityRole={urgente ? 'alert' : 'text'}
        accessibilityLabel={`${prefixo}: ${titulo ? `${titulo}. ` : ''}${mensagem}`}
        accessibilityLiveRegion={urgente ? 'assertive' : 'polite'}
        style={{
          flex: 1,
          flexDirection: 'row',
          gap: espaco.pequeno,
          alignItems: compacto ? 'center' : 'flex-start',
          paddingVertical: fechavel ? (compacto ? espaco.pequeno : espaco.medio) : 0,
        }}
      >
        <Icone nome={icone} cor={corAviso.icone} />
        <View style={{ flex: 1, gap: espaco.pequeno / 2 }}>
          {titulo ? (
            <Texto variante="rotulo" corPersonalizada={corAviso.texto}>
              {titulo}
            </Texto>
          ) : null}
          <Texto variante={compacto ? 'legenda' : 'corpo'} corPersonalizada={corAviso.texto}>
            {mensagem}
          </Texto>
        </View>
      </View>
      {fechavel ? (
        <Pressable
          onPress={fechar}
          accessibilityRole="button"
          accessibilityLabel="Fechar aviso"
          style={{
            minWidth: medidas.toqueMinimo,
            minHeight: medidas.toqueMinimo,
            alignItems: 'center',
            justifyContent: 'center',
            alignSelf: 'flex-start',
          }}
        >
          <Icone nome="close" cor={corAviso.texto} />
        </Pressable>
      ) : null}
    </View>
  );
}
