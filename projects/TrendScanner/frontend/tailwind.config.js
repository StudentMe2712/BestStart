/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        app: {
          bg: '#090B10',       // Background
          surface: '#0F1219',  // Surface
          elevated: '#151922', // Surface Elevated (карточки, модалки)
          hover: '#1B202B',    // Surface Hover (наведение на строки)
          border: '#252B36',   // Border
        },
        content: {
          primary: '#F1F3F5',  // Text Primary (заголовки, важные данные)
          secondary: '#A5ACB8',// Text Secondary (описания, второстепенное)
          muted: '#687180',    // Text Muted (плейсхолдеры, мелкие подписи)
        },
        brand: {
          DEFAULT: '#7C3AED',  // Primary (Фиолетовый)
          hover: '#8B5CF6',    // Primary Hover
        },
        status: {
          success: '#10B981',  // Позитив / Чисто / Score > 8
          warning: '#F59E0B',  // Внимание / Риск 20-50%
          danger: '#EF4444',   // Проблема / Риск > 50% / Ошибки
        },
      },
      fontFamily: {
        mono: ['JetBrains Mono', 'Fira Code', 'monospace'],
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
    },
  },
  plugins: [],
}
