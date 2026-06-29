import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const backendTarget = 'http://127.0.0.1:8877';

/**
 * Vite configuration for the development kiosk shell.
 * The ASP.NET Core host serves the built output for packaged operation.
 */
export default defineConfig({
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: false,
    proxy: {
      '/api': backendTarget,
      '/config': backendTarget,
      '/configuration.html': backendTarget,
      '/configuration.js': backendTarget,
      '/styles.css': backendTarget,
      '/favicon.ico': backendTarget,
      '/brand': backendTarget,
      '/hubs': {
        target: backendTarget,
        ws: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    rollupOptions: {
      onwarn(warning, defaultHandler) {
        if (warning.code === 'INVALID_ANNOTATION' && warning.id?.includes('@microsoft/signalr')) {
          return;
        }

        defaultHandler(warning);
      },
    },
  },
});