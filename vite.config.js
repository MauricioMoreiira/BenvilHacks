import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const apiProxy = {
  '/api': {
    target: 'http://127.0.0.1:5261',
    changeOrigin: true,
  },
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: apiProxy,
  },
  // Sem isto, `vite preview` devolve 404 em /api/* (só `server.proxy` aplica em `npm run dev`).
  preview: {
    proxy: apiProxy,
  },
})
