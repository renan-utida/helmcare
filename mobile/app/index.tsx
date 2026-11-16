import { Redirect } from 'expo-router';

import { Carregando } from '@/componentes/Carregando';
import { useSessao } from '@/contextos/SessaoContext';

/** Porta de entrada: sem perfil no aparelho, vai para o cadastro; com perfil, para a aba Turno. */
export default function Inicio() {
  const { carregando, perfilAtivo } = useSessao();
  if (carregando) {
    return <Carregando comLogo />;
  }
  if (!perfilAtivo) {
    return <Redirect href="/cadastro" />;
  }
  return <Redirect href="/turno" />;
}
