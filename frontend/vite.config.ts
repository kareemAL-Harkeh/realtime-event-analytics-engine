import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return defineConfig({
    plugins: [react(), tailwindcss()],
    server: {
      port: Number(env.VITE_PORT) || 5173,
      proxy: {
        '/api': {
          target: 'http://localhost:5261',
          changeOrigin: true,
          secure: false,
        },
        '/eventHub': {
          target: 'http://localhost:5261',
          changeOrigin: true,
          ws: true,
          secure: false,
        },
      },
    },
  })
})
