import { View } from 'react-native';

import { useTema } from '@/contextos/TemaContext';
import type { ComponentesRisco, FaixaRisco as Faixa } from '@/tipos/contrato';

import { FaixaRisco, TEXTO_FAIXA } from './FaixaRisco';
import { Texto } from './Texto';

/** Dados do Índice que o componente mostra (vem da API ou do turno salvo no SQLite). */
export interface DadosIndiceRisco {
  indiceRisco: number;
  faixaRisco: Faixa;
  componentes: ComponentesRisco;
  explicacao: string;
}

/** Frase que explica a escala das barras. */
const FRASE_ESCALA = 'Cada fator recebe uma nota de 0 a 100.';

/**
 * Nome de cada componente do Índice, em português simples, e a legenda da escala
 * (as normalizações da seção 8 do contrato: chuva de 0 a 10 mm/h; calor de 27 a 41 °C).
 */
const NOMES_COMPONENTES: { chave: keyof ComponentesRisco; nome: string; legenda: string | null }[] = [
  { chave: 'sinistro', nome: 'Histórico de sinistros no horário', legenda: null },
  { chave: 'chuva', nome: 'Chuva prevista no turno', legenda: '0 sem chuva, 100 a partir de 10 mm por hora' },
  {
    chave: 'calor',
    nome: 'Calor previsto no turno',
    legenda: '0 abaixo de 27 °C de índice de calor, 100 a partir de 41 °C',
  },
];

/** Limita o valor a 0 a 100, para a barra nunca passar do tamanho. */
function limitar(valor: number): number {
  return Math.max(0, Math.min(100, valor));
}

/** Monta a frase única que o leitor de tela lê para o Índice inteiro. */
export function fraseIndiceRisco(dados: DadosIndiceRisco): string {
  // Na leitura, "°C" vira "graus" e a legenda vai entre parênteses depois da nota.
  const partes = NOMES_COMPONENTES.map(({ chave, nome, legenda }) => {
    const nota = `${nome}: ${dados.componentes[chave]} de 100`;
    return legenda ? `${nota} (${legenda.replace(/ °C/g, ' graus')})` : nota;
  });
  return (
    `Índice de risco do turno: ${dados.indiceRisco} de 100, ${TEXTO_FAIXA[dados.faixaRisco].toLowerCase()}. ` +
    `${dados.explicacao} ${FRASE_ESCALA} ${partes.join('. ')}.`
  );
}

/** Props do `IndiceRisco`. */
export interface IndiceRiscoProps {
  dados: DadosIndiceRisco;
  /** Anuncia a frase de novo quando o Índice muda (região viva do leitor de tela). */
  anunciarMudancas?: boolean;
}

/**
 * Índice de Risco do Turno: número grande ("60 de 100"), selo da faixa, explicação e as
 * três barras dos componentes, cada uma com o texto do valor. Para o leitor de tela,
 * o bloco é um só elemento com uma frase completa.
 */
export function IndiceRisco({ dados, anunciarMudancas = false }: IndiceRiscoProps) {
  const { cores, espaco, raio } = useTema();
  return (
    <View
      accessible
      accessibilityLabel={fraseIndiceRisco(dados)}
      accessibilityLiveRegion={anunciarMudancas ? 'polite' : 'none'}
      style={{ gap: espaco.medio }}
    >
      <Texto variante="legenda">Índice de risco do turno</Texto>
      <View style={{ flexDirection: 'row', alignItems: 'baseline', flexWrap: 'wrap', gap: espaco.pequeno }}>
        <Texto variante="destaque">{dados.indiceRisco}</Texto>
        <Texto variante="subtitulo" cor="textoSecundario">
          de 100
        </Texto>
      </View>
      <FaixaRisco faixa={dados.faixaRisco} />
      <Texto>{dados.explicacao}</Texto>
      <View style={{ gap: espaco.medio }}>
        <Texto variante="legenda">{FRASE_ESCALA}</Texto>
        {NOMES_COMPONENTES.map(({ chave, nome, legenda }) => {
          const valor = dados.componentes[chave];
          return (
            <View key={chave} style={{ gap: espaco.pequeno / 2 }}>
              <Texto variante="rotulo">{`${nome}: ${valor} de 100`}</Texto>
              {legenda ? <Texto variante="legenda">{legenda}</Texto> : null}
              {/* A barra é só visual: o valor já está no texto acima. */}
              <View
                style={{
                  height: espaco.medio,
                  backgroundColor: cores.trilha,
                  borderRadius: raio.pequeno,
                  overflow: 'hidden',
                }}
              >
                <View
                  style={{
                    width: `${limitar(valor)}%`,
                    height: '100%',
                    backgroundColor: cores.primaria,
                  }}
                />
              </View>
            </View>
          );
        })}
      </View>
    </View>
  );
}
