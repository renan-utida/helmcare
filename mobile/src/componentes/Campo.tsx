import { useId, useState } from 'react';
import { Pressable, StyleSheet, TextInput, View, type KeyboardTypeOptions, type TextInputProps } from 'react-native';

import { useTema } from '@/contextos/TemaContext';

import { Icone } from './Icone';
import { Texto } from './Texto';

/** Tipo do conteúdo do campo, que escolhe o teclado (e, na senha, esconde o texto). */
export type TipoCampo = 'texto' | 'numero' | 'telefone' | 'url' | 'codigo' | 'email' | 'senha';

/** Teclado de cada tipo de campo. */
const TECLADO: Record<TipoCampo, KeyboardTypeOptions> = {
  texto: 'default',
  numero: 'number-pad',
  telefone: 'phone-pad',
  url: 'url',
  codigo: 'default',
  email: 'email-address',
  senha: 'default',
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
 * No tipo senha, o texto fica escondido e o botão de olho (48 dp) mostra ou oculta.
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
  const ehSenha = tipo === 'senha';
  const [mostrarSenha, setMostrarSenha] = useState(false);
  // E-mail, senha e códigos nunca começam com maiúscula automática.
  const capitalizacao = autoCapitalize ?? (tipo === 'texto' ? 'words' : 'none');
  const autoCompletar = autoComplete ?? (tipo === 'email' ? 'email' : ehSenha ? 'password' : 'off');

  return (
    <View style={{ gap: espaco.pequeno }}>
      <Texto variante="rotulo" nativeID={idRotulo}>
        {rotulo}
      </Texto>
      {dica ? <Texto variante="legenda">{dica}</Texto> : null}
      {/* A caixa é este contêiner (borda e fundo), com o texto e, na senha, o olho dentro dela,
          à direita: assim todos os campos têm a mesma largura e as mesmas margens, e o texto
          nunca passa por baixo do ícone (o olho tem a própria área). */}
      <View
        style={{
          flexDirection: 'row',
          alignItems: multilinha ? 'flex-start' : 'center',
          backgroundColor: cores.superficie,
          borderColor: temErro ? cores.erro : cores.borda,
          // Borda mais grossa no erro: a cor não é a única pista.
          borderWidth: temErro ? 3 : 2,
          borderRadius: raio.pequeno,
          minHeight: multilinha ? medidas.alturaBotao * 2 : medidas.alturaBotao,
        }}
      >
        <TextInput
          value={valor}
          onChangeText={aoMudar}
          keyboardType={TECLADO[tipo]}
          autoCapitalize={capitalizacao}
          autoComplete={autoCompletar}
          autoCorrect={false}
          secureTextEntry={ehSenha && !mostrarSenha}
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
              flex: 1,
              alignSelf: 'stretch',
              color: cores.texto,
              paddingLeft: espaco.medio,
              // Com o olho, o espaço da direita é dele; sem ele, a margem normal.
              paddingRight: ehSenha ? 0 : espaco.medio,
              paddingVertical: espaco.pequeno,
            },
          ]}
        />
        {ehSenha ? (
          <Pressable
            onPress={() => setMostrarSenha((atual) => !atual)}
            accessibilityRole="button"
            accessibilityLabel={mostrarSenha ? 'Ocultar senha' : 'Mostrar senha'}
            style={{
              minWidth: medidas.toqueMinimo,
              minHeight: medidas.toqueMinimo,
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Icone nome={mostrarSenha ? 'eye-off' : 'eye'} cor={cores.primaria} tamanho={26} />
          </Pressable>
        ) : null}
      </View>
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
