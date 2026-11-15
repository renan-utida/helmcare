import { useId } from 'react';
import { StyleSheet, TextInput, View, type KeyboardTypeOptions, type TextInputProps } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Texto } from './Texto';

/** Tipo do conteúdo do campo, que escolhe o teclado. */
export type TipoCampo = 'texto' | 'numero' | 'telefone' | 'url' | 'codigo';

/** Teclado de cada tipo de campo. */
const TECLADO: Record<TipoCampo, KeyboardTypeOptions> = {
  texto: 'default',
  numero: 'number-pad',
  telefone: 'phone-pad',
  url: 'url',
  codigo: 'default',
};

/** Props do `Campo`. */
export interface CampoProps {
  /** Rótulo sempre visível acima do campo. */
  rotulo: string;
  valor: string;
  aoMudar: (texto: string) => void;
  tipo?: TipoCampo;
  /** Mensagem de erro mostrada abaixo do campo e lida junto com o rótulo. */
  erro?: string | null;
  /** Explicação curta abaixo do rótulo (também vira a dica do leitor de tela). */
  dica?: string;
  maxLength?: number;
  multilinha?: boolean;
  /** Texto de exemplo dentro do campo vazio. */
  exemplo?: string;
  autoCapitalize?: TextInputProps['autoCapitalize'];
  autoComplete?: TextInputProps['autoComplete'];
  editavel?: boolean;
}

/**
 * Campo de texto acessível: rótulo visível, teclado pelo tipo e mensagem de erro
 * ligada ao campo (o leitor de tela lê "rótulo, erro: mensagem" ao focar).
 * A altura cresce com a fonte do sistema: só há altura mínima, nunca fixa.
 */
export function Campo({
  rotulo,
  valor,
  aoMudar,
  tipo = 'texto',
  erro,
  dica,
  maxLength,
  multilinha = false,
  exemplo,
  autoCapitalize,
  autoComplete,
  editavel = true,
}: CampoProps) {
  const { cores, tipografia, espaco, raio, medidas } = useTema();
  // Ids para ligar o rótulo ao campo no Android (accessibilityLabelledBy).
  const idRotulo = useId();
  const temErro = Boolean(erro);

  return (
    <View style={{ gap: espaco.pequeno }}>
      <Texto variante="rotulo" nativeID={idRotulo}>
        {rotulo}
      </Texto>
      {dica ? <Texto variante="legenda">{dica}</Texto> : null}
      <TextInput
        value={valor}
        onChangeText={aoMudar}
        keyboardType={TECLADO[tipo]}
        autoCapitalize={autoCapitalize ?? (tipo === 'texto' ? 'words' : 'none')}
        autoComplete={autoComplete ?? 'off'}
        autoCorrect={false}
        maxLength={maxLength}
        multiline={multilinha}
        editable={editavel}
        placeholder={exemplo}
        placeholderTextColor={cores.textoSecundario}
        accessibilityLabel={temErro ? `${rotulo}, erro: ${erro}` : rotulo}
        accessibilityLabelledBy={idRotulo}
        accessibilityHint={dica}
        accessibilityState={{ disabled: !editavel }}
        style={[
          tipografia.corpo,
          styles.entrada,
          {
            color: cores.texto,
            backgroundColor: cores.superficie,
            borderColor: temErro ? cores.erro : cores.borda,
            // Borda mais grossa no erro: a cor não é a única pista.
            borderWidth: temErro ? 3 : 2,
            borderRadius: raio.pequeno,
            minHeight: multilinha ? medidas.alturaBotao * 2 : medidas.alturaBotao,
            paddingHorizontal: espaco.medio,
            paddingVertical: espaco.pequeno,
          },
        ]}
      />
      {temErro ? (
        <Texto variante="legenda" cor="erro" accessibilityLiveRegion="polite">
          {`Erro: ${erro}`}
        </Texto>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  entrada: {
    textAlignVertical: 'top',
  },
});
