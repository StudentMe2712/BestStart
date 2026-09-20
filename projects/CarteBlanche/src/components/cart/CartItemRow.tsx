import React, { useState } from 'react';
import {
  View,
  Text,
  Image,
  TouchableOpacity,
  StyleSheet,
  Platform,
} from 'react-native';
import { Minus, Plus, Trash2, Utensils } from 'lucide-react-native';
import { CartItem } from '../../types/menu';
import { COLORS, FONTS } from '../../constants/theme';
import { formatKZT } from '../../utils/currency';

export interface CartItemRowProps {
  item: CartItem;
  onUpdateQuantity: (delta: number) => void;
  onRemove: () => void;
}

/**
 * CartItemRow (Stage 5)
 * Dark Luxe V2 row item for the cart.
 * Displays dish thumbnail, title, weight, unit price, stepper controller,
 * calculated total price, and quick remove action.
 */
export const CartItemRow: React.FC<CartItemRowProps> = ({
  item,
  onUpdateQuantity,
  onRemove,
}) => {
  const [imageError, setImageError] = useState(false);
  const dish = item.menuItem;
  const unitPrice = dish.priceKZT ?? dish.price ?? 0;
  const itemTotal = unitPrice * item.quantity;

  const handleDecrease = () => {
    if (item.quantity <= 1) {
      onRemove();
    } else {
      onUpdateQuantity(-1);
    }
  };

  const handleIncrease = () => {
    onUpdateQuantity(1);
  };

  return (
    <View style={styles.container}>
      {/* Left: 64x64px Dish Thumbnail */}
      <View style={styles.imageWrapper}>
        {dish.image && !imageError ? (
          <Image
            source={{ uri: dish.image }}
            style={styles.image}
            resizeMode="cover"
            onError={() => setImageError(true)}
          />
        ) : (
          <View style={styles.placeholder}>
            <Utensils size={24} color={COLORS.borderV2} />
          </View>
        )}
      </View>

      {/* Middle: Dish Details */}
      <View style={styles.details}>
        <Text style={styles.name} numberOfLines={2}>
          {dish.name}
        </Text>
        <View style={styles.metaRow}>
          {Boolean(dish.weight) && (
            <Text style={styles.weight}>{dish.weight}</Text>
          )}
          <Text style={styles.unitPrice}>{formatKZT(unitPrice)} / шт</Text>
        </View>
      </View>

      {/* Right: Actions & Total */}
      <View style={styles.rightSection}>
        <View style={styles.topRightRow}>
          <Text style={styles.itemTotal}>{formatKZT(itemTotal)}</Text>
          <TouchableOpacity
            onPress={onRemove}
            hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
            style={styles.removeBtn}
            accessibilityRole="button"
            accessibilityLabel={`Удалить ${dish.name} из корзины`}
          >
            <Trash2 size={16} color="#8E95A5" strokeWidth={2} />
          </TouchableOpacity>
        </View>

        {/* Stepper Controller */}
        <View style={styles.stepper}>
          <TouchableOpacity
            onPress={handleDecrease}
            hitSlop={{ top: 6, bottom: 6, left: 6, right: 6 }}
            style={styles.stepperBtn}
            accessibilityRole="button"
            accessibilityLabel="Уменьшить количество"
          >
            {item.quantity === 1 ? (
              <Trash2 size={13} color="#D4A373" strokeWidth={2.4} />
            ) : (
              <Minus size={13} color="#D4A373" strokeWidth={2.6} />
            )}
          </TouchableOpacity>

          <Text style={styles.quantityText}>{item.quantity}</Text>

          <TouchableOpacity
            onPress={handleIncrease}
            hitSlop={{ top: 6, bottom: 6, left: 6, right: 6 }}
            style={styles.stepperBtn}
            accessibilityRole="button"
            accessibilityLabel="Увеличить количество"
          >
            <Plus size={13} color="#D4A373" strokeWidth={2.6} />
          </TouchableOpacity>
        </View>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#1E2330',
    borderRadius: 16,
    padding: 12,
    marginVertical: 6,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    ...Platform.select({
      web: {
        boxShadow: '0 2px 10px rgba(0, 0, 0, 0.3)',
      } as any,
      default: {
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 2 },
        shadowOpacity: 0.25,
        shadowRadius: 6,
        elevation: 3,
      },
    }),
  },
  imageWrapper: {
    width: 64,
    height: 64,
    borderRadius: 12,
    overflow: 'hidden',
    backgroundColor: '#1A1E26',
  },
  image: {
    width: '100%',
    height: '100%',
  },
  placeholder: {
    width: '100%',
    height: '100%',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#16191F',
  },
  details: {
    flex: 1,
    justifyContent: 'center',
    gap: 4,
  },
  name: {
    fontFamily: FONTS.sans,
    fontSize: 15,
    fontWeight: '700',
    color: '#F8F9FA',
    lineHeight: 20,
  },
  metaRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    flexWrap: 'wrap',
  },
  weight: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    color: '#8E95A5',
    fontWeight: '500',
  },
  unitPrice: {
    fontFamily: FONTS.mono,
    fontSize: 12,
    color: '#8E95A5',
    fontWeight: '500',
  },
  rightSection: {
    alignItems: 'flex-end',
    justifyContent: 'center',
    gap: 8,
  },
  topRightRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  itemTotal: {
    fontFamily: FONTS.mono,
    fontSize: 14,
    fontWeight: '700',
    color: '#D4A373',
  },
  removeBtn: {
    padding: 2,
    alignItems: 'center',
    justifyContent: 'center',
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  stepper: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(212, 163, 115, 0.12)',
    borderWidth: 1,
    borderColor: 'rgba(212, 163, 115, 0.4)',
    borderRadius: 14,
    paddingHorizontal: 4,
    height: 28,
  },
  stepperBtn: {
    width: 24,
    height: 24,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 12,
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  quantityText: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    fontWeight: '700',
    color: '#F8F9FA',
    paddingHorizontal: 6,
    minWidth: 18,
    textAlign: 'center',
  },
});

export default CartItemRow;
