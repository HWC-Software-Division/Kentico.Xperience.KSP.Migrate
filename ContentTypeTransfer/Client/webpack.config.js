const path = require("path");

module.exports = (env, argv) => {
  const isProd = argv?.mode === "production";

  return {
    mode: isProd ? "production" : "development",
    entry: "./src/entry.tsx",
      output: {
          path: path.resolve(__dirname, "dist"),
          filename: "entry.js",   // ← เปลี่ยนจาก module.js
          library: { type: "system" },
          clean: true,
      },
    resolve: {
      extensions: [".ts", ".tsx", ".js", ".jsx"],
    },
    module: {
      rules: [
        {
          test: /\.[tj]sx?$/,
          exclude: /node_modules/,
          use: {
            loader: "babel-loader",
            options: {
              presets: [
                [
                  "@babel/preset-env",
                  { targets: "defaults", modules: false }
                ],
                [
                  "@babel/preset-react",
                  {
                      runtime: "classic",  // ← ไม่ต้อง import React ทุกไฟล์
                    development: !isProd,
                  }
                ],
                [
                  "@babel/preset-typescript",
                  {}
                ],
              ],
              plugins: [
                "@babel/plugin-transform-runtime"
              ],
            },
          },
        },
      ],
    },
      externals: [
          function ({ request }, callback) {
              // ทุก @kentico/* และ react ต้อง external
              if (request === 'react' ||
                  request === 'react-dom' ||
                  request?.startsWith('@kentico/')) {
                  return callback(null, 'system ' + request);
              }
              callback();
          }
      ],
    devtool: isProd ? false : "source-map",
    devServer: { port: 3070 },
  };
};
