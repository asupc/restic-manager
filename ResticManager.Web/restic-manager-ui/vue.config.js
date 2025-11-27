module.exports = {
  transpileDependencies: [],
  publicPath: "/",
  outputDir: "../../restic-manager-release/wwwroot",
  productionSourceMap: false,
  devServer: {
    port: 8080,
    proxy: {
      "/api": {
        target: "http://localhost:5050",
        ws: true,
        changeOrigin: true,
      },
    },
  },
};
