import React from 'react';
import {
  View,
  Text,
  Pressable,
  StyleSheet,
  StyleProp,
  ViewStyle,
} from 'react-native';
import { useRouter } from 'expo-router';
import { Crown, Compass, Plus, Check } from 'lucide-react-native';
import { MenuItem, COURSE_CATEGORY_SHORT_RU } from '../../types/menu';
import {
  COLORS,
  FONTS,
  RADIUS,
  SPACING,
  TYPOGRAPHY,
  SHADOWS,
} from '../../constants/theme';
import { GlassCard } from '../common/GlassCard';
import { DietaryBadge } from '../common/DietaryBadge';
import { useTasting } from '../../context/TastingContext';

export interface DishCardProps {
  dish: MenuItem;
  onPress?: () => void;
  style?: StyleProp<ViewStyle>;
  testID?: string;
}

export const DishCard: React.FC<DishCardProps> = ({
  dish,
  onPress,
  style,
  testID,
}) => {
  const router = useRouter();
  const { isInTastingSet, addToTastingSet, removeFromTastingSet } = useTasting();
  const inSet = isInTastingSet(dish.id);

  const handleCardPress = () => {
    if (onPress) {
      onPress();
    } else {
      router.push(`/dish/${dish.id}`);
    }
  };

  const handleToggleTastingSet = (e?: any) => {
    // Prevent event bubbling if needed
    if (e && typeof e.stopPropagation === 'function') {
      e.stopPropagation();
    }
    if (inSet) {
      removeFromTastingSet(dish.id);
    } else {
      addToTastingSet(dish);
    }
  };

  return (
    <GlassCard
      testID={testID}
      cornerRadius={RADIUS.lg}
      strokeColor={inSet ? COLORS.champagneAccent : COLORS.glassBorder}
      backgroundColor={inSet ? 'rgba(26, 30, 38, 0.95)' : COLORS.cardSurface}
      style={[
        styles.card,
        inSet && styles.cardSelected,
        style,
      ]}
      padding={SPACING.lg}
      onPress={handleCardPress}
    >
      {/* 1. Header: Course label + Chef Selection mark */}
      <View style={styles.headerRow}>
        <View style={styles.courseTagBox}>
          <Text style={styles.courseText}>
            {(COURSE_CATEGORY_SHORT_RU[dish.course] || dish.course).toUpperCase()}
          </Text>
        </View>

        {dish.isChefSelection && (
          <View style={styles.chefBadge}>
            <Crown size={11} color={COLORS.champagneAccent} strokeWidth={2.5} />
            <Text style={styles.chefBadgeText}>ВЫБОР ШЕФА</Text>
          </View>
        )}
      </View>

      {/* 2. Dish Title & Origin */}
      <View style={styles.titleSection}>
        <Text style={styles.dishTitle}>{dish.name}</Text>
        {dish.origin ? (
          <View style={styles.originRow}>
            <Compass size={11} color={COLORS.champagneAccent} strokeWidth={2} />
            <Text style={styles.originText}>{dish.origin}</Text>
          </View>
        ) : null}
      </View>

      {/* 3. Short Gastronomic Description */}
      <Text style={styles.description} numberOfLines={3}>
        {dish.description}
      </Text>

      {/* 4. Dietary Badges */}
      {dish.dietaryTags && dish.dietaryTags.length > 0 && (
        <View style={styles.tagsContainer}>
          {dish.dietaryTags.map((tag) => (
            <DietaryBadge key={tag} tag={tag} size="sm" />
          ))}
        </View>
      )}

      {/* 5. Footer: Monospace Price & Interactive Toggle Button */}
      <View style={styles.footerRow}>
        <View style={styles.priceContainer}>
          <Text style={styles.currencyPrefix}>€</Text>
          <Text style={styles.priceValue}>{dish.price.toFixed(2)}</Text>
        </View>

        <Pressable
          onPress={handleToggleTastingSet}
          accessibilityRole="button"
          accessibilityLabel={
            inSet ? `В сете (${dish.name})` : `Добавить в сет (${dish.name})`
          }
          style={({ pressed }) => [
            styles.toggleButton,
            inSet ? styles.toggleButtonActive : styles.toggleButtonInactive,
            inSet && SHADOWS.glowChampagne,
            pressed && styles.toggleButtonPressed,
          ]}
          hitSlop={8}
        >
          {inSet ? (
            <>
              <Check size={14} color={COLORS.obsidianCanvas} strokeWidth={2.8} />
              <Text style={styles.toggleButtonTextActive}>В сете</Text>
            </>
          ) : (
            <>
              <Plus size={14} color={COLORS.champagneAccent} strokeWidth={2.5} />
              <Text style={styles.toggleButtonTextInactive}>Добавить в сет</Text>
            </>
          )}
        </Pressable>
      </View>
    </GlassCard>
  );
};

const styles = StyleSheet.create({
  card: {
    marginVertical: SPACING.xs,
  },
  cardSelected: {
    borderWidth: 1.5,
    borderColor: COLORS.champagneAccent,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: SPACING.xs,
  },
  courseTagBox: {
    paddingVertical: 2,
  },
  courseText: {
    fontFamily: FONTS.mono,
    fontSize: 10,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 1.4,
  },
  chefBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: RADIUS.full,
    backgroundColor: 'rgba(229, 169, 98, 0.15)',
    borderWidth: 1,
    borderColor: 'rgba(229, 169, 98, 0.4)',
  },
  chefBadgeText: {
    fontFamily: FONTS.mono,
    fontSize: 9,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 0.8,
  },
  titleSection: {
    marginTop: 2,
    marginBottom: 6,
  },
  dishTitle: {
    fontFamily: FONTS.serif,
    fontSize: 19,
    lineHeight: 25,
    fontWeight: '700',
    color: COLORS.textPrimary,
  },
  originRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    marginTop: 3,
  },
  originText: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    color: COLORS.textSecondary,
  },
  description: {
    ...TYPOGRAPHY.body,
    fontSize: 13,
    lineHeight: 19,
    color: COLORS.textSecondary,
    marginBottom: SPACING.sm,
  },
  tagsContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 6,
    marginBottom: SPACING.md,
  },
  footerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: SPACING.xs,
    borderTopWidth: 1,
    borderTopColor: 'rgba(255, 255, 255, 0.05)',
  },
  priceContainer: {
    flexDirection: 'row',
    alignItems: 'baseline',
    gap: 2,
  },
  currencyPrefix: {
    fontFamily: FONTS.mono,
    fontSize: 14,
    fontWeight: '600',
    color: COLORS.champagneAccent,
  },
  priceValue: {
    fontFamily: FONTS.mono,
    fontSize: 18,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 0.5,
  },
  toggleButton: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: RADIUS.full,
    gap: 5,
    borderWidth: 1.5,
  },
  toggleButtonActive: {
    backgroundColor: COLORS.champagneAccent,
    borderColor: COLORS.champagneAccent,
  },
  toggleButtonInactive: {
    backgroundColor: 'rgba(22, 25, 31, 0.8)',
    borderColor: 'rgba(229, 169, 98, 0.4)',
  },
  toggleButtonTextActive: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    fontWeight: '700',
    color: COLORS.obsidianCanvas,
  },
  toggleButtonTextInactive: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    fontWeight: '600',
    color: COLORS.champagneAccent,
  },
  toggleButtonPressed: {
    transform: [{ scale: 0.94 }],
    opacity: 0.9,
  },
});

export default DishCard;
