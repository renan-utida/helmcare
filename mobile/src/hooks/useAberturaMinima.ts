import { useEffect, useState } from 'react';

/** Tempo mínimo da abertura com o logo na primeira abertura do app, em milissegundos. */
const DURACAO_MINIMA_MS = 2000;

/**
 * Instante em que a abertura com o logo apareceu pela primeira vez e se ela já cumpriu o
 * tempo mínimo. Ficam fora dos componentes porque a abertura passa por duas telas (o
 * carregamento do banco, na raiz, e o da sessão, no index) e o tempo conta da primeira.
 */
let inicioAbertura: number | null = null;
let aberturaConcluida = false;

/** Marca o momento em que a abertura com o logo apareceu (só a primeira vez conta). */
export function marcarInicioAbertura(): void {
  if (inicioAbertura === null) {
    inicioAbertura = Date.now();
  }
}

/**
 * Devolve `true` quando a abertura já ficou pelo menos 2 s na tela, contando de quando ela
 * apareceu. Se o carregamento demorar mais, quem chama continua mostrando a abertura até
 * terminar. Depois da primeira vez, devolve `true` na hora (não atrasa mais nada).
 */
export function useAberturaMinima(): boolean {
  const [pronta, setPronta] = useState(aberturaConcluida);

  useEffect(() => {
    if (aberturaConcluida) {
      return;
    }
    marcarInicioAbertura();
    const decorrido = Date.now() - (inicioAbertura ?? Date.now());
    const id = setTimeout(
      () => {
        aberturaConcluida = true;
        setPronta(true);
      },
      Math.max(0, DURACAO_MINIMA_MS - decorrido),
    );
    return () => clearTimeout(id);
  }, []);

  return pronta;
}
