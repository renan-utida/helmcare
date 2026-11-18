import { router, type Href } from 'expo-router';

/**
 * Começa uma pilha nova no destino: fecha todas as telas da pilha raiz (`dismissAll`) e troca a
 * que sobrou pelo destino (`replace`). Assim, o voltar do Android não leva de volta ao login, ao
 * cadastro ou às abas de uma conta que saiu.
 *
 * Por que não só o `dismissTo('/inicio')`: quando a rota não está na pilha, o `POP_TO` do React
 * Navigation troca só a tela de cima. Com a pilha [login, cadastro], o fim do cadastro ficava
 * [login, abas], e o voltar nas abas reabria o login. Com o `replace('/login')` de dentro de um
 * modal, a pilha ficava [abas, login] e o voltar no login mostrava as abas sem conta.
 *
 * Usado ao entrar (login e cadastro) e ao ir para o login (sair, fim da sessão, sem conta).
 */
export function comecarPilhaEm(destino: Href): void {
  if (router.canDismiss()) {
    router.dismissAll();
  }
  router.replace(destino);
}
