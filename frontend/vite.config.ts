import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    // Calls to /api are proxied to the ASP.NET Core API during development.
    proxy: { "/api": "http://localhost:5080" },
  },
});
