import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import fs from 'fs';
import path from 'path';
import child_process from 'child_process';
import { env } from 'process';

const baseFolder =
    env.APPDATA !== undefined && env.APPDATA !== ''
        ? `${env.APPDATA}/ASP.NET/https`
        : `${env.HOME}/.aspnet/https`;


const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
    env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'http://localhost:5227';

// https://vitejs.dev/config/
export default defineConfig({
    base: "./",
    plugins: [plugin()],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    server: {
        proxy: {
            '^/prepareblob': {
                target: 'https://localhost:32773/',
                secure: false
            },
            '^/createcopy': {
                target: 'https://localhost:32773/',
                secure: false
            },
            '^/getcolortheme': {
                target: 'https://localhost:32773/',
                secure: false
            },
            '^/getbackgrounddescription': {
                target: 'https://localhost:32773/',
                secure: false
            },
            '^/generatebackgrounds': {
                target: 'https://localhost:32773/',
                secure: false
            },
            '^/removebackgroundandcrop': {
                target: 'https://localhost:32773/',
                secure: false
            },
            '^/combineimages': {
                target: 'https://localhost:32773/',
                secure: false
            }
        },
        port: 5173
    }
})
