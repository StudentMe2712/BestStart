import React from 'react';
import { View, StyleSheet, StyleProp, ViewStyle } from 'react-native';
import Svg, {
  Polygon,
  Line,
  Circle,
  Defs,
  RadialGradient,
  Stop,
  Text as SvgText,
  TSpan,
} from 'react-native-svg';
import { FlavorProfile } from '../../types/menu';
import { COLORS } from '../../constants/theme';

export type AxisLanguage = 'RU' | 'EN' | 'dual';

export interface FlavorRadarChartProps {
  profile: FlavorProfile;
  size?: number;
  language?: AxisLanguage;
  showValueLabels?: boolean;
  showGridPercentages?: boolean;
  style?: StyleProp<ViewStyle>;
}

interface AxisMeta {
  key: keyof FlavorProfile;
  ru: string;
  en: string;
}

const SENSORY_AXES: AxisMeta[] = [
  { key: 'umami', ru: 'УМАМИ', en: 'UMAMI' },
  { key: 'acidity', ru: 'КИСЛОТНОСТЬ', en: 'ACIDITY' },
  { key: 'sweetness', ru: 'СЛАДОСТЬ', en: 'SWEETNESS' },
  { key: 'spiciness', ru: 'ПРЯНОСТЬ', en: 'SPICINESS' },
  { key: 'texture', ru: 'ТЕКСТУРА', en: 'TEXTURE' },
];

const GRID_LEVELS = [0.2, 0.4, 0.6, 0.8, 1.0];

/**
 * 5-Axis Haute Cuisine sensory flavor radar chart.
 * Built with react-native-svg: concentric pentagonal grid, radial axes,
 * smooth semi-transparent champagne fill, golden vertex points, and bilingual axis labels.
 */
export const FlavorRadarChart: React.FC<FlavorRadarChartProps> = ({
  profile,
  size = 220,
  language = 'RU',
  showValueLabels = true,
  showGridPercentages = true,
  style,
}) => {
  // Label padding outside the radar radius
  const paddingX = 64;
  const paddingY = 44;
  const svgWidth = size + paddingX * 2;
  const svgHeight = size + paddingY * 2;

  const cx = svgWidth / 2;
  const cy = svgHeight / 2;
  const radius = size / 2;

  // Compute angle for each of the 5 axes (starting at top = -pi / 2)
  const getAngle = (index: number): number => {
    return -Math.PI / 2 + index * ((2 * Math.PI) / 5);
  };

  // Concentric pentagons for the grid
  const gridPolygons = GRID_LEVELS.map((level) => {
    const r = radius * level;
    const points = SENSORY_AXES.map((_, i) => {
      const angle = getAngle(i);
      const x = cx + r * Math.cos(angle);
      const y = cy + r * Math.sin(angle);
      return `${x.toFixed(2)},${y.toFixed(2)}`;
    }).join(' ');
    return { level, points };
  });

  // 5 Radial spokes from center to outer pentagon vertices
  const spokes = SENSORY_AXES.map((_, i) => {
    const angle = getAngle(i);
    const x = cx + radius * Math.cos(angle);
    const y = cy + radius * Math.sin(angle);
    return {
      x2: x.toFixed(2),
      y2: y.toFixed(2),
    };
  });

  // Data values polygon
  const rawValues = SENSORY_AXES.map((axis) => {
    const val = profile[axis.key];
    // Aesthetic minimum floor so radar never completely collapses into center point
    return Math.max(0.06, Math.min(1.0, typeof val === 'number' ? val : 0.5));
  });

  const dataPoints = rawValues.map((val, i) => {
    const angle = getAngle(i);
    const r = radius * val;
    return {
      x: cx + r * Math.cos(angle),
      y: cy + r * Math.sin(angle),
    };
  });

  const dataPolygonString = dataPoints
    .map((p) => `${p.x.toFixed(2)},${p.y.toFixed(2)}`)
    .join(' ');

  // Format axis label
  const formatLabel = (axis: AxisMeta): string => {
    switch (language) {
      case 'RU':
        return axis.ru;
      case 'EN':
        return axis.en;
      case 'dual':
        return `${axis.ru} • ${axis.en}`;
    }
  };

  return (
    <View style={[styles.container, style]}>
      <Svg
        width={svgWidth}
        height={svgHeight}
        viewBox={`0 0 ${svgWidth} ${svgHeight}`}
      >
        <Defs>
          <RadialGradient
            id="champagneRadarGradient"
            cx="50%"
            cy="50%"
            rx="50%"
            ry="50%"
            fx="50%"
            fy="50%"
          >
            <Stop offset="0%" stopColor={COLORS.goldHighlight} stopOpacity="0.55" />
            <Stop offset="60%" stopColor={COLORS.champagneAccent} stopOpacity="0.28" />
            <Stop offset="100%" stopColor={COLORS.champagneAccent} stopOpacity="0.08" />
          </RadialGradient>
        </Defs>

        {/* 1. Concentric Pentagonal Level Rings */}
        {gridPolygons.map(({ level, points }) => (
          <Polygon
            key={`grid-pentagon-${level}`}
            points={points}
            fill="none"
            stroke="rgba(255, 255, 255, 0.08)"
            strokeWidth={level === 1.0 ? 1.4 : 0.9}
          />
        ))}

        {/* 2. Concentric Percentage Tick Marks (top axis) */}
        {showGridPercentages &&
          GRID_LEVELS.map((level) => {
            const r = radius * level;
            const y = cy - r;
            return (
              <SvgText
                key={`tick-${level}`}
                x={cx + 4}
                y={y - 2}
                fill="rgba(142, 149, 165, 0.45)"
                fontSize="8"
                fontFamily="monospace"
                fontWeight="600"
              >
                {Math.round(level * 100)}%
              </SvgText>
            );
          })}

        {/* 3. Radial Spoke Axes */}
        {spokes.map((spoke, i) => (
          <Line
            key={`spoke-${i}`}
            x1={cx}
            y1={cy}
            x2={spoke.x2}
            y2={spoke.y2}
            stroke="rgba(255, 255, 255, 0.09)"
            strokeWidth="1"
          />
        ))}

        {/* 4. Sensory Data Polygon Fill */}
        <Polygon
          points={dataPolygonString}
          fill="url(#champagneRadarGradient)"
          stroke={COLORS.champagneAccent}
          strokeWidth="2"
          strokeLinejoin="round"
          strokeLinecap="round"
        />

        {/* 5. Golden Dots on Polygon Vertices */}
        {dataPoints.map((point, i) => (
          <Circle
            key={`dot-${i}`}
            cx={point.x}
            cy={point.y}
            r="4"
            fill={COLORS.champagneAccent}
            stroke="#FFFFFF"
            strokeWidth="1"
          />
        ))}

        {/* 6. Axis Labels & Percentages */}
        {SENSORY_AXES.map((axis, i) => {
          const angle = getAngle(i);
          const labelDistance = radius + (language === 'dual' ? 24 : 18);
          const lx = cx + labelDistance * Math.cos(angle);
          const ly = cy + labelDistance * Math.sin(angle);

          // Determine SVG text-anchor based on position
          let textAnchor: 'middle' | 'start' | 'end' = 'middle';
          const cosVal = Math.cos(angle);
          if (cosVal > 0.3) {
            textAnchor = 'start';
          } else if (cosVal < -0.3) {
            textAnchor = 'end';
          } else {
            textAnchor = 'middle';
          }

          // Small vertical adjustment
          const sinVal = Math.sin(angle);
          let dyAdjustment = 4;
          if (sinVal < -0.7) {
            dyAdjustment = -6; // Top axis
          } else if (sinVal > 0.7) {
            dyAdjustment = 12; // Bottom axes
          }

          const rawVal = profile[axis.key];
          const pct = Math.round((typeof rawVal === 'number' ? rawVal : 0) * 100);

          return (
            <SvgText
              key={`label-${axis.key}`}
              x={lx}
              y={ly + dyAdjustment}
              textAnchor={textAnchor}
              fill={COLORS.textPrimary}
              fontSize="10"
              fontFamily="Georgia, serif"
              fontWeight="700"
              letterSpacing="0.8"
            >
              {formatLabel(axis)}
              {showValueLabels && (
                <TSpan
                  x={lx}
                  dy="13"
                  fill={COLORS.champagneAccent}
                  fontSize="9.5"
                  fontFamily="monospace"
                  fontWeight="600"
                >
                  {`${pct}%`}
                </TSpan>
              )}
            </SvgText>
          );
        })}
      </Svg>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    justifyContent: 'center',
  },
});

export default FlavorRadarChart;
