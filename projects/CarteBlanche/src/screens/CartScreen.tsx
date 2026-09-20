import React, { useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  StyleSheet,
  Platform,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { ShoppingBag, ArrowRight, UtensilsCrossed, Trash2 } from 'lucide-react-native';
import { useCart } from '../context/CartContext';
import { CartItemRow } from '../components/cart/CartItemRow';
import { OrderSuccessModal } from '../components/cart/OrderSuccessModal';
import { COLORS, FONTS, RADIUS, SHADOWS, SPACING } from '../constants/theme';
import { formatKZT } from '../utils/currency';

export interface CartScreenProps {
  onNavigateToMenu: () => void;
}

const TIP_OPTIONS = [
  { value: 0, label: '0%' },
  { value: 10, label: '10% (Рекомендуется)' },
  { value: 15, label: '15%' },
];

/**
 * CartScreen (Stage 5)
 * Haute Cuisine dining cart and bill summary.
 * Features:
 * - Dynamic Island SafeArea top clearance
 * - Empty state with luxury call-to-action
 * - Item list with CartItemRow controllers
 * - Service charge / gratuity selector (0%, 10%, 15%)
 * - Elegant bill breakdown in KZT
 * - Kitchen submission CTA with OrderSuccessModal
 */
export const CartScreen: React.FC<CartScreenProps> = ({ onNavigateToMenu }) => {
  let insets = { top: 0, bottom: 0, left: 0, right: 0 };
  try {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    insets = useSafeAreaInsets();
  } catch {
    // Fallback if rendered outside SafeAreaProvider
  }

  const {
    items,
    itemsCount,
    totalAmount,
    tipPercentage,
    tipAmount,
    grandTotal,
    tableNumber,
    updateQuantity,
    removeFromCart,
    clearCart,
    setTipPercentage,
  } = useCart();

  const [isSuccessModalVisible, setIsSuccessModalVisible] = useState(false);

  const paddingTop = Platform.OS === 'web' ? 56 : Math.max(insets.top + 12, 52);

  const handleOrderSubmit = () => {
    setIsSuccessModalVisible(true);
  };

  const handleReturnToMenu = () => {
    clearCart();
    setIsSuccessModalVisible(false);
    onNavigateToMenu();
  };

  return (
    <View style={styles.container}>
      {/* 1. Top Header */}
      <View style={[styles.header, { paddingTop }]}>
        <View style={styles.headerRow}>
          <Text style={styles.headerTitle}>Корзина заказа</Text>

          <View style={styles.tableBadge}>
            <View style={styles.goldDot} />
            <Text style={styles.tableBadgeText}>{tableNumber || 'Стол №4'}</Text>
          </View>
        </View>

        {items.length > 0 && (
          <View style={styles.headerSubRow}>
            <Text style={styles.headerSubtext}>
              {itemsCount} {itemsCount === 1 ? 'блюдо' : itemsCount < 5 ? 'блюда' : 'блюд'} в вашем сете
            </Text>
            <TouchableOpacity
              onPress={clearCart}
              hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
              style={styles.clearAllBtn}
              accessibilityRole="button"
              accessibilityLabel="Очистить всю корзину"
            >
              <Trash2 size={13} color="#8E95A5" />
              <Text style={styles.clearAllText}>Очистить</Text>
            </TouchableOpacity>
          </View>
        )}
      </View>

      {/* 2. Main Body: Empty State or Populated Cart */}
      {items.length === 0 ? (
        <View style={styles.emptyContainer}>
          <View style={styles.emptyIconBox}>
            <ShoppingBag size={64} color="#8E95A5" strokeWidth={1.5} />
          </View>

          <Text style={styles.emptyTitle}>В вашей корзине пока пусто</Text>

          <Text style={styles.emptySubtitle}>
            Добавьте изысканные блюда и винные пейринги из авторского меню шефа.
          </Text>

          <TouchableOpacity
            style={styles.goToMenuBtn}
            onPress={onNavigateToMenu}
            activeOpacity={0.85}
            accessibilityRole="button"
            accessibilityLabel="Перейти в меню"
          >
            <UtensilsCrossed size={18} color="#0A0B0E" strokeWidth={2.4} />
            <Text style={styles.goToMenuBtnText}>Перейти в меню</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <ScrollView
          showsVerticalScrollIndicator={false}
          contentContainerStyle={styles.scrollContent}
        >
          {/* Items List */}
          <View style={styles.itemsSection}>
            {items.map((item) => (
              <CartItemRow
                key={item.menuItem.id}
                item={item}
                onUpdateQuantity={(delta) => updateQuantity(item.menuItem.id, delta)}
                onRemove={() => removeFromCart(item.menuItem.id)}
              />
            ))}
          </View>

          {/* Service Charge & Gratuity Selector */}
          <View style={styles.tipsCard}>
            <Text style={styles.tipsTitle}>Сервисный сбор и чаевые</Text>
            <View style={styles.tipOptionsRow}>
              {TIP_OPTIONS.map((opt) => {
                const isSelected = tipPercentage === opt.value;
                return (
                  <TouchableOpacity
                    key={opt.value}
                    onPress={() => setTipPercentage(opt.value)}
                    style={[
                      styles.tipOptionPill,
                      isSelected && styles.tipOptionPillActive,
                    ]}
                    accessibilityRole="radio"
                    accessibilityState={{ selected: isSelected }}
                    accessibilityLabel={`Чаевые ${opt.label}`}
                  >
                    <Text
                      style={[
                        styles.tipOptionText,
                        isSelected && styles.tipOptionTextActive,
                      ]}
                    >
                      {opt.label}
                    </Text>
                  </TouchableOpacity>
                );
              })}
            </View>
          </View>

          {/* Bill Summary Breakdown */}
          <View style={styles.summaryCard}>
            <View style={styles.summaryRow}>
              <Text style={styles.summaryLabel}>Сумма блюд:</Text>
              <Text style={styles.summaryValue}>{formatKZT(totalAmount)}</Text>
            </View>

            <View style={styles.summaryRow}>
              <Text style={styles.summaryLabel}>
                Сервисный сбор ({tipPercentage}%):
              </Text>
              <Text style={styles.summaryValue}>{formatKZT(tipAmount)}</Text>
            </View>

            <View style={styles.goldDivider} />

            <View style={styles.summaryRowGrand}>
              <Text style={styles.grandTotalLabel}>Итого к оплате:</Text>
              <Text style={styles.grandTotalValue}>{formatKZT(grandTotal)}</Text>
            </View>
          </View>

          {/* CTA Button: Submit to Kitchen */}
          <TouchableOpacity
            style={styles.submitButton}
            onPress={handleOrderSubmit}
            activeOpacity={0.88}
            accessibilityRole="button"
            accessibilityLabel={`Отправить заказ на кухню. Сумма: ${formatKZT(grandTotal)}`}
          >
            <Text style={styles.submitButtonText}>
              Отправить заказ на кухню • {formatKZT(grandTotal)}
            </Text>
            <ArrowRight size={18} color="#0A0B0E" strokeWidth={2.6} />
          </TouchableOpacity>
        </ScrollView>
      )}

      {/* 3. Order Success Modal */}
      <OrderSuccessModal
        visible={isSuccessModalVisible}
        tableNumber={tableNumber || 'Стол №4'}
        totalAmountKZT={grandTotal}
        itemsCount={itemsCount}
        onReturnToMenu={handleReturnToMenu}
      />
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0B0E',
  },
  header: {
    backgroundColor: '#0A0B0E',
    borderBottomWidth: 1,
    borderBottomColor: '#1E2330',
    paddingHorizontal: 16,
    paddingBottom: 14,
    gap: 8,
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  headerTitle: {
    fontFamily: FONTS.serif,
    fontSize: 20,
    fontWeight: '700',
    color: '#F8F9FA',
    letterSpacing: 0.3,
  },
  tableBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#1E2330',
    borderRadius: 9999,
    paddingHorizontal: 12,
    paddingVertical: 6,
  },
  goldDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
    backgroundColor: '#D4A373',
  },
  tableBadgeText: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    fontWeight: '600',
    color: '#F8F9FA',
  },
  headerSubRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  headerSubtext: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    color: '#8E95A5',
  },
  clearAllBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  clearAllText: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    color: '#8E95A5',
  },
  emptyContainer: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 32,
    gap: 16,
  },
  emptyIconBox: {
    width: 104,
    height: 104,
    borderRadius: 52,
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#1E2330',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 8,
  },
  emptyTitle: {
    fontFamily: FONTS.serif,
    fontSize: 20,
    fontWeight: '700',
    color: '#F8F9FA',
    textAlign: 'center',
  },
  emptySubtitle: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    color: '#8E95A5',
    textAlign: 'center',
    lineHeight: 22,
    maxWidth: 300,
  },
  goToMenuBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    backgroundColor: '#D4A373',
    paddingHorizontal: 24,
    paddingVertical: 14,
    borderRadius: 14,
    marginTop: 12,
    ...Platform.select({
      web: {
        cursor: 'pointer',
        boxShadow: '0 4px 16px rgba(212, 163, 115, 0.35)',
      } as any,
    }),
  },
  goToMenuBtnText: {
    fontFamily: FONTS.sans,
    fontSize: 15,
    fontWeight: '700',
    color: '#0A0B0E',
  },
  scrollContent: {
    paddingHorizontal: 16,
    paddingTop: 12,
    paddingBottom: 40,
    gap: 16,
  },
  itemsSection: {
    gap: 2,
  },
  tipsCard: {
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#1E2330',
    borderRadius: 16,
    padding: 16,
    gap: 12,
  },
  tipsTitle: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    fontWeight: '600',
    color: '#F8F9FA',
  },
  tipOptionsRow: {
    flexDirection: 'row',
    gap: 8,
    flexWrap: 'wrap',
  },
  tipOptionPill: {
    flex: 1,
    minWidth: 80,
    paddingVertical: 10,
    paddingHorizontal: 10,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: '#1E2330',
    backgroundColor: '#0A0B0E',
    alignItems: 'center',
    justifyContent: 'center',
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  tipOptionPillActive: {
    borderColor: '#D4A373',
    backgroundColor: 'rgba(212, 163, 115, 0.15)',
  },
  tipOptionText: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    color: '#8E95A5',
    fontWeight: '500',
    textAlign: 'center',
  },
  tipOptionTextActive: {
    color: '#D4A373',
    fontWeight: '700',
  },
  summaryCard: {
    backgroundColor: '#16191F',
    borderWidth: 1,
    borderColor: '#1E2330',
    borderRadius: 16,
    padding: 16,
    gap: 10,
  },
  summaryRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  summaryLabel: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    color: '#8E95A5',
  },
  summaryValue: {
    fontFamily: FONTS.mono,
    fontSize: 14,
    color: '#F8F9FA',
    fontWeight: '600',
  },
  goldDivider: {
    height: 1,
    backgroundColor: 'rgba(212, 163, 115, 0.25)',
    marginVertical: 4,
  },
  summaryRowGrand: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingTop: 2,
  },
  grandTotalLabel: {
    fontFamily: FONTS.serif,
    fontSize: 16,
    fontWeight: '700',
    color: '#F8F9FA',
  },
  grandTotalValue: {
    fontFamily: FONTS.mono,
    fontSize: 22,
    fontWeight: '700',
    color: '#D4A373',
  },
  submitButton: {
    height: 52,
    borderRadius: 16,
    backgroundColor: '#D4A373',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 10,
    marginTop: 6,
    ...Platform.select({
      web: {
        cursor: 'pointer',
        boxShadow: '0 4px 20px rgba(212, 163, 115, 0.4)',
      } as any,
      default: {
        shadowColor: '#D4A373',
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: 0.4,
        shadowRadius: 10,
        elevation: 6,
      },
    }),
  },
  submitButtonText: {
    fontFamily: FONTS.sans,
    fontSize: 16,
    fontWeight: '700',
    color: '#0A0B0E',
  },
});

export default CartScreen;
