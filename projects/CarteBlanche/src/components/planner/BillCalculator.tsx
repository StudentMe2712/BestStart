import React, { useState } from 'react';
import {
  View,
  Text,
  Pressable,
  StyleSheet,
  StyleProp,
  ViewStyle,
} from 'react-native';
import { Users, Minus, Plus, Receipt, Sparkles } from 'lucide-react-native';
import {
  COLORS,
  FONTS,
  RADIUS,
  SPACING,
  TYPOGRAPHY,
  SHADOWS,
} from '../../constants/theme';
import { GlassCard } from '../common/GlassCard';
import { useTasting } from '../../context/TastingContext';

export interface BillCalculatorProps {
  subtotal?: number;
  style?: StyleProp<ViewStyle>;
}

interface TipOption {
  percent: number;
  label: string;
  sublabel: string;
}

const TIP_OPTIONS: TipOption[] = [
  { percent: 0, label: '0%', sublabel: 'Без чаевых' },
  { percent: 10, label: '10%', sublabel: 'Сервис' },
  { percent: 15, label: '15%', sublabel: 'Комплимент сомелье' },
  { percent: 20, label: '20%', sublabel: 'Высшая кухня' },
];

export const BillCalculator: React.FC<BillCalculatorProps> = ({
  subtotal: propSubtotal,
  style,
}) => {
  const { tastingSubtotal } = useTasting();
  const subtotal = propSubtotal !== undefined ? propSubtotal : tastingSubtotal;

  const [selectedTipPercent, setSelectedTipPercent] = useState<number>(15);
  const [guestCount, setGuestCount] = useState<number>(2);

  // Calculations
  const tipAmount = (subtotal * selectedTipPercent) / 100;
  const totalAmount = subtotal + tipAmount;
  const perGuestAmount = guestCount > 0 ? totalAmount / guestCount : totalAmount;

  const handleIncrementGuests = () => {
    if (guestCount < 8) {
      setGuestCount((prev) => prev + 1);
    }
  };

  const handleDecrementGuests = () => {
    if (guestCount > 1) {
      setGuestCount((prev) => prev - 1);
    }
  };

  return (
    <GlassCard
      cornerRadius={RADIUS.xl}
      style={[styles.container, style]}
      padding={SPACING.lg}
      backgroundColor="rgba(22, 25, 31, 0.9)"
      strokeColor="rgba(229, 169, 98, 0.25)"
    >
      {/* 1. Header: Luxe Guest Folio */}
      <View style={styles.header}>
        <View style={styles.headerTitleRow}>
          <Receipt size={16} color={COLORS.champagneAccent} />
          <Text style={styles.headerTitle}>ГОСТЕВОЕ ФОЛИО И РАСЧЕТ</Text>
        </View>
        <Text style={styles.headerSubtitle}>Расчет сета, сервиса и сплита</Text>
      </View>

      {/* 2. Tip / Service Percentage Selector */}
      <View style={styles.sectionBlock}>
        <Text style={styles.sectionLabel}>СЕРВИС И БЛАГОДАРНОСТЬ</Text>
        <View style={styles.tipGrid}>
          {TIP_OPTIONS.map((option) => {
            const isSelected = selectedTipPercent === option.percent;
            return (
              <Pressable
                key={option.percent}
                onPress={() => setSelectedTipPercent(option.percent)}
                style={({ pressed }) => [
                  styles.tipCard,
                  isSelected ? styles.tipCardSelected : styles.tipCardDefault,
                  pressed && styles.tipCardPressed,
                ]}
                accessibilityRole="button"
                accessibilityLabel={`${option.label} • ${option.sublabel}`}
              >
                <Text
                  style={[
                    styles.tipPercentText,
                    isSelected && styles.tipPercentTextSelected,
                  ]}
                >
                  {option.label}
                </Text>
                <Text
                  style={[
                    styles.tipSublabelText,
                    isSelected && styles.tipSublabelTextSelected,
                  ]}
                  numberOfLines={1}
                >
                  {option.sublabel}
                </Text>
              </Pressable>
            );
          })}
        </View>
      </View>

      {/* 3. Guest Bill Split Counter */}
      <View style={styles.sectionBlock}>
        <View style={styles.splitHeaderRow}>
          <View style={styles.splitTitleCluster}>
            <Users size={14} color={COLORS.champagneAccent} />
            <Text style={styles.sectionLabel}>КОЛИЧЕСТВО ГОСТЕЙ</Text>
          </View>
          <Text style={styles.guestCountSummary}>
            {guestCount} {guestCount === 1 ? 'гость' : guestCount < 5 ? 'гостя' : 'гостей'}
          </Text>
        </View>

        <View style={styles.guestCounterRow}>
          <Pressable
            onPress={handleDecrementGuests}
            disabled={guestCount <= 1}
            style={({ pressed }) => [
              styles.counterButton,
              guestCount <= 1 && styles.counterButtonDisabled,
              pressed && styles.counterButtonPressed,
            ]}
            hitSlop={8}
            accessibilityRole="button"
            accessibilityLabel="Уменьшить количество гостей"
          >
            <Minus
              size={16}
              color={guestCount <= 1 ? 'rgba(255, 255, 255, 0.2)' : COLORS.textPrimary}
              strokeWidth={2.5}
            />
          </Pressable>

          <View style={styles.guestDisplay}>
            <Text style={styles.guestNumberText}>{guestCount}</Text>
            <Text style={styles.guestLabelText}>
              {guestCount === 1 ? 'ПЕРСОНА' : guestCount < 5 ? 'ПЕРСОНЫ' : 'ПЕРСОН'}
            </Text>
          </View>

          <Pressable
            onPress={handleIncrementGuests}
            disabled={guestCount >= 8}
            style={({ pressed }) => [
              styles.counterButton,
              guestCount >= 8 && styles.counterButtonDisabled,
              pressed && styles.counterButtonPressed,
            ]}
            hitSlop={8}
            accessibilityRole="button"
            accessibilityLabel="Увеличить количество гостей"
          >
            <Plus
              size={16}
              color={guestCount >= 8 ? 'rgba(255, 255, 255, 0.2)' : COLORS.textPrimary}
              strokeWidth={2.5}
            />
          </Pressable>
        </View>
      </View>

      {/* 4. Financial Breakdown */}
      <View style={styles.breakdownCard}>
        <View style={styles.breakdownRow}>
          <Text style={styles.breakdownLabel}>Сумма блюд</Text>
          <Text style={styles.breakdownValue}>€{subtotal.toFixed(2)}</Text>
        </View>

        <View style={styles.breakdownRow}>
          <Text style={styles.breakdownLabel}>
            Сервисный сбор ({selectedTipPercent}%)
          </Text>
          <Text style={styles.breakdownValue}>€{tipAmount.toFixed(2)}</Text>
        </View>

        <View style={styles.divider} />

        <View style={styles.totalRow}>
          <Text style={styles.totalLabel}>ИТОГО К ОПЛАТЕ</Text>
          <Text style={styles.totalValue}>€{totalAmount.toFixed(2)}</Text>
        </View>
      </View>

      {/* 5. Highlighted Champagne Per-Guest Box */}
      <View style={styles.perGuestBox}>
        <View style={styles.perGuestHeader}>
          <Sparkles size={13} color={COLORS.champagneAccent} />
          <Text style={styles.perGuestMicroLabel}>НА КАЖДОГО ГОСТЯ</Text>
        </View>
        <View style={styles.perGuestPriceRow}>
          <Text style={styles.perGuestCurrency}>€</Text>
          <Text style={styles.perGuestAmount}>{perGuestAmount.toFixed(2)}</Text>
        </View>
        <Text style={styles.perGuestNote}>
          На каждого гостя: €{perGuestAmount.toFixed(2)} • расчет на {guestCount} {guestCount === 1 ? 'персону' : guestCount < 5 ? 'персоны' : 'персон'} с учетом {selectedTipPercent}% сервиса
        </Text>
      </View>
    </GlassCard>
  );
};

const styles = StyleSheet.create({
  container: {
    marginVertical: SPACING.md,
  },
  header: {
    marginBottom: SPACING.lg,
  },
  headerTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  headerTitle: {
    fontFamily: FONTS.mono,
    fontSize: 12,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 1.5,
  },
  headerSubtitle: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    color: COLORS.textSecondary,
    marginTop: 2,
  },
  sectionBlock: {
    marginBottom: SPACING.lg,
  },
  sectionLabel: {
    fontFamily: FONTS.mono,
    fontSize: 9.5,
    fontWeight: '700',
    color: COLORS.textSecondary,
    letterSpacing: 1.2,
    marginBottom: 8,
  },
  tipGrid: {
    flexDirection: 'row',
    gap: 8,
  },
  tipCard: {
    flex: 1,
    paddingVertical: 10,
    paddingHorizontal: 6,
    borderRadius: RADIUS.md,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
  },
  tipCardDefault: {
    backgroundColor: 'rgba(255, 255, 255, 0.03)',
    borderColor: COLORS.glassBorder,
  },
  tipCardSelected: {
    backgroundColor: 'rgba(229, 169, 98, 0.16)',
    borderColor: COLORS.champagneAccent,
  },
  tipCardPressed: {
    opacity: 0.8,
    transform: [{ scale: 0.98 }],
  },
  tipPercentText: {
    fontFamily: FONTS.mono,
    fontSize: 14,
    fontWeight: '700',
    color: COLORS.textSecondary,
  },
  tipPercentTextSelected: {
    color: COLORS.champagneAccent,
  },
  tipSublabelText: {
    fontFamily: FONTS.sans,
    fontSize: 9,
    color: COLORS.textSecondary,
    marginTop: 2,
  },
  tipSublabelTextSelected: {
    color: COLORS.textPrimary,
    fontWeight: '600',
  },
  splitHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  splitTitleCluster: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  guestCountSummary: {
    fontFamily: FONTS.sans,
    fontSize: 12,
    color: COLORS.champagneAccent,
    fontWeight: '600',
  },
  guestCounterRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: 'rgba(0, 0, 0, 0.25)',
    borderRadius: RADIUS.lg,
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderWidth: 1,
    borderColor: COLORS.glassBorder,
  },
  counterButton: {
    width: 38,
    height: 38,
    borderRadius: RADIUS.full,
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  counterButtonDisabled: {
    opacity: 0.35,
  },
  counterButtonPressed: {
    transform: [{ scale: 0.94 }],
  },
  guestDisplay: {
    alignItems: 'center',
  },
  guestNumberText: {
    fontFamily: FONTS.mono,
    fontSize: 22,
    fontWeight: '700',
    color: COLORS.textPrimary,
  },
  guestLabelText: {
    fontFamily: FONTS.mono,
    fontSize: 8.5,
    letterSpacing: 1.2,
    color: COLORS.textSecondary,
    marginTop: 1,
  },
  breakdownCard: {
    backgroundColor: 'rgba(0, 0, 0, 0.28)',
    borderRadius: RADIUS.md,
    padding: SPACING.md,
    marginBottom: SPACING.md,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.05)',
  },
  breakdownRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 4,
  },
  breakdownLabel: {
    fontFamily: FONTS.sans,
    fontSize: 13,
    color: COLORS.textSecondary,
  },
  breakdownValue: {
    fontFamily: FONTS.mono,
    fontSize: 14,
    fontWeight: '600',
    color: COLORS.textPrimary,
  },
  divider: {
    height: 1,
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    marginVertical: 8,
  },
  totalRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: 2,
  },
  totalLabel: {
    fontFamily: FONTS.mono,
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1,
    color: COLORS.textPrimary,
  },
  totalValue: {
    fontFamily: FONTS.mono,
    fontSize: 18,
    fontWeight: '700',
    color: COLORS.champagneAccent,
  },
  perGuestBox: {
    backgroundColor: 'rgba(229, 169, 98, 0.12)',
    borderRadius: RADIUS.lg,
    borderWidth: 1.5,
    borderColor: COLORS.champagneAccent,
    padding: SPACING.md,
    alignItems: 'center',
    ...SHADOWS.glowChampagne,
  },
  perGuestHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    marginBottom: 4,
  },
  perGuestMicroLabel: {
    fontFamily: FONTS.mono,
    fontSize: 10,
    fontWeight: '700',
    color: COLORS.champagneAccent,
    letterSpacing: 1.5,
  },
  perGuestPriceRow: {
    flexDirection: 'row',
    alignItems: 'baseline',
    gap: 3,
  },
  perGuestCurrency: {
    fontFamily: FONTS.mono,
    fontSize: 18,
    fontWeight: '700',
    color: COLORS.champagneAccent,
  },
  perGuestAmount: {
    fontFamily: FONTS.mono,
    fontSize: 28,
    fontWeight: '800',
    color: COLORS.champagneAccent,
    letterSpacing: 0.5,
  },
  perGuestNote: {
    fontFamily: FONTS.sans,
    fontSize: 10.5,
    color: COLORS.textSecondary,
    marginTop: 4,
    textAlign: 'center',
  },
});

export default BillCalculator;
