/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './entrypoints/**/*.{html,ts,tsx}',
    './components/**/*.{ts,tsx}',
    './lib/**/*.{ts,tsx}',
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#fff8e1',
          100: '#ffeeb3',
          400: '#ffca28',
          500: '#f59e0b',
          600: '#d97706',
        },
      },
    },
  },
  plugins: [],
};
