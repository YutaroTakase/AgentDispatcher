export default defineNuxtConfig({
  compatibilityDate: '2026-09-01',
  devtools: { enabled: false },
  modules: ['@nuxt/eslint'],
  ssr: false,
  typescript: {
    strict: true
  }
})
