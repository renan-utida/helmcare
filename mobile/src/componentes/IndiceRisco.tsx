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

/** Campos do turno (do SQLite ou restaurado) de onde sai o Índice. */
export interface TurnoComIndice {
  indiceRisco: number;
  faixaRisco: Faixa;
  componenteSinistro: number;
  componenteChuva: number;
  componenteCalor: number;
  explicacao: string;
}

/** Converte o turno no formato que o componente do Índice usa. */
export function dadosDoTurno(turno: TurnoComIndice): DadosIndiceRisco {
  return {
    indiceRisco: turno.indiceRisco,
    faixaRisco: turno.faixaRisco,
    componentes: { sinistro: turno.componenteSinistro, chuva: turno.componenteChuva, calor: turno.componenteCalor },
    explicacao: turno.explicacao,
  };
}

/** Marcas da escala de cada fator, de 0 a 100. */
const MARCAS = [0, 25, 50, 75, 100] as const;

/**
 * Palavra de cada fator pela nota de 0 a 100. Os limites dos acidentes graves são os mesmos da
 * explicação do Serviço de Risco (até 25 baixo, até 50 moderado, acima alto).
 */
function palavraSinistro(nota: number): string {
  if (nota <= 25) return 'Baixo';
  if (nota <= 50) return 'Moderado';
  return 'Alto';
}

/** Chuva: 0 sem chuva, até 25 fraca, até 76 moderada, 77 ou mais forte. */
function palavraChuva(nota: number): string {
  if (nota === 0) return 'Sem chuva';
  if (nota <= 25) return 'Chuva fraca';
  if (nota <= 76) return 'Chuva moderada';
  return 'Chuva forte';
}

/** Calor: 0 sem risco, até 35 atenção, até 70 forte, 71 ou mais extremo. */
function palavraCalor(nota: number): string {
  if (nota === 0) return 'Sem risco de calor';
  if (nota <= 35) return 'Atenção ao calor';
  if (nota <= 70) return 'Calor forte';
  return 'Calor extremo';
}

/**
 * Cada fator do Índice: o nome em português simples, a palavra pela nota e a legenda da escala
 * (as normalizações da seção 8 do contrato: chuva de 0 a 10 mm/h; calor de 27 a 41 °C).
 */
const FATORES: {
  chave: keyof ComponentesRisco;
  nome: string;
  palavra: (nota: number) => string;
  legenda: string;
}[] = [
  {
    chave: 'sinistro',
    nome: 'Acidentes graves no seu horário',
    palavra: palavraSinistro,
    legenda: 'Compara o seu horário com os horários mais perigosos de São Paulo, de 2019 a 2025.',
  },
  {
    chave: 'chuva',
    nome: 'Chuva prevista no turno',
    palavra: palavraChuva,
    legenda: '0 sem chuva, 100 a partir de 10 mm por hora',
  },
  {
    chave: 'calor',
    nome: 'Calor previsto no turno',
    palavra: palavraCalor,
    legenda: '0 abaixo de 27 °C de índice de calor, 100 a partir de 41 °C',
  },
];

/** Limita o valor a 0 a 100, para a barra nunca passar do tamanho. */
function limitar(valor: number): number {
  return Math.max(0, Math.min(100, Math.round(valor)));
}

/**
 * Frase única que o leitor de tela lê para o Índice inteiro: o número geral e a faixa, a
 * explicação e, de cada fator, a palavra (não o número), como a tela mostra.
 */
export function fraseIndiceRisco(dados: DadosIndiceRisco): string {
  const fatores = FATORES.map(({ chave, nome, palavra }) => `${nome}: ${palavra(limitar(dados.componentes[chave])).toLowerCase()}`);
  return (
    `Índice de risco do turno: ${dados.indiceRisco} de 100, ${TEXTO_FAIXA[dados.faixaRisco].toLowerCase()}. ` +
    `${dados.explicacao} ${fatores.join('. ')}.`
  );
}

/**
 * Barra de um fator: a trilha com as marcas em 0, 25, 50, 75 e 100, a parte preenchida até a nota
 * e o marcador no valor, com o número pequeno acima. Nota 0: barra vazia, sem marcador. Tudo
 * visual: a palavra do fator já diz a mesma coisa (e o leitor de tela lê a frase do Índice).
 */
function BarraFator({ nota }: { nota: number }) {
  const { cores, espaco, raio } = useTema();
  const valor = limitar(nota);
  const alturaBarra = espaco.pequeno + 4;
  const tamanhoMarcador = alturaBarra + 8;
  return (
    <View style={{ gap: espaco.pequeno / 2 }}>
      {valor > 0 ? (
        // O número fica sobre o marcador: os espaços dos lados dividem a largura pela nota.
        <View style={{ flexDirection: 'row' }}>
          <View style={{ flex: valor }} />
          <Texto variante="legenda">{String(valor)}</Texto>
          <View style={{ flex: 100 - valor }} />
        </View>
      ) : null}
      <View style={{ height: tamanhoMarcador, justifyContent: 'center' }}>
        <View style={{ height: alturaBarra, backgroundColor: cores.trilha, borderRadius: raio.pequeno, overflow: 'hidden' }}>
          <View style={{ width: `${valor}%`, height: '100%', backgroundColor: cores.primaria }} />
        </View>
        {MARCAS.map((marca) => (
          <View
            key={marca}
            style={{
              position: 'absolute',
              left: `${marca}%`,
              marginLeft: marca === 0 ? 0 : marca === 100 ? -2 : -1,
              width: 2,
              height: tamanhoMarcador,
              backgroundColor: cores.borda,
            }}
          />
        ))}
        {valor > 0 ? (
          <View
            style={{
              position: 'absolute',
              left: `${valor}%`,
              marginLeft: -tamanhoMarcador / 2,
              width: tamanhoMarcador,
              height: tamanhoMarcador,
              borderRadius: tamanhoMarcador / 2,
              backgroundColor: cores.primaria,
              borderWidth: 3,
              borderColor: cores.superficie,
            }}
          />
        ) : null}
      </View>
    </View>
  );
}

/** Props do `IndiceRisco`. */
export interface IndiceRiscoProps {
  dados: DadosIndiceRisco;
  /** Anuncia a frase de novo quando o Índice muda (região viva do leitor de tela). */
  anunciarMudancas?: boolean;
}

/**
 * Índice de Risco do Turno: número grande ("60 de 100"), selo da faixa, explicação e os três
 * fatores, cada um com uma palavra ("Chuva moderada") e a barra com a escala. Para o leitor de
 * tela, o bloco é um só elemento com uma frase completa.
 */
export function IndiceRisco({ dados, anunciarMudancas = false }: IndiceRiscoProps) {
  const { espaco } = useTema();
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
      <View style={{ gap: espaco.grande }}>
        {FATORES.map(({ chave, nome, palavra, legenda }) => {
          const nota = limitar(dados.componentes[chave]);
          return (
            <View key={chave} style={{ gap: espaco.pequeno / 2 }}>
              <Texto variante="legenda">{nome}</Texto>
              <Texto variante="rotulo">{palavra(nota)}</Texto>
              <BarraFator nota={nota} />
              <Texto variante="legenda">{legenda}</Texto>
            </View>
          );
        })}
      </View>
    </View>
  );
}
