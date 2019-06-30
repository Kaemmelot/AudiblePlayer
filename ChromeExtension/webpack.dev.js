const path = require('path');
const merge = require('webpack-merge');
const common = require('./webpack.common.js');

module.exports = merge(common, {
    devtool: 'inline-source-map',
    mode: 'development',
    resolve: {
        alias: {
            "jsmediatags": path.join(__dirname, 'node_modules/jsmediatags/dist/jsmediatags.js')
        }
    },
});
