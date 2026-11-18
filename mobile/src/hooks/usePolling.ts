import { useEffect, useRef } from 'react';
import { AppState } from 'react-native';

import { INTERVALO_POLLING_APP_S } from '@/config/constantes';

/**
 * Consulta periódica do app (CLAUDE.md, "Polling"): executa `tarefa` na hora e depois a cada
 * `INTERVALO_POLLING_APP_S` segundos, com `setTimeout` encadeado (a próxima só começa quando a
 * anterior termina, então nunca há duas consultas ao mesmo tempo). Para com o app em segundo
 * plano (AppState) e volta ao reabrir; para quando `ativo` fica falso e ao desmontar.
 * Erros da tarefa não param o polling: quem chama trata o erro dentro da tarefa.
 *
 * `chave` identifica de quem é o polling (por exemplo, o `voluntarioId` do perfil ativo). Quando
 * ela muda, o polling anterior é cancelado (inclusive a próxima consulta agendada) e um novo
 * começa na hora: assim, na troca de conta, nenhuma consulta da conta antiga continua rodando.
 */
export function usePolling(
  tarefa: () => Promise<void>,
  ativo: boolean,
  chave: string | null = null,
  intervaloMs: number = INTERVALO_POLLING_APP_S * 1000,
): void {
  // A tarefa mais recente fica numa ref: trocar a função não reinicia o polling.
  const tarefaAtual = useRef(tarefa);
  useEffect(() => {
    tarefaAtual.current = tarefa;
  }, [tarefa]);

  useEffect(() => {
    if (!ativo) {
      return;
    }
    let cancelado = false;
    let rodando = false;
    let temporizador: ReturnType<typeof setTimeout> | null = null;
    let emPrimeiroPlano = AppState.currentState === 'active';

    const agendar = () => {
      if (!cancelado && emPrimeiroPlano) {
        temporizador = setTimeout(() => void ciclo(), intervaloMs);
      }
    };

    const ciclo = async () => {
      if (cancelado || !emPrimeiroPlano || rodando) {
        return;
      }
      rodando = true;
      try {
        await tarefaAtual.current();
      } catch {
        // A tarefa trata os próprios erros; aqui só garante que o polling continua.
      } finally {
        rodando = false;
        agendar();
      }
    };

    const assinatura = AppState.addEventListener('change', (estado) => {
      const ativoAgora = estado === 'active';
      if (ativoAgora && !emPrimeiroPlano) {
        // Voltou para a frente: consulta na hora (se não houver uma em curso).
        emPrimeiroPlano = true;
        void ciclo();
      } else if (!ativoAgora && emPrimeiroPlano) {
        // Foi para segundo plano: cancela a próxima consulta agendada.
        emPrimeiroPlano = false;
        if (temporizador) {
          clearTimeout(temporizador);
          temporizador = null;
        }
      }
    });

    void ciclo();

    return () => {
      cancelado = true;
      if (temporizador) {
        clearTimeout(temporizador);
      }
      assinatura.remove();
    };
    // `chave` entra nas dependências só para reiniciar o polling quando o dono dele muda.
  }, [ativo, chave, intervaloMs]);
}
