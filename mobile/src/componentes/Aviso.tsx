import { View } from 'react-native';

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
}

/**
 * Faixa de aviso com ícone e texto. É uma região viva: quando aparece ou muda,
 * o leitor de tela anuncia (erros e atenção com prioridade).
 */
export function Aviso({ tipo, mensagem, titulo, compacto = false }: AvisoProps) {
  const { cores, espaco, raio } = useTema();
  const corAviso = cores.aviso[tipo];
  const { icone, prefixo } = APARENCIA[tipo];
  const urgente = tipo === 'erro' || tipo === 'atencao';
  return (
    <View
      accessible
      accessibilityRole={urgente ? 'alert' : 'text'}
      accessibilityLabel={`${prefixo}: ${titulo ? `${titulo}. ` : ''}${mensagem}`}
      accessibilityLiveRegion={urgente ? 'assertive' : 'polite'}
      style={{
        flexDirection: 'row',
        gap: espaco.pequeno,
        backgroundColor: corAviso.fundo,
        borderLeftColor: corAviso.icone,
        borderLeftWidth: compacto ? 4 : 6,
        borderRadius: raio.pequeno,
        padding: compacto ? espaco.pequeno : espaco.medio,
        alignItems: compacto ? 'center' : 'flex-start',
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
  );
}
