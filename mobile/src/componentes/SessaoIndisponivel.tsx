import { useState } from 'react';

import { useSessao } from '@/contextos/SessaoContext';

import { Carregando } from './Carregando';
import { EstadoErro } from './EstadoErro';
import { Tela } from './Tela';

/**
 * Tela de uma aba que depende da sessão da conta (`GET /entregadores/sessao`) quando ela ainda não
 * chegou: carregando, ou a mensagem de sem conexão com "Tentar de novo". A conta continua salva:
 * a falta de rede nunca apaga a conta.
 */
export function SessaoIndisponivel() {
  const { estadoSessao, recarregarSessao } = useSessao();
  const [tentando, setTentando] = useState(false);

  if (estadoSessao === 'carregando') {
    return <Carregando mensagem="Carregando sua conta..." />;
  }

  async function tentarDeNovo() {
    setTentando(true);
    await recarregarSessao();
    setTentando(false);
  }

  const mensagem =
    estadoSessao === 'sem-conexao'
      ? 'Sem conexão: não deu para carregar seus dados. O histórico funciona sem internet.'
      : 'Não deu para carregar seus dados agora.';
  return (
    <Tela dentroDeAbas>
      <EstadoErro mensagem={mensagem} aoTentarDeNovo={() => void tentarDeNovo()} tentando={tentando} />
    </Tela>
  );
}
