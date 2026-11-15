import { EstadoVazio } from '@/componentes/EstadoVazio';
import { Tela } from '@/componentes/Tela';

/** Aba Histórico: a próxima entrega traz os turnos, alertas e ocorrências salvos no aparelho. */
export default function Historico() {
  return (
    <Tela dentroDeAbas>
      <EstadoVazio
        titulo="Disponível em breve"
        mensagem="Aqui você vai ver seus turnos, alertas e ocorrências, mesmo sem internet."
        icone="time-outline"
      />
    </Tela>
  );
}
