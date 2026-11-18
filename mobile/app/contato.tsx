import { router } from 'expo-router';
import { AccessibilityInfo } from 'react-native';

import { EstadoVazio } from '@/componentes/EstadoVazio';
import { FormularioContato } from '@/componentes/FormularioContato';
import { Tela } from '@/componentes/Tela';
import { Texto } from '@/componentes/Texto';
import { useSessao } from '@/contextos/SessaoContext';

/**
 * Modal "Adicionar contato": o mesmo formulário do passo 2 do cadastro.
 * Depois do 201, volta ao Perfil, que recarrega a lista ao ganhar foco.
 */
export default function Contato() {
  const { contaAtiva } = useSessao();
  if (!contaAtiva) {
    return (
      <Tela>
        <EstadoVazio titulo="Nenhuma conta neste aparelho" icone="person-outline" />
      </Tela>
    );
  }
  return (
    <Tela>
      <Texto>Quem devemos avisar se você cair? Essa pessoa recebe o aviso do SOS.</Texto>
      <FormularioContato
        entregadorId={contaAtiva}
        aoSalvar={() => {
          AccessibilityInfo.announceForAccessibility('Contato salvo');
          router.back();
        }}
        aoPular={() => router.back()}
        textoPular="Cancelar"
      />
    </Tela>
  );
}
