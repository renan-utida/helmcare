import { Redirect } from 'expo-router';

import { Carregando } from '@/componentes/Carregando';
import { useSessao } from '@/contextos/SessaoContext';
import { useAberturaMinima } from '@/hooks/useAberturaMinima';

/**
 * Porta de entrada. Na primeira abertura, a abertura com o logo fica pelo menos 2 s (ou até a
 * sessão carregar, se demorar mais), porque o Expo Go não mostra o splash do app.
 * - Sem conta salva no aparelho: vai para o login.
 * - Com conta ativa: espera o `GET /entregadores/sessao`. Sessão terminada (renovação com 401):
 *   login com o aviso. Sessão ok, sem conexão ou com erro: vai para a aba Início (sem rede, o app
 *   abre com a conta salva, sem nome, e o histórico funciona; a conta nunca é apagada por falta de
 *   rede). O TurnoContext assume o turno ativo da sessão e abre a ocorrência ativa, se houver.
 */
export default function Inicio() {
  const { carregando, contaAtiva, estadoSessao } = useSessao();
  const aberturaPronta = useAberturaMinima();
  if (carregando || !aberturaPronta || (contaAtiva !== null && estadoSessao === 'carregando')) {
    return <Carregando comLogo />;
  }
  if (!contaAtiva) {
    return <Redirect href="/login" />;
  }
  if (estadoSessao === 'terminada') {
    return <Redirect href={{ pathname: '/login', params: { motivo: 'sessao' } }} />;
  }
  return <Redirect href="/inicio" />;
}
