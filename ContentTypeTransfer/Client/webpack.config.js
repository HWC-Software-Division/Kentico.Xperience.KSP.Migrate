const path         = require("path");
const webpackMerge = require("webpack-merge");
const baseConfig   = require("@kentico/xperience-webpack-config");

module.exports = (opts, argv) => {
  const projectConfig = {
    entry: "./src/entry.tsx",
    output: {
      path:       path.resolve(__dirname, "dist"),
      publicPath: "/",  // Prevent systemjs-webpack-interop injection
    },
    module: {
      rules: [{
        test: /\.[tj]sx?$/,
        exclude: /node_modules/,
        use: {
          loader: "babel-loader",
          options: {
            presets: [
              ["@babel/preset-env",       { targets: "defaults", modules: false }],
              ["@babel/preset-react",     { runtime: "classic" }],
              ["@babel/preset-typescript"],
            ],
            plugins: ["@babel/plugin-transform-runtime"],
          },
        },
      }],
    },
    resolve: { extensions: [".ts", ".tsx", ".js", ".jsx"] },
    devServer: { port: 3070 },
  };

  return webpackMerge.merge(projectConfig, baseConfig(opts, argv));
};
