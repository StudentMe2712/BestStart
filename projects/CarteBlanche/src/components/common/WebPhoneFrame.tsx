import React from 'react';
import {
  Platform,
  StyleSheet,
  Text,
  View,
  useWindowDimensions,
} from 'react-native';
import { COLORS, FONTS } from '../../constants/theme';

export interface WebPhoneFrameProps {
  children: React.ReactNode;
}

/**
 * WebPhoneFrame
 *
 * Provides a luxury studio preview container and an authentic iPhone 15 Pro titanium chassis
 * when rendered in desktop web environments (Platform.OS === 'web' and viewport >= 450px).
 *
 * For native platforms (iOS, Android) and mobile web viewports (< 450px),
 * it seamlessly passes children through without visual or dimensional overhead.
 */
export const WebPhoneFrame: React.FC<WebPhoneFrameProps> = ({ children }) => {
  // Pass through on native mobile platforms
  if (Platform.OS !== 'web') {
    return <>{children}</>;
  }

  const { width, height } = useWindowDimensions();

  // If viewed on mobile web browser or mobile-width viewport, render full screen without chassis
  if (width < 450 || height < 600) {
    return <View style={styles.mobileContainer}>{children}</View>;
  }

  return (
    <View style={styles.studioContainer}>
      <View style={styles.centerStage}>
        {/* Editorial Studio Label */}
        <View style={styles.labelContainer} aria-hidden={true}>
          <View style={styles.labelBadge}>
            <View style={styles.goldDot} />
            <Text style={styles.labelText}>
              CARTE BLANCHE • ПРЕДПРОСМОТР IPHONE 15 PRO
            </Text>
          </View>
        </View>

        {/* iPhone 15 Pro Chassis */}
        <View style={styles.phoneBody}>
          {/* App Screen Viewport */}
          <View style={styles.screenWrapper}>{children}</View>

          {/* Dynamic Island Simulation */}
          <View
            pointerEvents="none"
            style={styles.dynamicIslandContainer}
            aria-hidden={true}
          >
            <View style={styles.dynamicIsland}>
              <View style={styles.sensorDot} />
              <View style={styles.cameraLens}>
                <View style={styles.lensReflection} />
              </View>
            </View>
          </View>

          {/* Home Indicator Simulation */}
          <View
            pointerEvents="none"
            style={styles.homeIndicatorContainer}
            aria-hidden={true}
          >
            <View style={styles.homeIndicator} />
          </View>
        </View>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  mobileContainer: {
    flex: 1,
    width: '100%',
    height: '100%',
    backgroundColor: COLORS.obsidianCanvas,
  },
  studioContainer: {
    height: '100vh' as any,
    maxHeight: '100vh' as any,
    width: '100%',
    backgroundColor: '#07080A',
    backgroundImage:
      'radial-gradient(ellipse 65% 50% at 50% 35%, rgba(229, 169, 98, 0.08) 0%, rgba(7, 8, 10, 0.7) 65%, #07080A 100%)' as any,
    alignItems: 'center',
    justifyContent: 'center',
    padding: 12,
    paddingTop: 12,
    paddingBottom: 12,
    boxSizing: 'border-box' as any,
    overflow: 'hidden' as any,
  },
  centerStage: {
    height: '100%' as any,
    maxHeight: '98vh' as any,
    alignItems: 'center',
    justifyContent: 'center',
    marginVertical: 'auto' as any,
  },
  labelContainer: {
    marginBottom: 8,
    alignItems: 'center',
    userSelect: 'none' as any,
  },
  labelBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 7,
    borderRadius: 999,
    backgroundColor: 'rgba(22, 25, 31, 0.85)',
    borderWidth: 1,
    borderColor: 'rgba(229, 169, 98, 0.22)',
    boxShadow:
      '0 4px 20px rgba(0, 0, 0, 0.5), 0 0 16px rgba(229, 169, 98, 0.06)' as any,
    gap: 8,
  },
  goldDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
    backgroundColor: COLORS.champagneAccent,
    boxShadow: '0 0 8px rgba(229, 169, 98, 0.8)' as any,
  },
  labelText: {
    fontFamily: FONTS.serif,
    fontSize: 11,
    fontWeight: '600',
    letterSpacing: 1.8,
    color: COLORS.champagneAccent,
    textTransform: 'uppercase',
  },
  phoneBody: {
    width: '100%' as any,
    maxWidth: 400,
    height: 'min(852px, 92vh)' as any,
    maxHeight: '92vh' as any,
    display: 'flex' as any,
    flexDirection: 'column' as any,
    borderRadius: 48,
    borderWidth: 4,
    borderColor: '#242831',
    backgroundColor: COLORS.obsidianCanvas,
    overflow: 'hidden',
    position: 'relative',
    boxShadow:
      '0 25px 60px rgba(0, 0, 0, 0.8), 0 0 0 1px rgba(255,255,255,0.1), 0 0 40px rgba(229, 169, 98, 0.08)' as any,
  },
  screenWrapper: {
    flex: 1,
    width: '100%',
    height: '100%',
    display: 'flex' as any,
    flexDirection: 'column' as any,
    position: 'relative',
    overflow: 'hidden',
  },
  dynamicIslandContainer: {
    position: 'absolute',
    top: 11,
    left: 0,
    right: 0,
    alignItems: 'center',
    zIndex: 9999,
    pointerEvents: 'none' as any,
  },
  dynamicIsland: {
    width: 122,
    height: 35,
    borderRadius: 18,
    backgroundColor: '#000000',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'flex-end',
    paddingRight: 12,
    gap: 8,
    borderWidth: 0.5,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    boxShadow: '0 2px 10px rgba(0, 0, 0, 0.9)' as any,
  },
  sensorDot: {
    width: 7,
    height: 7,
    borderRadius: 3.5,
    backgroundColor: '#0A0C10',
  },
  cameraLens: {
    width: 11,
    height: 11,
    borderRadius: 5.5,
    backgroundColor: '#07090E',
    borderWidth: 1,
    borderColor: '#181C26',
    alignItems: 'center',
    justifyContent: 'center',
  },
  lensReflection: {
    width: 4,
    height: 4,
    borderRadius: 2,
    backgroundColor: 'rgba(54, 88, 140, 0.45)',
  },
  homeIndicatorContainer: {
    position: 'absolute',
    bottom: 8,
    left: 0,
    right: 0,
    alignItems: 'center',
    zIndex: 9999,
    pointerEvents: 'none' as any,
  },
  homeIndicator: {
    width: 136,
    height: 4,
    borderRadius: 2,
    backgroundColor: 'rgba(255, 255, 255, 0.35)',
  },
});
