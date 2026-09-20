import React from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  Platform,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { SlidersHorizontal, Search, X } from 'lucide-react-native';
import { COLORS, FONTS } from '../../constants/theme';
import { useCart } from '../../context/CartContext';

export interface HeaderProps {
  tableNumber?: string; // default "Стол №4"
  restaurantName?: string; // default "Carte Blanche"
  searchQuery: string;
  onSearchChange: (text: string) => void;
  onFilterPress?: () => void;
  activeFilterCount?: number;
}

/**
 * Header component for Carte Blanche V2 Dark Luxe experience.
 * Features:
 * - Dynamic Island SafeArea clearance (paddingTop >= 56 on web, >= 52 on mobile)
 * - Top row: Table Badge pill («Стол №4 • Carte Blanche») + Filter button with badge
 * - Bottom row: Search bar with icon and quick clear button
 */
export const Header: React.FC<HeaderProps> = ({
  tableNumber: propTableNumber,
  restaurantName = 'Carte Blanche',
  searchQuery,
  onSearchChange,
  onFilterPress,
  activeFilterCount = 0,
}) => {
  let insets = { top: 0, bottom: 0, left: 0, right: 0 };
  try {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    insets = useSafeAreaInsets();
  } catch {
    // Fallback if rendered outside SafeAreaProvider
  }

  let cartTableNumber: string | undefined;
  try {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const cart = useCart();
    cartTableNumber = cart.tableNumber;
  } catch {
    // Fallback if rendered outside CartProvider
  }

  const effectiveTableNumber = propTableNumber || cartTableNumber || 'Стол №4';

  // Dynamic Island safe padding:
  // In WebPhoneFrame, Dynamic Island covers top: 11 to top: 46.
  // 56px guarantees at least 10px breathing room below the island.
  const paddingTop = Platform.OS === 'web' ? 56 : Math.max(insets.top + 12, 52);

  const hasActiveFilters = activeFilterCount > 0;

  return (
    <View style={[styles.container, { paddingTop }]}>
      {/* Top Row: Table Badge Pill + Filter Button */}
      <View style={styles.topRow}>
        <View style={styles.tableBadge}>
          <View style={styles.goldDot} />
          <Text style={styles.tableBadgeText}>
            {effectiveTableNumber} • {restaurantName}
          </Text>
        </View>

        {onFilterPress && (
          <TouchableOpacity
            style={[
              styles.filterButton,
              hasActiveFilters && styles.filterButtonActive,
            ]}
            onPress={onFilterPress}
            activeOpacity={0.7}
            accessibilityRole="button"
            accessibilityLabel="Фильтры меню"
          >
            <SlidersHorizontal
              size={18}
              color={hasActiveFilters ? COLORS.goldPrimary : COLORS.textPrimaryV2}
              strokeWidth={2}
            />
            {hasActiveFilters && (
              <View style={styles.filterBadge}>
                <Text style={styles.filterBadgeText}>{activeFilterCount}</Text>
              </View>
            )}
          </TouchableOpacity>
        )}
      </View>

      {/* Bottom Row: Search Input Bar */}
      <View style={styles.searchRow}>
        <View style={styles.searchBar}>
          <Search size={18} color={COLORS.textSecondaryV2} strokeWidth={2} />
          <TextInput
            style={styles.searchInput}
            value={searchQuery}
            onChangeText={onSearchChange}
            placeholder="Поиск блюд, терруара..."
            placeholderTextColor={COLORS.textSecondaryV2}
            returnKeyType="search"
            autoCapitalize="none"
            autoCorrect={false}
            clearButtonMode="never"
          />
          {searchQuery.length > 0 && (
            <TouchableOpacity
              style={styles.clearButton}
              onPress={() => onSearchChange('')}
              hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
              accessibilityRole="button"
              accessibilityLabel="Очистить поиск"
            >
              <X size={16} color={COLORS.textSecondaryV2} strokeWidth={2.2} />
            </TouchableOpacity>
          )}
        </View>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: COLORS.bgDark,
    borderBottomWidth: 1,
    borderBottomColor: COLORS.borderV2,
    paddingHorizontal: 16,
    paddingBottom: 12,
    gap: 12,
    ...Platform.select({
      web: {
        boxShadow: '0 4px 20px rgba(0, 0, 0, 0.4)',
      } as any,
      default: {
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: 0.3,
        shadowRadius: 8,
        elevation: 6,
      },
    }),
  },
  topRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  tableBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: COLORS.cardSurfaceV2,
    borderWidth: 1,
    borderColor: COLORS.borderV2,
    borderRadius: 9999,
    paddingHorizontal: 12,
    paddingVertical: 7,
    gap: 8,
  },
  goldDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
    backgroundColor: COLORS.goldPrimary,
    ...Platform.select({
      web: {
        boxShadow: '0 0 8px rgba(212, 163, 115, 0.9)',
      } as any,
    }),
  },
  tableBadgeText: {
    color: COLORS.textPrimaryV2,
    fontSize: 13,
    fontWeight: '600',
    fontFamily: FONTS.sans,
    letterSpacing: 0.2,
  },
  filterButton: {
    width: 38,
    height: 38,
    borderRadius: 12,
    backgroundColor: COLORS.cardSurfaceV2,
    borderWidth: 1,
    borderColor: COLORS.borderV2,
    alignItems: 'center',
    justifyContent: 'center',
    position: 'relative',
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  filterButtonActive: {
    borderColor: COLORS.goldPrimary,
    backgroundColor: 'rgba(212, 163, 115, 0.12)',
  },
  filterBadge: {
    position: 'absolute',
    top: -4,
    right: -4,
    minWidth: 16,
    height: 16,
    borderRadius: 8,
    paddingHorizontal: 3,
    backgroundColor: COLORS.goldPrimary,
    alignItems: 'center',
    justifyContent: 'center',
  },
  filterBadgeText: {
    color: '#0A0B0E',
    fontSize: 10,
    fontWeight: '700',
    fontFamily: FONTS.mono,
  },
  searchRow: {
    width: '100%',
  },
  searchBar: {
    height: 42,
    backgroundColor: COLORS.cardSurfaceV2,
    borderWidth: 1,
    borderColor: COLORS.borderV2,
    borderRadius: 12,
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 12,
  },
  searchInput: {
    flex: 1,
    color: COLORS.textPrimaryV2,
    fontSize: 14,
    fontFamily: FONTS.sans,
    paddingVertical: 0,
    paddingHorizontal: 8,
    ...Platform.select({
      web: {
        outlineStyle: 'none',
      } as any,
    }),
  },
  clearButton: {
    padding: 4,
    alignItems: 'center',
    justifyContent: 'center',
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
});
