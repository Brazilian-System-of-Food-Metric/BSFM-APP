import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// O front fala com a API pelo próprio servidor (proxy), então não há CORS
// e a assinatura por sessão nunca precisa de segredo global no bundle.
const destino = process.env.BSFM_API ?? 'http://127.0.0.1:5100'

export default defineConfig({
  plugins: [react()],
  server: { port: 5173, proxy: { '/api': { target: destino, changeOrigin: true } } },
  preview: { port: 4173, proxy: { '/api': { target: destino, changeOrigin: true } } },
})
