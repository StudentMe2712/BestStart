import React from 'react';
import {
  Text,
  Pressable,
  StyleSheet,
  View,
  StyleProp,
  ViewStyle,
} from 'react-native';
import {
  Leaf,
  Sprout,
  Moon,
  ShieldCheck,
  Fish,
  Check,
  Ban,
  Droplets,
} from 'lucide-react-native';
import { DietaryTag } from '../../types/menu';
import { COLORS, FONTS, RADIUS, SPACING } from '../../constants/theme';

export interface DietaryBadgeProps {
  tag: DietaryTag;
  isInteractive?: boolean;
  isSelected?: boolean;
  onPress?: () => void;
  locale?: 'en' | 'ru';
  size?: 'sm' | 'md';
  style?: StyleProp<ViewStyle>;
}

export const DIETARY_TAG_COLORS: Record<DietaryTag, string> = {
  'Vegan': COLORS.sageGreen,
  'Vegetarian': COLORS.sageGreen,
  'Gluten-Free': COLORS.champagneAccent,
  'Nut-Free': COLORS.champagneAccent,
  'Halal': COLORS.burgundy,
  'Dairy-Free': COLORS.slateBlue,
  'Pescatarian': COLORS.oceanTeal,
};

export const DIETARY_TAG_LABELS_RU: Record<DietaryTag, string> = {
  'Gluten-Free': 'Без глютена',
  'Vegan': 'Веган',
  'Vegetarian': 'Вегетарианское',
  'Halal': 'Халяль',
  'Nut-Free': 'Без орехов',
  'Dairy-Free': 'Без лактозы',
  'Pescatarian': 'Пескетарианское',
};

const TagIcon: React.FC<{ tag: DietaryTag; color: string; size: number }> = ({
  tag,
  color,
  size,
}) => {
  const strokeWidth = 2.2;
  switch (tag) {
    case 'Vegan':
      return <Leaf size={size} color={color} strokeWidth={strokeWidth} />;
    case 'Vegetarian':
      return <Sprout size={size} color={color} strokeWidth={strokeWidth} />;
    case 'Gluten-Free':
      return <Ban size={size} color={color} strokeWidth={strokeWidth} />;
    case 'Halal':
      return <Moon size={size} color={color} strokeWidth={strokeWidth} />;
    case 'Nut-Free':
      return <ShieldCheck size={size} color={color} strokeWidth={strokeWidth} />;
    case 'Dairy-Free':
      return <Droplets size={size} color={color} strokeWidth={strokeWidth} />;
    case 'Pescatarian':
      return <Fish size={size} color={color} strokeWidth={strokeWidth} />;
    default:
      return <Leaf size={size} color={color} strokeWidth={strokeWidth} />;
  }
};

/**
 * Allergen / dietary badge with icon and label.
 * Supports static display mode (e.g. inside dish cards) and interactive filter chip mode.
 */
export const DietaryBadge: React.FC<DietaryBadgeProps> = ({
  tag,
  isInteractive = false,
  isSelected = false,
  onPress,
  locale = 'en',
  size = 'md',
  style,
}) => {
  const accentColor = DIETARY_TAG_COLORS[tag] || COLORS.champagneAccent;
  const label = locale === 'ru' ? DIETARY_TAG_LABELS_RU[tag] : tag;

  const isSmall = size === 'sm' && !isInteractive;
  const iconSize = isSmall ? 10 : 12;

  // Dynamic styling based on mode and selection state
  let backgroundColor: string;
  let borderColor: string;
  let textColor: string;

  if (isInteractive) {
    if (isSelected) {
      backgroundColor = `${accentColor}33`; // ~20% opacity
      borderColor = accentColor;
      textColor = accentColor;
    } else {
      backgroundColor = 'rgba(22, 25, 31, 0.9)';
      borderColor = COLORS.glassBorder;
      textColor = COLORS.textSecondary;
    }
  } else {
    backgroundColor = `${accentColor}1F`; // ~12% opacity
    borderColor = `${accentColor}55`; // ~33% opacity
    textColor = accentColor;
  }

  const badgeContent = (
    <View
      style={[
        styles.badge,
        isSmall ? styles.badgeSmall : styles.badgeMedium,
        {
          backgroundColor,
          borderColor,
        },
        style,
      ]}
    >
      <TagIcon tag={tag} color={textColor} size={iconSize} />
      <Text
        style={[
          styles.label,
          isSmall ? styles.labelSmall : styles.labelMedium,
          { color: textColor },
        ]}
        numberOfLines={1}
      >
        {label}
      </Text>
      {isInteractive && isSelected && (
        <Check size={11} color={accentColor} strokeWidth={2.8} style={styles.checkIcon} />
      )}
    </View>
  );

  if (isInteractive) {
    return (
      <Pressable
        onPress={onPress}
        accessibilityRole="checkbox"
        accessibilityState={{ checked: isSelected }}
        accessibilityLabel={`${label} filter`}
        style={({ pressed }) => [
          styles.pressable,
          pressed && styles.pressed,
        ]}
      >
        {badgeContent}
      </Pressable>
    );
  }

  return badgeContent;
};

const styles = StyleSheet.create({
  pressable: {
    borderRadius: RADIUS.full,
  },
  badge: {
    flexDirection: 'row',
    alignItems: 'center',
    borderRadius: RADIUS.full,
    borderWidth: 1,
  },
  badgeSmall: {
    paddingHorizontal: SPACING.sm,
    paddingVertical: 3,
    gap: 4,
  },
  badgeMedium: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    gap: 6,
  },
  label: {
    fontFamily: FONTS.sans,
    fontWeight: '500',
  },
  labelSmall: {
    fontSize: 10,
    lineHeight: 12,
  },
  labelMedium: {
    fontSize: 12,
    lineHeight: 14,
  },
  checkIcon: {
    marginLeft: 1,
  },
  pressed: {
    opacity: 0.8,
    transform: [{ scale: 0.97 }],
  },
});

export default DietaryBadge;
