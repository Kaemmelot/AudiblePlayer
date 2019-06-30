const path = require('path');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');

module.exports = {
    entry: {
        background: path.join(__dirname, 'src/background.ts'),
        inject: [
            path.join(__dirname, 'src/AudibleControl.ts'),
            path.join(__dirname, 'src/inject.ts')
        ],
        "offline-inject": [
            path.join(__dirname, 'src/OfflinePlayer.ts'),
            path.join(__dirname, 'src/inject.ts')
        ],
        offline: [
            path.join(__dirname, 'node_modules/jquery/dist/jquery.min.js'),
            path.join(__dirname, 'lib/foundation-6.5.1.min.js'),
            path.join(__dirname, 'src/offline.ts')
        ],
        offPlayerStyle: [
            path.join(__dirname, 'lib/foundation-6.5.1.min.css'),
            path.join(__dirname, 'ampExample/app.scss')
        ]
    },
    output: {
        path: path.join(__dirname, 'dist')
    },
    /*optimization: {
        splitChunks: {
            cacheGroups: {
                commons: {
                    test: /(node_modules\/(jsmediatags|amplitudejs|jquery)|lib\/)/,
                    name: 'offline-vendors',
                    chunks: 'all'
                }
            }
        }
    },*/
    module: {
        rules: [
            {
                test: /\.ts$/,
                use: 'ts-loader',
                exclude: /node_modules/
            },
            {
                test: /\.min\.js$/,
                use: 'script-loader',
                include: /(node_modules\/(jsmediatags|amplitudejs|jquery)|lib\/)/
            },
            {
                test: /\.(sa|sc|c)ss$/,
                use: [
                    MiniCssExtractPlugin.loader,
                    'css-loader',
                    'sass-loader'
                ],
                exclude: /node_modules/
            },
            {
                test: /\.(png|jpe?g|gif)$/i,
                use: [
                    {
                        loader: 'url-loader',
                        options: {
                            limit: 8192,
                        },
                    },
                ],
            },
            {
                test: /\.svg/,
                use: 'svg-url-loader'
            }
        ]
    },
    resolve: {
        extensions: ['.ts', '.js', '.scss', '.sass', 'css'],
        alias: {
            //"Amplitude": path.join(__dirname, 'node_modules/amplitudejs/dist/amplitude.min.js'),
            "foundation": path.join(__dirname, 'lib/foundation-6.5.1.min.js')
        }
    },
    plugins: [
        new MiniCssExtractPlugin({
            filename: '[name].css',
        }),
    ],
};
