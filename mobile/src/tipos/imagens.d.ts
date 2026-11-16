/**
 * Permite `import logo from '.../logo.png'` com tipo. O Metro troca cada PNG por uma
 * referência ao arquivo empacotado no app, que o `<Image source>` do React Native aceita.
 */
declare module '*.png' {
  const imagem: import('react-native').ImageSourcePropType;
  export default imagem;
}
