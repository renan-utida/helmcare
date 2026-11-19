/** Parentescos oferecidos em botões no formulário do contato de emergência. */
export const PARENTESCOS = ['Mãe', 'Pai', 'Cônjuge', 'Irmão ou irmã', 'Amigo ou amiga', 'Outro'] as const;

/** Um dos parentescos oferecidos em botões. */
export type Parentesco = (typeof PARENTESCOS)[number];

/** Confere se um texto é um dos parentescos dos botões (o contato editado pode ter outro). */
export function ehParentesco(valor: string): valor is Parentesco {
  return PARENTESCOS.some((parentesco) => parentesco === valor);
}
