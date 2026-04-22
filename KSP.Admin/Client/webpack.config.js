const path              = require("path");
const webpackMerge      = require("webpack-merge");
const baseWebpackConfig = require("@kentico/xperience-webpack-config");

module.exports = (webpackConfigEnv, argv) => {
  // Official boilerplate: pass orgName + projectName to baseWebpackConfig
  const baseConfig = (opts, args) => baseWebpackConfig({
    orgName:          "ksp",
    projectName:      "admin",
    webpackConfigEnv: opts,
    argv:             args,
  });

  const projectConfig = {
    entry: "./src/entry.tsx",
    output: {
      path:       path.resolve(__dirname, "dist"),
      publicPath: "/",
    },
    module: {
      rules: [{
        test:    /\.[tj]sx?$/,
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

  return webpackMerge.merge(projectConfig, baseConfig(webpackConfigEnv, argv));
};
