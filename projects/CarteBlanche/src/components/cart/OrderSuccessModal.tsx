import React from 'react';
import {
  View,
  Text,
  Modal,
  TouchableOpacity,
  StyleSheet,
  Platform,
} from 'react-native';
import { CheckCircle2, UtensilsCrossed, Clock } from 'lucide-react-native';
import { COLORS, FONTS, SHADOWS } from '../../constants/theme';
import { formatKZT } from '../../utils/currency';

export interface OrderSuccessModalProps {
  visible: boolean;
  tableNumber: string;
  totalAmountKZT: number;
  itemsCount: number;
  onReturnToMenu: () => void;
}

/**
 * OrderSuccessModal (Stage 5)
 * Haute Cuisine confirmation modal presented when the guest places the order.
 * Features glowing emerald/gold status indicator, order summary pill,
 * and a primary button returning the guest to the menu.
 */
export const OrderSuccessModal: React.FC<OrderSuccessModalProps> = ({
  visible,
  tableNumber,
  totalAmountKZT,
  itemsCount,
  onReturnToMenu,
}) => {
  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={onReturnToMenu}
    >
      <View style={styles.overlay}>
        <View style={[styles.card, SHADOWS.glowChampagne]}>
          {/* Glowing Status Icon */}
          <View style={styles.iconGlowWrapper}>
            <CheckCircle2 size={56} color="#2EC4B6" strokeWidth={2.2} />
          </View>

          {/* Title */}
          <Text style={styles.title}>Заказ отправлен на кухню!</Text>

          {/* Subtitle with Table and Estimated Wait Time */}
          <Text style={styles.subtitle}>
            Шеф-повар уже готовит ваши блюда. {tableNumber} • Ориентировочное время первой подачи ~15 минут
          </Text>

          {/* Order Summary Pill */}
          <View style={styles.summaryPill}>
            <View style={styles.summaryItem}>
              <UtensilsCrossed size={14} color="#D4A373" />
              <Text style={styles.summaryText}>
                {itemsCount} {itemsCount === 1 ? 'позиция' : itemsCount < 5 ? 'позиции' : 'позиций'}
              </Text>
            </View>
            <View style={styles.summaryDivider} />
            <View style={styles.summaryItem}>
              <Clock size={14} color="#D4A373" />
              <Text style={styles.summaryPriceText}>
                {formatKZT(totalAmountKZT)}
              </Text>
            </View>
          </View>

          {/* Primary Action Button */}
          <TouchableOpacity
            style={styles.primaryButton}
            onPress={onReturnToMenu}
            activeOpacity={0.85}
            accessibilityRole="button"
            accessibilityLabel="Вернуться в меню"
          >
            <Text style={styles.primaryButtonText}>Вернуться в меню</Text>
          </TouchableOpacity>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(5, 6, 8, 0.85)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
    ...Platform.select({
      web: {
        backdropFilter: 'blur(12px)',
        WebkitBackdropFilter: 'blur(12px)',
      } as any,
    }),
  },
  card: {
    backgroundColor: '#13161F',
    borderWidth: 1,
    borderColor: '#D4A373',
    borderRadius: 24,
    padding: 24,
    alignItems: 'center',
    width: '100%',
    maxWidth: 420,
    gap: 16,
  },
  iconGlowWrapper: {
    width: 80,
    height: 80,
    borderRadius: 40,
    backgroundColor: 'rgba(46, 196, 182, 0.12)',
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: 'rgba(46, 196, 182, 0.3)',
    ...Platform.select({
      web: {
        boxShadow: '0 0 24px rgba(46, 196, 182, 0.4)',
      } as any,
      default: {
        shadowColor: '#2EC4B6',
        shadowOffset: { width: 0, height: 0 },
        shadowOpacity: 0.6,
        shadowRadius: 16,
        elevation: 8,
      },
    }),
  },
  title: {
    fontFamily: FONTS.serif,
    fontSize: 22,
    fontWeight: '700',
    color: '#F8F9FA',
    textAlign: 'center',
    lineHeight: 28,
  },
  subtitle: {
    fontFamily: FONTS.sans,
    fontSize: 14,
    color: '#8E95A5',
    textAlign: 'center',
    lineHeight: 20,
    paddingHorizontal: 8,
  },
  summaryPill: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 14,
    backgroundColor: 'rgba(212, 163, 115, 0.08)',
    borderWidth: 1,
    borderColor: 'rgba(212, 163, 115, 0.25)',
    borderRadius: 14,
    paddingHorizontal: 16,
    paddingVertical: 10,
    width: '100%',
  },
  summaryItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  summaryText: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    fontWeight: '600',
    color: '#F8F9FA',
  },
  summaryDivider: {
    width: 1,
    height: 16,
    backgroundColor: 'rgba(212, 163, 115, 0.3)',
  },
  summaryPriceText: {
    fontFamily: FONTS.mono,
    fontSize: 14,
    fontWeight: '700',
    color: '#D4A373',
  },
  primaryButton: {
    height: 48,
    width: '100%',
    backgroundColor: '#D4A373',
    borderRadius: 14,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 8,
    ...Platform.select({
      web: {
        cursor: 'pointer',
        boxShadow: '0 4px 16px rgba(212, 163, 115, 0.35)',
      } as any,
    }),
  },
  primaryButtonText: {
    fontFamily: FONTS.sans,
    fontSize: 16,
    fontWeight: '700',
    color: '#0A0B0E',
    letterSpacing: 0.3,
  },
});

export default OrderSuccessModal;
