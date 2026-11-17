// Configuração do Metro (empacotador do Expo) com o react-native-svg-transformer,
// como manda o README do transformer para o Expo SDK 41 ou mais novo: os arquivos .svg
// deixam de ser imagens comuns e viram componentes React (usados no logo e no símbolo).
const { getDefaultConfig } = require('expo/metro-config');

module.exports = (() => {
  const config = getDefaultConfig(__dirname);

  const { transformer, resolver } = config;

  config.transformer = {
    ...transformer,
    babelTransformerPath: require.resolve('react-native-svg-transformer/expo'),
  };
  config.resolver = {
    ...resolver,
    assetExts: resolver.assetExts.filter((ext) => ext !== 'svg'),
    sourceExts: [...resolver.sourceExts, 'svg'],
  };

  return config;
})();
