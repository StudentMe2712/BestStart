import { Platform } from 'react-native';

export const COLORS = {
  // Core Haute Cuisine Tokens
  obsidianCanvas: '#0E1013',
  cardSurface: '#16191F',
  champagneAccent: '#E5A962',
  burgundy: '#8C2D38',
  burgundySubAccent: '#8C2D38',
  sageGreen: '#5E8C6A',
  textPrimary: '#F5F6F8',
  textSecondary: '#8E95A5',

  // Glassmorphism & Translucency
  glassBorder: 'rgba(255, 255, 255, 0.08)',
  glassBorderSolid: '#262B35',
  glassBackground: 'rgba(22, 25, 31, 0.85)',
  glassBackgroundSubtle: 'rgba(22, 25, 31, 0.60)',
  glassSheen: 'rgba(255, 255, 255, 0.04)',

  // Supplementary Semantic Colors
  slateBlue: '#4A6B82',
  oceanTeal: '#2C7A7B',
  goldHighlight: '#F7D89C',
  goldMuted: '#BF823D',
  burgundyLight: '#B53A49',
  burgundyDark: '#56151E',
  surfaceLight: '#1A1E26',
  surfaceDark: '#12151B',
  // V2 Dark Luxe Design Tokens
  bgDark: '#0A0B0E',
  cardSurfaceV2: '#13161F',
  borderV2: '#1E2330',
  goldPrimary: '#D4A373',
  goldSecondary: '#E5A962',
  statusSuccess: '#2EC4B6',
  textPrimaryV2: '#F8F9FA',
  textSecondaryV2: '#8E95A5',
} as const;

export const FONTS = {
  serif: Platform.select({
    ios: 'Georgia',
    android: 'serif',
    web: "Georgia, Cambria, 'Times New Roman', Times, serif",
    default: 'serif',
  }),
  sans: Platform.select({
    ios: 'System',
    android: 'sans-serif',
    web: "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif",
    default: 'sans-serif',
  }),
  mono: Platform.select({
    ios: 'Menlo',
    android: 'monospace',
    web: "SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace",
    default: 'monospace',
  }),
} as const;

export const TYPOGRAPHY = {
  largeTitle: {
    fontFamily: FONTS.serif,
    fontSize: 32,
    lineHeight: 38,
    fontWeight: '700' as const,
    color: COLORS.textPrimary,
  },
  dishTitle: {
    fontFamily: FONTS.serif,
    fontSize: 22,
    lineHeight: 28,
    fontWeight: '700' as const,
    color: COLORS.textPrimary,
  },
  title3: {
    fontFamily: FONTS.serif,
    fontSize: 18,
    lineHeight: 24,
    fontWeight: '600' as const,
    color: COLORS.textPrimary,
  },
  headline: {
    fontFamily: FONTS.serif,
    fontSize: 16,
    lineHeight: 22,
    fontWeight: '600' as const,
    color: COLORS.champagneAccent,
  },
  subheadline: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    lineHeight: 20,
    fontWeight: '400' as const,
    color: COLORS.textSecondary,
  },
  body: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    lineHeight: 22,
    fontWeight: '400' as const,
    color: COLORS.textSecondary,
  },
  price: {
    fontFamily: FONTS.mono,
    fontSize: 20,
    lineHeight: 24,
    fontWeight: '700' as const,
    color: COLORS.champagneAccent,
  },
  cardPrice: {
    fontFamily: FONTS.mono,
    fontSize: 15,
    lineHeight: 20,
    fontWeight: '700' as const,
    color: COLORS.champagneAccent,
  },
  microLabel: {
    fontFamily: FONTS.mono,
    fontSize: 10,
    lineHeight: 14,
    fontWeight: '700' as const,
    letterSpacing: 1.5,
    color: COLORS.champagneAccent,
  },
  caption: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    lineHeight: 16,
    fontWeight: '400' as const,
    color: COLORS.textSecondary,
  },
} as const;

export const SPACING = {
  xxs: 2,
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  xxl: 24,
  xxxl: 32,
} as const;

export const RADIUS = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  cardV2: 20,
  xxl: 24,
  full: 9999,
} as const;

export const SHADOWS = {
  subtle: {
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.35,
    shadowRadius: 8,
    elevation: 4,
  },
  card: {
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.55,
    shadowRadius: 16,
    elevation: 8,
  },
  glowChampagne: {
    shadowColor: COLORS.champagneAccent,
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.3,
    shadowRadius: 12,
    elevation: 6,
  },
  glowBurgundy: {
    shadowColor: COLORS.burgundy,
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.3,
    shadowRadius: 12,
    elevation: 6,
  },
} as const;

export const THEME = {
  colors: COLORS,
  fonts: FONTS,
  typography: TYPOGRAPHY,
  spacing: SPACING,
  radius: RADIUS,
  shadows: SHADOWS,
} as const;

export default THEME;
