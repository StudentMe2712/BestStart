import React from 'react';
import {
  ScrollView,
  Pressable,
  Text,
  StyleSheet,
  View,
  StyleProp,
  ViewStyle,
} from 'react-native';
import {
  Sparkles,
  UtensilsCrossed,
  Flame,
  Cake,
  Wine,
  LucideIcon,
} from 'lucide-react-native';
import { COLORS, FONTS, RADIUS, SPACING } from '../../constants/theme';

export type CategoryFilterType = 'all' | 'starters' | 'mains' | 'desserts' | 'drinks';

export interface CategorySelectorProps {
  selectedCategory: CategoryFilterType;
  onSelectCategory: (category: CategoryFilterType) => void;
  style?: StyleProp<ViewStyle>;
}

interface CategoryOption {
  id: CategoryFilterType;
  label: string;
  icon: LucideIcon;
}

export const CATEGORIES_LIST: CategoryOption[] = [
  { id: 'all', label: 'Все блюда', icon: Sparkles },
  { id: 'starters', label: 'Закуски', icon: UtensilsCrossed },
  { id: 'mains', label: 'Основные блюда', icon: Flame },
  { id: 'desserts', label: 'Десерты', icon: Cake },
  { id: 'drinks', label: 'Винный погреб & Бар', icon: Wine },
];

/**
 * CategorySelector (Stage 4)
 * Horizontal pill selector for fine dining menu categories in Dark Luxe V2 aesthetic.
 */
export const CategorySelector: React.FC<CategorySelectorProps> = ({
  selectedCategory,
  onSelectCategory,
  style,
}) => {
  return (
    <View style={[styles.container, style]}>
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.scrollContent}
      >
        {CATEGORIES_LIST.map((cat) => {
          const isActive = selectedCategory === cat.id;
          const IconComponent = cat.icon;

          return (
            <Pressable
              key={cat.id}
              onPress={() => onSelectCategory(cat.id)}
              accessibilityRole="tab"
              accessibilityState={{ selected: isActive }}
              accessibilityLabel={`Категория: ${cat.label}`}
              style={({ pressed }) => [
                styles.pill,
                isActive ? styles.pillActive : styles.pillInactive,
                pressed && styles.pillPressed,
              ]}
            >
              <IconComponent
                size={14}
                color={isActive ? COLORS.goldPrimary : COLORS.textSecondaryV2}
                strokeWidth={isActive ? 2.4 : 1.8}
              />
              <Text
                style={[
                  styles.pillText,
                  isActive ? styles.pillTextActive : styles.pillTextInactive,
                ]}
              >
                {cat.label}
              </Text>
            </Pressable>
          );
        })}
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    marginVertical: SPACING.xs,
  },
  scrollContent: {
    paddingHorizontal: SPACING.lg,
    paddingVertical: SPACING.xs,
    gap: SPACING.sm,
    alignItems: 'center',
  },
  pill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: RADIUS.full,
    borderWidth: 1,
  },
  pillActive: {
    backgroundColor: 'rgba(212, 163, 115, 0.15)',
    borderColor: '#D4A373',
  },
  pillInactive: {
    backgroundColor: '#13161F',
    borderColor: '#1E2330',
  },
  pillPressed: {
    opacity: 0.8,
  },
  pillText: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    lineHeight: 18,
  },
  pillTextActive: {
    color: '#D4A373',
    fontWeight: '600',
  },
  pillTextInactive: {
    color: '#8E95A5',
    fontWeight: '500',
  },
});

export default CategorySelector;
