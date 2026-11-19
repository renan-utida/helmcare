import { addNotificationResponseReceivedListener, getLastNotificationResponseAsync } from 'expo-notifications';
import { router, usePathname } from 'expo-router';
import { useEffect, useRef, useState } from 'react';

import { configurarNotificacoes, destinoDaResposta, type DestinoNotificacao } from '@/servicos/notificacoes';

/**
 * Prepara as notificações ao abrir o app e, quando a pessoa toca numa delas, abre a tela certa:
 * o alerta de queda, a ocorrência (no papel certo), a aba Chamados ou o turno. Vale com o app
 * aberto, minimizado e também fechado (a última notificação tocada é lida ao abrir). A navegação
 * espera a abertura terminar: na rota "/" (abertura) e no login, o destino fica guardado.
 */
export function useNotificacoesTocadas(): void {
  const caminho = usePathname();
  // Destino guardado numa ref; o número de versão só serve para o efeito rodar de novo.
  const pendente = useRef<DestinoNotificacao | null>(null);
  const [versao, setVersao] = useState(0);
  // Uma notificação tocada não é tratada duas vezes (pelo ouvinte e pela leitura da última).
  const tratadas = useRef(new Set<string>());

  useEffect(() => {
    void configurarNotificacoes();
    const guardar = (identificador: string, destino: DestinoNotificacao | null) => {
      if (!destino || tratadas.current.has(identificador)) {
        return;
      }
      tratadas.current.add(identificador);
      pendente.current = destino;
      setVersao((atual) => atual + 1);
    };
    const assinatura = addNotificationResponseReceivedListener((resposta) =>
      guardar(resposta.notification.request.identifier, destinoDaResposta(resposta)),
    );
    // App aberto pelo toque numa notificação (estava fechado): lê a última resposta.
    // Nenhuma promessa fica sem tratamento: uma falha aqui só deixa de abrir a tela da notificação.
    getLastNotificationResponseAsync()
      .then((resposta) => {
        if (resposta) {
          guardar(resposta.notification.request.identifier, destinoDaResposta(resposta));
        }
      })
      .catch(() => undefined);
    return () => assinatura.remove();
  }, []);

  useEffect(() => {
    const destino = pendente.current;
    if (!destino || caminho === '/' || caminho === '/login') {
      return;
    }
    pendente.current = null;
    switch (destino.tela) {
      case 'alerta':
        if (caminho !== '/alerta') router.push('/alerta');
        break;
      case 'ocorrencia':
        router.push({ pathname: '/ocorrencia/[id]', params: { id: destino.ocorrenciaId, papel: destino.papel } });
        break;
      case 'chamados':
        router.navigate('/chamados');
        break;
      case 'turno':
        router.navigate('/turno/ativo');
        break;
    }
  }, [versao, caminho]);
}
