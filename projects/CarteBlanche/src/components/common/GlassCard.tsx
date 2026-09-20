import React from 'react';
import {
  View,
  Pressable,
  StyleSheet,
  ViewStyle,
  StyleProp,
  Platform,
} from 'react-native';
import { COLORS, RADIUS, SHADOWS, SPACING } from '../../constants/theme';

export interface GlassCardProps {
  children?: React.ReactNode;
  style?: StyleProp<ViewStyle>;
  contentContainerStyle?: StyleProp<ViewStyle>;
  cornerRadius?: number;
  strokeColor?: string;
  backgroundColor?: string;
  hasInnerGlow?: boolean;
  padding?: number;
  onPress?: () => void;
  disabled?: boolean;
  testID?: string;
}

/**
 * Frosted dark glass container with subtle border, border radius, and click/wrapper support.
 * Designed for Dark Luxe Haute Cuisine aesthetic.
 */
export const GlassCard: React.FC<GlassCardProps> = ({
  children,
  style,
  contentContainerStyle,
  cornerRadius = RADIUS.lg,
  strokeColor = COLORS.glassBorder,
  backgroundColor = COLORS.glassBackground,
  hasInnerGlow = true,
  padding = SPACING.lg,
  onPress,
  disabled = false,
  testID,
}) => {
  const isInteractive = Boolean(onPress);

  const cardContent = (
    <View
      style={[
        styles.innerContainer,
        {
          padding,
          borderRadius: cornerRadius,
        },
        contentContainerStyle,
      ]}
    >
      {hasInnerGlow && (
        <View
          style={[
            styles.specularSheen,
            {
              borderTopLeftRadius: cornerRadius,
              borderTopRightRadius: cornerRadius,
            },
          ]}
          pointerEvents="none"
        />
      )}
      {children}
    </View>
  );

  const containerStyle: StyleProp<ViewStyle> = [
    styles.container,
    SHADOWS.card,
    {
      borderRadius: cornerRadius,
      borderColor: strokeColor,
      backgroundColor,
    },
    style,
  ];

  if (isInteractive) {
    return (
      <Pressable
        testID={testID}
        disabled={disabled}
        onPress={onPress}
        style={({ pressed }) => [
          containerStyle,
          pressed && styles.pressed,
          disabled && styles.disabled,
        ]}
      >
        {cardContent}
      </Pressable>
    );
  }

  return (
    <View testID={testID} style={containerStyle}>
      {cardContent}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    borderWidth: 1,
    overflow: 'hidden',
    position: 'relative',
    ...Platform.select({
      web: {
        backdropFilter: 'blur(20px)',
        WebkitBackdropFilter: 'blur(20px)',
      },
      default: {},
    }),
  },
  innerContainer: {
    width: '100%',
    position: 'relative',
  },
  specularSheen: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    height: 1,
    backgroundColor: 'rgba(255, 255, 255, 0.12)',
  },
  pressed: {
    opacity: 0.88,
    transform: [{ scale: 0.99 }],
  },
  disabled: {
    opacity: 0.5,
  },
});

export default GlassCard;
