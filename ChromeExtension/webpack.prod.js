const path = require('path');
const merge = require('webpack-merge');
const common = require('./webpack.common.js');
const TerserJSPlugin = require('terser-webpack-plugin');
const OptimizeCSSAssetsPlugin = require('optimize-css-assets-webpack-plugin');

function recursiveIssuer(m) {
    if (m.issuer) {
        return recursiveIssuer(m.issuer);
    } else if (m.name) {
        return m.name;
    } else {
        return false;
    }
}

module.exports = merge(common, {
    optimization: {
        minimizer: [new TerserJSPlugin({}), new OptimizeCSSAssetsPlugin({})],
        splitChunks: {
            cacheGroups: {
                offPlayerStyle: {
                    name: 'offPlayerStyle',
                    test: (m, c, entry = 'offPlayerStyle') => m.constructor.name === 'CssModule' && recursiveIssuer(m) === entry,
                    chunks: 'all',
                    enforce: true
                },
            },
        }
    },
    mode: 'production',
    resolve: {
        alias: {
            "jsmediatags": path.join(__dirname, 'node_modules/jsmediatags/dist/jsmediatags.min.js')
        }
    },
});
