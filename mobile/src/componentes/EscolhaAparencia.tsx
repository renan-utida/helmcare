import { AccessibilityInfo, Pressable, View } from 'react-native';

import { useAparencia, useTema, type ModoTema } from '@/contextos/TemaContext';
import { PALETAS, type Cores } from '@/tema/cores';

import { Icone } from './Icone';
import { Texto } from './Texto';

/** Paleta em uso de um modo. */
function coresDoModo(modo: ModoTema, paletaClara: string, paletaEscura: string): Cores {
  const lista: readonly { id: string; cores: Cores }[] = PALETAS[modo];
  const id = modo === 'claro' ? paletaClara : paletaEscura;
  return (lista.find((item) => item.id === id) ?? lista[0])?.cores ?? PALETAS[modo][0].cores;
}

/**
 * Miniatura de uma tela do app com as cores da paleta: o fundo, a barra de cima, duas linhas de
 * texto, um cartão e o botão principal. É só desenho (fora do leitor de tela): o nome ao lado diz
 * o modo. As cores vêm da própria paleta mostrada, não do tema em uso.
 */
function Miniatura({ cores }: { cores: Cores }) {
  const { raio } = useTema();
  return (
    <View
      accessible={false}
      importantForAccessibility="no-hide-descendants"
      style={{
        width: 72,
        height: 120,
        borderRadius: raio.pequeno,
        backgroundColor: cores.fundo,
        borderWidth: 1,
        borderColor: cores.borda,
        overflow: 'hidden',
        gap: 6,
      }}
    >
      <View style={{ height: 16, backgroundColor: cores.superficie, borderBottomWidth: 2, borderBottomColor: cores.destaque }} />
      <View style={{ paddingHorizontal: 8, gap: 4 }}>
        <View style={{ height: 6, width: 40, borderRadius: 3, backgroundColor: cores.texto }} />
        <View style={{ height: 4, width: 50, borderRadius: 2, backgroundColor: cores.textoSecundario }} />
        <View style={{ height: 26, borderRadius: 4, backgroundColor: cores.superficie, marginTop: 4 }} />
        <View style={{ height: 14, borderRadius: 4, backgroundColor: cores.primaria, marginTop: 4 }} />
      </View>
    </View>
  );
}

/** Selo "Em uso", com ícone e texto (a cor nunca é a única pista). */
function MarcaEmUso() {
  const { cores, espaco } = useTema();
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco.pequeno / 2 }}>
      <Icone nome="checkmark-circle" cor={cores.aviso.sucesso.icone} />
      <Texto variante="legenda">Em uso</Texto>
    </View>
  );
}

/**
 * "Aparência" da aba Ajustes: dois cartões grandes (Claro e Escuro), cada um com a miniatura de
 * uma tela, e abaixo as paletas do modo em uso em amostras de cor: HelmCare, Alto contraste e a
 * terceira (Areia no claro, Grafite no escuro). Cada escolha é um botão de opção para o leitor de
 * tela, com o estado marcado, e é anunciada.
 */
export function EscolhaAparencia() {
  const { cores, espaco, raio, medidas } = useTema();
  const { modo, paletaClara, paletaEscura, definirModo, definirPaletaClara, definirPaletaEscura } = useAparencia();
  const paletas: readonly { id: string; nome: string; cores: Cores }[] = PALETAS[modo];
  const paletaEmUso = modo === 'claro' ? paletaClara : paletaEscura;

  function escolherModo(novo: ModoTema) {
    void definirModo(novo);
    AccessibilityInfo.announceForAccessibility(novo === 'claro' ? 'Modo claro' : 'Modo escuro');
  }

  function escolherPaleta(id: string, nome: string) {
    if (modo === 'claro') {
      const paleta = PALETAS.claro.find((item) => item.id === id);
      if (paleta) void definirPaletaClara(paleta.id);
    } else {
      const paleta = PALETAS.escuro.find((item) => item.id === id);
      if (paleta) void definirPaletaEscura(paleta.id);
    }
    AccessibilityInfo.announceForAccessibility(`Paleta ${nome}`);
  }

  return (
    <View style={{ gap: espaco.grande }}>
      <View accessibilityRole="radiogroup" style={{ flexDirection: 'row', gap: espaco.medio }}>
        {(['claro', 'escuro'] as const).map((opcao) => {
          const selecionado = modo === opcao;
          const nome = opcao === 'claro' ? 'Claro' : 'Escuro';
          return (
            <Pressable
              key={opcao}
              onPress={() => escolherModo(opcao)}
              accessibilityRole="radio"
              accessibilityLabel={`Modo ${nome.toLowerCase()}`}
              accessibilityState={{ checked: selecionado, selected: selecionado }}
              style={({ pressed }) => ({
                flex: 1,
                minHeight: medidas.toqueMinimo,
                alignItems: 'center',
                gap: espaco.pequeno,
                padding: espaco.medio,
                borderRadius: raio.medio,
                backgroundColor: cores.superficie,
                // A borda grossa na escolhida, junto com o "Em uso": a cor não é a única pista.
                borderWidth: selecionado ? 3 : 1,
                borderColor: selecionado ? cores.primaria : cores.trilha,
                opacity: pressed ? 0.85 : 1,
              })}
            >
              <Miniatura cores={coresDoModo(opcao, paletaClara, paletaEscura)} />
              <Texto variante="rotulo">{nome}</Texto>
              {selecionado ? <MarcaEmUso /> : null}
            </Pressable>
          );
        })}
      </View>

      <View style={{ gap: espaco.pequeno }}>
        <Texto variante="rotulo">{modo === 'claro' ? 'Cores do modo claro' : 'Cores do modo escuro'}</Texto>
        <View accessibilityRole="radiogroup" style={{ gap: espaco.pequeno }}>
          {paletas.map((paleta) => {
            const selecionada = paleta.id === paletaEmUso;
            return (
              <Pressable
                key={paleta.id}
                onPress={() => escolherPaleta(paleta.id, paleta.nome)}
                accessibilityRole="radio"
                accessibilityLabel={`Paleta ${paleta.nome}`}
                accessibilityState={{ checked: selecionada, selected: selecionada }}
                style={({ pressed }) => ({
                  flexDirection: 'row',
                  alignItems: 'center',
                  gap: espaco.medio,
                  minHeight: medidas.alturaBotao,
                  paddingHorizontal: espaco.medio,
                  paddingVertical: espaco.pequeno,
                  borderRadius: raio.medio,
                  backgroundColor: cores.superficie,
                  borderWidth: selecionada ? 3 : 1,
                  borderColor: selecionada ? cores.primaria : cores.trilha,
                  opacity: pressed ? 0.85 : 1,
                })}
              >
                {/* Amostras: fundo, superfície, cor principal e laranja da marca (só desenho). */}
                <View
                  accessible={false}
                  importantForAccessibility="no-hide-descendants"
                  style={{ flexDirection: 'row', gap: 4 }}
                >
                  {[paleta.cores.fundo, paleta.cores.superficie, paleta.cores.primaria, paleta.cores.destaque].map((amostra, indice) => (
                    <View
                      key={indice}
                      style={{
                        width: 24,
                        height: 24,
                        borderRadius: 12,
                        backgroundColor: amostra,
                        borderWidth: 1,
                        borderColor: cores.borda,
                      }}
                    />
                  ))}
                </View>
                <Texto variante="rotulo" style={{ flex: 1 }}>
                  {paleta.nome}
                </Texto>
                {selecionada ? <MarcaEmUso /> : null}
              </Pressable>
            );
          })}
        </View>
      </View>
    </View>
  );
}
