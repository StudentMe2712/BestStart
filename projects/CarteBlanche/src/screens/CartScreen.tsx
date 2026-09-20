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
import {
  ShoppingBag,
  ArrowRight,
  UtensilsCrossed,
  Trash2,
  Users,
  Minus,
  Plus,
} from 'lucide-react-native';
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

  const [guestCount, setGuestCount] = useState<number>(2);
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

            {/* Interactive Bill Split Section */}
            <View style={styles.splitSection}>
              <View style={styles.goldDivider} />

              <View style={styles.splitHeaderRow}>
                <View style={styles.splitLabelRow}>
                  <Users size={16} color="#D4A373" strokeWidth={2.2} />
                  <Text style={styles.splitLabel}>Разделить счет:</Text>
                </View>

                {/* Stepper Controller [- {guestCount} +] */}
                <View style={styles.splitStepper}>
                  <TouchableOpacity
                    onPress={() => setGuestCount((prev) => Math.max(1, prev - 1))}
                    disabled={guestCount <= 1}
                    hitSlop={{ top: 6, bottom: 6, left: 6, right: 6 }}
                    style={[
                      styles.splitStepperBtn,
                      guestCount <= 1 && styles.splitStepperBtnDisabled,
                    ]}
                    accessibilityRole="button"
                    accessibilityLabel="Уменьшить количество гостей"
                  >
                    <Minus
                      size={13}
                      color={guestCount <= 1 ? '#4A5060' : '#D4A373'}
                      strokeWidth={2.6}
                    />
                  </TouchableOpacity>

                  <Text style={styles.splitGuestCount}>{guestCount}</Text>

                  <TouchableOpacity
                    onPress={() => setGuestCount((prev) => Math.min(8, prev + 1))}
                    disabled={guestCount >= 8}
                    hitSlop={{ top: 6, bottom: 6, left: 6, right: 6 }}
                    style={[
                      styles.splitStepperBtn,
                      guestCount >= 8 && styles.splitStepperBtnDisabled,
                    ]}
                    accessibilityRole="button"
                    accessibilityLabel="Увеличить количество гостей"
                  >
                    <Plus
                      size={13}
                      color={guestCount >= 8 ? '#4A5060' : '#D4A373'}
                      strokeWidth={2.6}
                    />
                  </TouchableOpacity>
                </View>
              </View>

              {/* Interactive Guest Count Pills (1..8) with active gold pill */}
              <View style={styles.guestPillsRow}>
                {[1, 2, 3, 4, 5, 6, 7, 8].map((num) => {
                  const isActive = guestCount === num;
                  return (
                    <TouchableOpacity
                      key={num}
                      onPress={() => setGuestCount(num)}
                      style={[
                        styles.guestPill,
                        isActive && styles.guestPillActive,
                      ]}
                      accessibilityRole="button"
                      accessibilityLabel={`${num} персон`}
                    >
                      <Text
                        style={[
                          styles.guestPillText,
                          isActive && styles.guestPillTextActive,
                        ]}
                      >
                        {num}
                      </Text>
                    </TouchableOpacity>
                  );
                })}
              </View>

              {/* Dynamic Luxury Split Banner */}
              <View style={styles.splitBanner}>
                <Text style={styles.splitBannerAmount}>
                  По {formatKZT(Math.round(grandTotal / guestCount))} с персоны
                </Text>
                <Text style={styles.splitBannerSubtext}>
                  (включая сервис и чаевые)
                </Text>
              </View>
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
    paddingBottom: 110,
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
  splitSection: {
    gap: 10,
    marginTop: 4,
  },
  splitHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  splitLabelRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  splitLabel: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    fontWeight: '600',
    color: '#F8F9FA',
  },
  splitStepper: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    backgroundColor: '#0A0B0E',
    borderWidth: 1,
    borderColor: '#1E2330',
    borderRadius: 12,
    paddingHorizontal: 6,
    paddingVertical: 4,
  },
  splitStepperBtn: {
    width: 26,
    height: 26,
    borderRadius: 8,
    backgroundColor: 'rgba(212, 163, 115, 0.15)',
    alignItems: 'center',
    justifyContent: 'center',
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  splitStepperBtnDisabled: {
    backgroundColor: 'rgba(255, 255, 255, 0.04)',
    opacity: 0.4,
  },
  splitGuestCount: {
    fontFamily: FONTS.mono,
    fontSize: 14,
    fontWeight: '700',
    color: '#D4A373',
    minWidth: 18,
    textAlign: 'center',
  },
  guestPillsRow: {
    flexDirection: 'row',
    gap: 6,
    justifyContent: 'space-between',
  },
  guestPill: {
    flex: 1,
    height: 32,
    borderRadius: 8,
    backgroundColor: '#0A0B0E',
    borderWidth: 1,
    borderColor: '#1E2330',
    alignItems: 'center',
    justifyContent: 'center',
    ...Platform.select({
      web: {
        cursor: 'pointer',
      } as any,
    }),
  },
  guestPillActive: {
    backgroundColor: '#D4A373',
    borderColor: '#D4A373',
  },
  guestPillText: {
    fontFamily: FONTS.mono,
    fontSize: 12,
    fontWeight: '600',
    color: '#8E95A5',
  },
  guestPillTextActive: {
    color: '#0A0B0E',
    fontWeight: '700',
  },
  splitBanner: {
    backgroundColor: 'rgba(212, 163, 115, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(212, 163, 115, 0.3)',
    borderRadius: 14,
    padding: 12,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 3,
    marginTop: 2,
  },
  splitBannerAmount: {
    fontFamily: FONTS.serif,
    fontSize: 16,
    fontWeight: '700',
    color: '#D4A373',
    textAlign: 'center',
    letterSpacing: 0.3,
  },
  splitBannerSubtext: {
    fontFamily: FONTS.sans,
    fontSize: 11,
    color: '#8E95A5',
    textAlign: 'center',
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
