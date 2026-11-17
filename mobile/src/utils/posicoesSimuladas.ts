/**
 * Posições simuladas do voluntário, as mesmas três da evidência da API. São dados simulados,
 * como pede o contrato (seção 2): o app não usa o GPS do aparelho. A queda de referência é a do
 * simulador do capacete, na Avenida Paulista (-23.5641, -46.6525).
 *
 * A posição escolhida fica só em memória (ChamadosContext), nunca no SQLite: coordenada de
 * pessoa não vai para o banco local (CLAUDE.md, "Banco local").
 */

/** Código de uma posição simulada. */
export type IdPosicaoSimulada = 'V1' | 'V2' | 'V3';

/** Uma posição simulada, com a distância que a API deve devolver até a queda de referência. */
export interface PosicaoSimulada {
  id: IdPosicaoSimulada;
  /** Nome do lugar, mostrado no botão. */
  nome: string;
  latitude: number;
  longitude: number;
  /** Distância esperada no `distanciaKm`, em km (até a localização aproximada da queda). */
  distanciaEsperadaKm: number;
  /** Verdadeiro quando fica fora do raio da camada 1 (2 km) e só vê o chamado na camada 2. */
  soCamada2: boolean;
}

/** As três posições, na ordem dos botões: da mais perto para a mais longe da queda. */
export const POSICOES_SIMULADAS: readonly PosicaoSimulada[] = [
  { id: 'V1', nome: 'MASP', latitude: -23.5614, longitude: -46.6559, distanciaEsperadaKm: 0.6, soCamada2: false },
  { id: 'V2', nome: 'Consolação', latitude: -23.556, longitude: -46.662, distanciaEsperadaKm: 1.3, soCamada2: false },
  { id: 'V3', nome: 'Pinheiros', latitude: -23.567, longitude: -46.693, distanciaEsperadaKm: 4.5, soCamada2: true },
];

/** Códigos das posições, para o grupo de botões. */
export const IDS_POSICOES: readonly IdPosicaoSimulada[] = POSICOES_SIMULADAS.map((posicao) => posicao.id);

/** Busca a posição pelo código. As três sempre existem, então nunca devolve vazio. */
export function posicaoSimulada(id: IdPosicaoSimulada): PosicaoSimulada {
  const posicao = POSICOES_SIMULADAS.find((item) => item.id === id);
  if (!posicao) {
    // Não acontece: o tipo só admite os três códigos da lista.
    throw new Error(`Posição simulada desconhecida: ${id}`);
  }
  return posicao;
}
