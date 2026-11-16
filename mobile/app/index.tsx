import { Redirect } from 'expo-router';

import { Carregando } from '@/componentes/Carregando';
import { useSessao } from '@/contextos/SessaoContext';
import { useAberturaMinima } from '@/hooks/useAberturaMinima';

/**
 * Porta de entrada: sem perfil no aparelho, vai para o cadastro; com perfil, para a aba Turno.
 * Na primeira abertura, a abertura com o logo fica pelo menos 2 s (ou até a sessão carregar,
 * se demorar mais), porque o Expo Go não mostra o splash do app.
 */
export default function Inicio() {
  const { carregando, perfilAtivo } = useSessao();
  const aberturaPronta = useAberturaMinima();
  if (carregando || !aberturaPronta) {
    return <Carregando comLogo />;
  }
  if (!perfilAtivo) {
    return <Redirect href="/cadastro" />;
  }
  return <Redirect href="/turno" />;
}
