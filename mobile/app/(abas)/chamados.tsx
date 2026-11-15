import { EstadoVazio } from '@/componentes/EstadoVazio';
import { Tela } from '@/componentes/Tela';

/** Aba Chamados: a próxima entrega traz a rede de voluntários e os chamados próximos. */
export default function Chamados() {
  return (
    <Tela dentroDeAbas>
      <EstadoVazio
        titulo="Disponível em breve"
        mensagem="Aqui você vai ver os pedidos de ajuda de entregadores perto de você."
        icone="people-outline"
      />
    </Tela>
  );
}
