/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Pages/**/*.cshtml',
    './Views/**/*.cshtml',
    './Pages/**/*.cs',
  ],
  theme: {
    extend: {
      colors: {
        'srt-navy': {
          DEFAULT: '#0D2E6E',
          light: '#1A4A9E',
          dark: '#081D47',
          50:  '#E8EEF8',
          100: '#C4D2EC',
          200: '#9DB4DE',
          300: '#7596D0',
          400: '#4E78C2',
          500: '#0D2E6E',
          600: '#0B2660',
          700: '#091E52',
          800: '#071644',
          900: '#050E36',
        },
        'srt-gold': {
          DEFAULT: '#C9A227',
          light: '#E4BE4A',
          dark: '#A07D0E',
          50: '#FDF8E7',
          100: '#FAF0C4',
        },
        'slate': {
          925: '#0c1425',
        },
      },
      fontFamily: {
        'thai': ['Noto Sans Thai', 'system-ui', '-apple-system', 'Segoe UI', 'sans-serif'],
      },
      fontSize: {
        'th-sm': ['0.9375rem', { lineHeight: '1.5rem' }],
        'th-base': ['1.0625rem', { lineHeight: '1.75rem' }],
        'th-lg': ['1.1875rem', { lineHeight: '1.875rem' }],
        'th-xl': ['1.375rem', { lineHeight: '2rem' }],
      },
      boxShadow: {
        'glow-navy': '0 0 20px rgba(13,46,110,0.25)',
        'glow-gold': '0 0 20px rgba(201,162,39,0.3)',
        'card': '0 1px 3px rgba(0,0,0,0.06), 0 4px 16px rgba(0,0,0,0.04)',
        'card-hover': '0 4px 12px rgba(0,0,0,0.08), 0 16px 40px rgba(0,0,0,0.06)',
        'inner-glow': 'inset 0 1px 0 rgba(255,255,255,0.1)',
      },
      backgroundImage: {
        'mesh-navy': 'radial-gradient(at 40% 20%, rgba(26,74,158,0.4) 0px, transparent 50%), radial-gradient(at 80% 0%, rgba(13,46,110,0.6) 0px, transparent 50%), radial-gradient(at 0% 50%, rgba(5,14,54,0.4) 0px, transparent 50%)',
        'mesh-subtle': 'radial-gradient(at 20% 80%, rgba(13,46,110,0.05) 0px, transparent 50%), radial-gradient(at 80% 20%, rgba(201,162,39,0.05) 0px, transparent 50%)',
        'gradient-navy': 'linear-gradient(135deg, #0D2E6E 0%, #081D47 100%)',
        'gradient-sidebar': 'linear-gradient(180deg, #0a2258 0%, #071644 100%)',
      },
      animation: {
        'fade-in': 'fadeIn 0.3s ease-out',
        'slide-up': 'slideUp 0.4s ease-out',
        'pulse-slow': 'pulse 3s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'shimmer': 'shimmer 2s infinite',
      },
      keyframes: {
        fadeIn: {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
        slideUp: {
          '0%': { opacity: '0', transform: 'translateY(12px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        shimmer: {
          '0%': { backgroundPosition: '-200% 0' },
          '100%': { backgroundPosition: '200% 0' },
        },
      },
      borderRadius: {
        '4xl': '2rem',
      },
      backdropBlur: {
        xs: '2px',
      },
    },
  },
  plugins: [],
};
