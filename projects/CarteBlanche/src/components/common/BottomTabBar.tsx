import React from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Platform,
  Animated,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { UtensilsCrossed, Wine, ShoppingBag, LucideIcon } from 'lucide-react-native';
import { COLORS, FONTS } from '../../constants/theme';
import { useCart } from '../../context/CartContext';

export type TabType = 'menu' | 'sommelier' | 'cart';

export interface BottomTabBarProps {
  activeTab: TabType;
  onTabChange: (tab: TabType) => void;
}

interface TabItemConfig {
  key: TabType;
  label: string;
  Icon: LucideIcon;
}

const TABS: TabItemConfig[] = [
  { key: 'menu', label: 'Меню', Icon: UtensilsCrossed },
  { key: 'sommelier', label: 'Сомелье', Icon: Wine },
  { key: 'cart', label: 'Корзина', Icon: ShoppingBag },
];

/**
 * BottomTabBar component for Carte Blanche V2 Dark Luxe experience.
 * Features 3 navigation tabs (Menu, Sommelier, Cart) with glowing indicators
 * and real-time floating badge for cart count.
 */
export const BottomTabBar: React.FC<BottomTabBarProps> = ({
  activeTab,
  onTabChange,
}) => {
  let insets = { bottom: 0, top: 0, left: 0, right: 0 };
  try {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    insets = useSafeAreaInsets();
  } catch {
    // Fallback if rendered outside SafeAreaProvider
  }

  let itemsCount = 0;
  try {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const cart = useCart();
    itemsCount = cart.itemsCount;
  } catch {
    // Fallback if rendered outside CartProvider
    itemsCount = 0;
  }

  // Microanimation for floating badge
  const badgeScale = React.useRef(new Animated.Value(1)).current;
  const prevCountRef = React.useRef(itemsCount);

  React.useEffect(() => {
    if (itemsCount > prevCountRef.current) {
      Animated.sequence([
        Animated.timing(badgeScale, {
          toValue: 1.35,
          duration: 150,
          useNativeDriver: Platform.OS !== 'web',
        }),
        Animated.spring(badgeScale, {
          toValue: 1,
          tension: 160,
          friction: 5,
          useNativeDriver: Platform.OS !== 'web',
        }),
      ]).start();
    }
    prevCountRef.current = itemsCount;
  }, [itemsCount, badgeScale]);

  const bottomPadding = Math.max(insets.bottom, 16);

  return (
    <View style={[styles.container, { paddingBottom: bottomPadding }]}>
      {TABS.map((tab) => {
        const isActive = activeTab === tab.key;
        const tintColor = isActive ? COLORS.goldPrimary : COLORS.textSecondary;

        return (
          <TouchableOpacity
            key={tab.key}
            style={styles.tabButton}
            onPress={() => onTabChange(tab.key)}
            activeOpacity={0.7}
            accessibilityRole="tab"
            accessibilityState={{ selected: isActive }}
            accessibilityLabel={tab.label}
          >
            {/* Glowing top line indicator for active tab */}
            {isActive && <View style={styles.activeIndicator} />}

            {/* Icon container with soft pill glow when active */}
            <View style={[styles.iconContainer, isActive && styles.iconContainerActive]}>
              <tab.Icon size={22} color={tintColor} strokeWidth={isActive ? 2.3 : 1.8} />

              {/* Floating badge for Cart tab */}
              {tab.key === 'cart' && itemsCount > 0 && (
                <Animated.View
                  style={[
                    styles.badge,
                    { transform: [{ scale: badgeScale }] },
                  ]}
                >
                  <Text style={styles.badgeText}>
                    {itemsCount > 99 ? '99+' : itemsCount}
                  </Text>
                </Animated.View>
              )}
            </View>

            <Text
              style={[
                styles.label,
                { color: tintColor },
                isActive && styles.labelActive,
              ]}
            >
              {tab.label}
            </Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    bottom: 0,
    left: 0,
    right: 0,
    zIndex: 1000,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-around',
    backgroundColor: '#0D0F15',
    borderTopWidth: 1,
    borderTopColor: 'rgba(255, 255, 255, 0.08)',
    minHeight: 65,
    paddingTop: 10,
    ...Platform.select({
      web: {
        boxShadow: '0 -4px 24px rgba(0, 0, 0, 0.45)',
      } as any,
      default: {
        shadowColor: '#000',
        shadowOffset: { width: 0, height: -4 },
        shadowOpacity: 0.35,
        shadowRadius: 12,
        elevation: 12,
      },
    }),
  },
  tabButton: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 4,
    position: 'relative',
    ...Platform.select({
      web: {
        cursor: 'pointer',
        userSelect: 'none',
      } as any,
    }),
  },
  activeIndicator: {
    position: 'absolute',
    top: -10,
    width: 32,
    height: 2,
    borderRadius: 1,
    backgroundColor: COLORS.goldPrimary,
    ...Platform.select({
      web: {
        boxShadow: '0 0 10px rgba(212, 163, 115, 0.85)',
      } as any,
      default: {
        shadowColor: COLORS.goldPrimary,
        shadowOffset: { width: 0, height: 0 },
        shadowOpacity: 0.9,
        shadowRadius: 5,
        elevation: 3,
      },
    }),
  },
  iconContainer: {
    width: 44,
    height: 28,
    borderRadius: 14,
    alignItems: 'center',
    justifyContent: 'center',
    position: 'relative',
  },
  iconContainerActive: {
    backgroundColor: 'rgba(212, 163, 115, 0.12)',
  },
  badge: {
    position: 'absolute',
    top: -5,
    right: -6,
    minWidth: 18,
    height: 18,
    borderRadius: 9,
    paddingHorizontal: 4,
    backgroundColor: COLORS.champagneAccent,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1.5,
    borderColor: COLORS.cardSurfaceV2,
    ...Platform.select({
      web: {
        boxShadow: '0 2px 6px rgba(0, 0, 0, 0.4)',
      } as any,
    }),
  },
  badgeText: {
    color: '#0A0B0E',
    fontSize: 11,
    fontWeight: '700',
    fontFamily: FONTS.mono,
    textAlign: 'center',
    includeFontPadding: false,
  },
  label: {
    fontSize: 11,
    fontFamily: FONTS.sans,
    fontWeight: '500',
    marginTop: 4,
    letterSpacing: 0.2,
  },
  labelActive: {
    fontWeight: '700',
  },
});
