import React, { useState } from 'react';
import {
  View,
  Text,
  Image,
  Pressable,
  StyleSheet,
  StyleProp,
  ViewStyle,
} from 'react-native';
import { Sparkles, Plus, Minus, Utensils } from 'lucide-react-native';
import { MenuItem } from '../../types/menu';
import { COLORS, FONTS, RADIUS, SHADOWS, SPACING } from '../../constants/theme';
import { formatKZT } from '../../utils/currency';
import { useCart } from '../../context/CartContext';

export interface DishCardV2Props {
  item: MenuItem;
  onPress: (item: MenuItem) => void;
  style?: StyleProp<ViewStyle>;
}

/**
 * DishCardV2 (Stage 4)
 * High-end Dark Luxe editorial card with Unsplash photography,
 * Chef's choice badge, dietary tags, KZT pricing, and real-time Cart quick-add.
 */
export const DishCardV2: React.FC<DishCardV2Props> = ({
  item,
  onPress,
  style,
}) => {
  const { getItemQuantity, addToCart, updateQuantity } = useCart();
  const [imageError, setImageError] = useState(false);

  const quantity = getItemQuantity(item.id);
  const isChef = Boolean(item.isChefChoice || item.isChefSelection);

  // Dietary list from V2 or fallback to V1
  const dietaryList: string[] =
    item.dietary && item.dietary.length > 0
      ? item.dietary
      : (item.dietaryTags as string[]) || [];

  const handleCardPress = () => {
    onPress(item);
  };

  const handleQuickAdd = (e: any) => {
    if (e?.stopPropagation) {
      e.stopPropagation();
    }
    addToCart(item);
  };

  const handleDecrease = (e: any) => {
    if (e?.stopPropagation) {
      e.stopPropagation();
    }
    updateQuantity(item.id, -1);
  };

  const handleIncrease = (e: any) => {
    if (e?.stopPropagation) {
      e.stopPropagation();
    }
    updateQuantity(item.id, 1);
  };

  const priceValue = item.priceKZT ?? item.price ?? 0;
  const descriptionText = item.shortDescription || item.description || '';

  return (
    <Pressable
      onPress={handleCardPress}
      accessibilityRole="button"
      accessibilityLabel={`Блюдо: ${item.name}`}
      style={({ pressed }) => [
        styles.card,
        SHADOWS.card,
        pressed && styles.cardPressed,
        style,
      ]}
    >
      {/* 1. Top Image Section */}
      <View style={styles.imageContainer}>
        {item.image && !imageError ? (
          <Image
            source={{ uri: item.image }}
            style={styles.image}
            resizeMode="cover"
            onError={() => setImageError(true)}
          />
        ) : (
          <View style={styles.imagePlaceholder}>
            <Utensils size={32} color={COLORS.borderV2} />
          </View>
        )}

        {/* Floating Badge (Top Left over photo): Chef Choice */}
        {isChef && (
          <View style={styles.chefBadge}>
            <Sparkles size={11} color="#0A0B0E" strokeWidth={2.8} />
            <Text style={styles.chefBadgeText}>ВЫБОР ШЕФА</Text>
          </View>
        )}

        {/* Optional Dietary Tags (Top Right over photo) */}
        {dietaryList.length > 0 && (
          <View style={styles.dietaryContainer}>
            {dietaryList.slice(0, 2).map((tag, idx) => (
              <View key={`${tag}-${idx}`} style={styles.dietaryChip}>
                <Text style={styles.dietaryText}>{tag}</Text>
              </View>
            ))}
          </View>
        )}
      </View>

      {/* 2. Middle Content Section */}
      <View style={styles.content}>
        <View style={styles.titleRow}>
          <Text style={styles.title} numberOfLines={1}>
            {item.name}
          </Text>
          {Boolean(item.weight) && (
            <Text style={styles.weight}>{item.weight}</Text>
          )}
        </View>

        {Boolean(descriptionText) && (
          <Text style={styles.description} numberOfLines={2}>
            {descriptionText}
          </Text>
        )}
      </View>

      {/* 3. Bottom Action Row */}
      <View style={styles.actionRow}>
        <Text style={styles.price}>{formatKZT(priceValue)}</Text>

        {quantity > 0 ? (
          /* Active Stepper in Cart */
          <View style={styles.stepperContainer}>
            <Pressable
              onPress={handleDecrease}
              hitSlop={8}
              style={({ pressed }) => [
                styles.stepperBtn,
                pressed && styles.stepperBtnPressed,
              ]}
              accessibilityRole="button"
              accessibilityLabel="Уменьшить количество"
            >
              <Minus size={13} color="#D4A373" strokeWidth={2.6} />
            </Pressable>

            <Text style={styles.stepperQty}>{quantity}</Text>

            <Pressable
              onPress={handleIncrease}
              hitSlop={8}
              style={({ pressed }) => [
                styles.stepperBtn,
                pressed && styles.stepperBtnPressed,
              ]}
              accessibilityRole="button"
              accessibilityLabel="Увеличить количество"
            >
              <Plus size={13} color="#D4A373" strokeWidth={2.6} />
            </Pressable>
          </View>
        ) : (
          /* Quick Add Circular Button */
          <Pressable
            onPress={handleQuickAdd}
            hitSlop={6}
            style={({ pressed }) => [
              styles.addButton,
              pressed && styles.addButtonPressed,
            ]}
            accessibilityRole="button"
            accessibilityLabel={`Добавить ${item.name} в заказ`}
          >
            <Plus size={18} color="#0A0B0E" strokeWidth={2.6} />
          </Pressable>
        )}
      </View>
    </Pressable>
  );
};

const styles = StyleSheet.create({
  card: {
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#1E2330',
    borderRadius: RADIUS.cardV2,
    overflow: 'hidden',
    marginBottom: SPACING.md,
  },
  cardPressed: {
    opacity: 0.95,
    borderColor: 'rgba(212, 163, 115, 0.4)',
  },
  imageContainer: {
    width: '100%',
    height: 160,
    backgroundColor: '#1A1E26',
    position: 'relative',
  },
  image: {
    width: '100%',
    height: '100%',
  },
  imagePlaceholder: {
    width: '100%',
    height: '100%',
    backgroundColor: '#16191F',
    alignItems: 'center',
    justifyContent: 'center',
  },
  chefBadge: {
    position: 'absolute',
    top: 12,
    left: 12,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: 'rgba(212, 163, 115, 0.95)',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 8,
  },
  chefBadgeText: {
    color: '#0A0B0E',
    fontFamily: FONTS.sans,
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 0.5,
  },
  dietaryContainer: {
    position: 'absolute',
    top: 12,
    right: 12,
    flexDirection: 'row',
    gap: 6,
  },
  dietaryChip: {
    backgroundColor: 'rgba(10, 11, 14, 0.78)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
    paddingHorizontal: 7,
    paddingVertical: 3,
    borderRadius: 6,
  },
  dietaryText: {
    color: '#F8F9FA',
    fontFamily: FONTS.sans,
    fontSize: 10,
    fontWeight: '600',
  },
  content: {
    paddingHorizontal: SPACING.lg,
    paddingTop: SPACING.md,
    paddingBottom: SPACING.sm,
  },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'baseline',
    justifyContent: 'space-between',
    gap: SPACING.sm,
    marginBottom: 4,
  },
  title: {
    flex: 1,
    color: '#F8F9FA',
    fontFamily: FONTS.sans,
    fontSize: 17,
    fontWeight: '700',
    lineHeight: 22,
  },
  weight: {
    color: '#8E95A5',
    fontFamily: FONTS.sans,
    fontSize: 12,
    fontWeight: '500',
  },
  description: {
    color: '#8E95A5',
    fontFamily: FONTS.sans,
    fontSize: 13,
    lineHeight: 18,
    fontWeight: '400',
  },
  actionRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: SPACING.lg,
    paddingBottom: SPACING.lg,
    paddingTop: SPACING.xs,
  },
  price: {
    color: '#D4A373',
    fontFamily: FONTS.mono,
    fontSize: 17,
    fontWeight: '700',
    letterSpacing: 0.3,
  },
  addButton: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: '#D4A373',
    alignItems: 'center',
    justifyContent: 'center',
  },
  addButtonPressed: {
    transform: [{ scale: 0.92 }],
    opacity: 0.9,
  },
  stepperContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(212, 163, 115, 0.15)',
    borderWidth: 1,
    borderColor: '#D4A373',
    borderRadius: 18,
    paddingHorizontal: 4,
    height: 36,
  },
  stepperBtn: {
    width: 28,
    height: 28,
    borderRadius: 14,
    alignItems: 'center',
    justifyContent: 'center',
  },
  stepperBtnPressed: {
    backgroundColor: 'rgba(212, 163, 115, 0.25)',
  },
  stepperQty: {
    color: '#F8F9FA',
    fontFamily: FONTS.sans,
    fontSize: 14,
    fontWeight: '700',
    paddingHorizontal: 6,
    minWidth: 20,
    textAlign: 'center',
  },
});

export default DishCardV2;
