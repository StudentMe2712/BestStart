//
//  FlavorWheelView.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

// MARK: - Language Localization Option

/// Presentation language for the 5 sensory taste axes
public enum AxisLanguage: String, CaseIterable, Identifiable {
    case russian = "RU"
    case english = "EN"
    case dual = "RU / EN"

    public var id: String { rawValue }

    public func title(for axis: FlavorProfile.Axis) -> String {
        switch self {
        case .russian:
            return axis.titleRu.uppercased()
        case .english:
            return axis.rawValue.uppercased()
        case .dual:
            return "\(axis.titleRu.uppercased()) • \(axis.rawValue.uppercased())"
        }
    }
}

// MARK: - Radar Shapes

/// Concentric pentagonal reference grid and radial spokes
public struct RadarGridShape: Shape {
    public var levels: [Double] = [0.2, 0.4, 0.6, 0.8, 1.0]

    public func path(in rect: CGRect) -> Path {
        var path = Path()
        let center = CGPoint(x: rect.midX, y: rect.midY)
        let radius = min(rect.width, rect.height) / 2

        // Draw 5 concentric pentagonal level rings
        for level in levels {
            let r = radius * CGFloat(level)
            for i in 0..<5 {
                let angle = -Double.pi / 2 + Double(i) * (2 * Double.pi / 5)
                let point = CGPoint(
                    x: center.x + r * CGFloat(cos(angle)),
                    y: center.y + r * CGFloat(sin(angle))
                )
                if i == 0 {
                    path.move(to: point)
                } else {
                    path.addLine(to: point)
                }
            }
            path.closeSubpath()
        }

        // Draw 5 radial spokes connecting center to maximum vertices
        for i in 0..<5 {
            let angle = -Double.pi / 2 + Double(i) * (2 * Double.pi / 5)
            let outerPoint = CGPoint(
                x: center.x + radius * CGFloat(cos(angle)),
                y: center.y + radius * CGFloat(sin(angle))
            )
            path.move(to: center)
            path.addLine(to: outerPoint)
        }

        return path
    }
}

/// Dynamic 5-axis polygon with animatable vertex coordinates
public struct RadarPolygonShape: Shape {
    public var umami: Double
    public var acidity: Double
    public var sweetness: Double
    public var spiciness: Double
    public var texture: Double

    public init(
        umami: Double,
        acidity: Double,
        sweetness: Double,
        spiciness: Double,
        texture: Double
    ) {
        self.umami = umami
        self.acidity = acidity
        self.sweetness = sweetness
        self.spiciness = spiciness
        self.texture = texture
    }

    public init(profile: FlavorProfile) {
        self.umami = profile.umami
        self.acidity = profile.acidity
        self.sweetness = profile.sweetness
        self.spiciness = profile.spiciness
        self.texture = profile.texture
    }

    /// Full 5-value animatable data hierarchy for seamless spring animations
    public var animatableData: AnimatablePair<Double, AnimatablePair<Double, AnimatablePair<Double, AnimatablePair<Double, Double>>>> {
        get {
            AnimatablePair(
                umami,
                AnimatablePair(
                    acidity,
                    AnimatablePair(
                        sweetness,
                        AnimatablePair(spiciness, texture)
                    )
                )
            )
        }
        set {
            umami = newValue.first
            acidity = newValue.second.first
            sweetness = newValue.second.second.first
            spiciness = newValue.second.second.second.first
            texture = newValue.second.second.second.second
        }
    }

    public func path(in rect: CGRect) -> Path {
        var path = Path()
        let center = CGPoint(x: rect.midX, y: rect.midY)
        let radius = min(rect.width, rect.height) / 2
        let values = [umami, acidity, sweetness, spiciness, texture]

        for i in 0..<5 {
            // Keep a tiny aesthetic minimum floor (0.05) so the polygon never completely collapses to 0
            let normalized = max(min(values[i], 1.0), 0.05)
            let r = radius * CGFloat(normalized)
            let angle = -Double.pi / 2 + Double(i) * (2 * Double.pi / 5)
            let point = CGPoint(
                x: center.x + r * CGFloat(cos(angle)),
                y: center.y + r * CGFloat(sin(angle))
            )
            if i == 0 {
                path.move(to: point)
            } else {
                path.addLine(to: point)
            }
        }
        path.closeSubpath()
        return path
    }
}

/// Dynamic golden dots rendered at each active vertex, fully animatable in sync with the polygon
public struct RadarDotsShape: Shape {
    public var umami: Double
    public var acidity: Double
    public var sweetness: Double
    public var spiciness: Double
    public var texture: Double
    public var dotRadius: CGFloat = 3.5

    public init(
        umami: Double,
        acidity: Double,
        sweetness: Double,
        spiciness: Double,
        texture: Double,
        dotRadius: CGFloat = 3.5
    ) {
        self.umami = umami
        self.acidity = acidity
        self.sweetness = sweetness
        self.spiciness = spiciness
        self.texture = texture
        self.dotRadius = dotRadius
    }

    public init(profile: FlavorProfile, dotRadius: CGFloat = 3.5) {
        self.umami = profile.umami
        self.acidity = profile.acidity
        self.sweetness = profile.sweetness
        self.spiciness = profile.spiciness
        self.texture = profile.texture
        self.dotRadius = dotRadius
    }

    public var animatableData: AnimatablePair<Double, AnimatablePair<Double, AnimatablePair<Double, AnimatablePair<Double, Double>>>> {
        get {
            AnimatablePair(
                umami,
                AnimatablePair(
                    acidity,
                    AnimatablePair(
                        sweetness,
                        AnimatablePair(spiciness, texture)
                    )
                )
            )
        }
        set {
            umami = newValue.first
            acidity = newValue.second.first
            sweetness = newValue.second.second.first
            spiciness = newValue.second.second.second.first
            texture = newValue.second.second.second.second
        }
    }

    public func path(in rect: CGRect) -> Path {
        var path = Path()
        let center = CGPoint(x: rect.midX, y: rect.midY)
        let radius = min(rect.width, rect.height) / 2
        let values = [umami, acidity, sweetness, spiciness, texture]

        for i in 0..<5 {
            let normalized = max(min(values[i], 1.0), 0.05)
            let r = radius * CGFloat(normalized)
            let angle = -Double.pi / 2 + Double(i) * (2 * Double.pi / 5)
            let point = CGPoint(
                x: center.x + r * CGFloat(cos(angle)),
                y: center.y + r * CGFloat(sin(angle))
            )
            let dotRect = CGRect(
                x: point.x - dotRadius,
                y: point.y - dotRadius,
                width: dotRadius * 2,
                height: dotRadius * 2
            )
            path.addEllipse(in: dotRect)
        }
        return path
    }
}

// MARK: - FlavorWheelView Main Component

/// 5-axis sensory taste profile radar chart for Haute Cuisine dish analysis
public struct FlavorWheelView: View {
    public var profile: FlavorProfile
    public var title: String?
    public var language: AxisLanguage
    public var showValueLabels: Bool
    public var chartDiameter: CGFloat

    public init(
        profile: FlavorProfile,
        title: String? = nil,
        language: AxisLanguage = .russian,
        showValueLabels: Bool = true,
        chartDiameter: CGFloat = 240
    ) {
        self.profile = profile
        self.title = title
        self.language = language
        self.showValueLabels = showValueLabels
        self.chartDiameter = chartDiameter
    }

    public var body: some View {
        VStack(spacing: 12) {
            // Optional Sensory Header
            if let title {
                HStack(spacing: 6) {
                    Image(systemName: "dial.low.fill")
                        .font(.system(size: 11))
                        .foregroundStyle(Color.champagneAccent)
                    Text(title.uppercased())
                        .font(.luxeMicroLabel)
                        .tracking(1.8)
                        .foregroundStyle(Color.champagneAccent)
                }
            }

            // Radar Visual Body
            ZStack {
                // Outer container frame bounding radar + labels
                let radius = chartDiameter / 2

                // 1. Concentric Background Grid
                RadarGridShape()
                    .stroke(Color.glassBorder.opacity(0.85), lineWidth: 1)
                    .frame(width: chartDiameter, height: chartDiameter)

                // 2. Subtle Concentric Percentage Tick Marks (along top vertical axis)
                VStack(spacing: 0) {
                    ForEach([1.0, 0.8, 0.6, 0.4, 0.2], id: \.self) { level in
                        Text("\(Int(level * 100))")
                            .font(.system(size: 8, weight: .semibold, design: .monospaced))
                            .foregroundStyle(Color.textSecondary.opacity(0.4))
                            .frame(height: radius * 0.2, alignment: .top)
                    }
                }
                .offset(y: -radius / 2 - 4)

                // 3. Champagne Gradient Fill for Value Polygon
                RadarPolygonShape(profile: profile)
                    .fill(
                        RadialGradient(
                            colors: [
                                Color.champagneAccent.opacity(0.48),
                                Color.champagneAccent.opacity(0.20),
                                Color.champagneAccent.opacity(0.06)
                            ],
                            center: .center,
                            startRadius: 0,
                            endRadius: radius
                        )
                    )
                    .frame(width: chartDiameter, height: chartDiameter)

                // 4. Luminous Border Outline for Value Polygon
                RadarPolygonShape(profile: profile)
                    .stroke(
                        LinearGradient(
                            colors: [
                                Color(hex: 0xF7D89C),
                                Color.champagneAccent,
                                Color(hex: 0xBF823D)
                            ],
                            startPoint: .top,
                            endPoint: .bottom
                        ),
                        style: StrokeStyle(lineWidth: 2, lineCap: .round, lineJoin: .round)
                    )
                    .frame(width: chartDiameter, height: chartDiameter)
                    .shadow(color: Color.champagneAccent.opacity(0.55), radius: 8, x: 0, y: 0)

                // 5. Golden Dot Anchors on Polygon Vertices
                RadarDotsShape(profile: profile, dotRadius: 4)
                    .fill(Color.champagneAccent)
                    .frame(width: chartDiameter, height: chartDiameter)
                    .shadow(color: Color.champagneAccent.opacity(0.9), radius: 4)

                // 6. 5-Axis Outer Labels
                ForEach(0..<5, id: \.self) { index in
                    axisLabelView(index: index, radius: radius)
                }
            }
            .frame(width: chartDiameter + 110, height: chartDiameter + 80)
        }
    }

    // MARK: - Axis Label Placement

    @ViewBuilder
    private func axisLabelView(index: Int, radius: CGFloat) -> some View {
        let axes = FlavorProfile.Axis.allCases
        let axis = axes[index]
        let angle = -Double.pi / 2 + Double(index) * (2 * Double.pi / 5)
        let labelDistance = radius + 26
        let offsetX = labelDistance * CGFloat(cos(angle))
        let offsetY = labelDistance * CGFloat(sin(angle))
        let value = profile.value(for: axis)

        VStack(spacing: 1) {
            Text(language.title(for: axis))
                .font(.system(size: 10, weight: .bold, design: .serif))
                .foregroundStyle(Color.textPrimary)
                .lineLimit(1)

            if showValueLabels {
                Text("\(Int(value * 100))%")
                    .font(.system(size: 9.5, weight: .semibold, design: .monospaced))
                    .foregroundStyle(Color.champagneAccent)
            }
        }
        .offset(x: offsetX, y: offsetY)
    }
}

// MARK: - Interactive Preview

#Preview("FlavorWheel Sensory Radar") {
    FlavorWheelPreviewContainer()
}

/// Interactive preview showcase with quick presets and 5-axis real-time tuning sliders
public struct FlavorWheelPreviewContainer: View {
    @State private var selectedDishIndex: Int = 0
    @State private var language: AxisLanguage = .russian
    @State private var showValues: Bool = true

    // Editable active profile values
    @State private var umami: Double = 0.85
    @State private var acidity: Double = 0.65
    @State private var sweetness: Double = 0.40
    @State private var spiciness: Double = 0.20
    @State private var texture: Double = 0.75

    private let presetDishes: [(name: String, course: String, profile: FlavorProfile)] = [
        (
            name: "Hokkaido Scallops",
            course: "Prelude",
            profile: FlavorProfile(umami: 0.85, acidity: 0.65, sweetness: 0.40, spiciness: 0.20, texture: 0.75)
        ),
        (
            name: "A5 Kagoshima Wagyu",
            course: "Main Course",
            profile: FlavorProfile(umami: 0.98, acidity: 0.40, sweetness: 0.40, spiciness: 0.20, texture: 0.90)
        ),
        (
            name: "Smoked Chocolate Soufflé",
            course: "Dessert",
            profile: FlavorProfile(umami: 0.30, acidity: 0.30, sweetness: 0.88, spiciness: 0.10, texture: 0.80)
        ),
        (
            name: "Heirloom Beetroot",
            course: "Garden",
            profile: FlavorProfile(umami: 0.50, acidity: 0.75, sweetness: 0.65, spiciness: 0.10, texture: 0.70)
        )
    ]

    private var activeProfile: FlavorProfile {
        FlavorProfile(
            umami: umami,
            acidity: acidity,
            sweetness: sweetness,
            spiciness: spiciness,
            texture: texture
        )
    }

    public init() {}

    public var body: some View {
        ZStack {
            Color.obsidianCanvas
                .ignoresSafeArea()

            ScrollView {
                VStack(spacing: 24) {
                    // Header
                    VStack(spacing: 6) {
                        Text("SENSORY PROFILE VISUALIZATION")
                            .font(.luxeMicroLabel)
                            .tracking(2)
                            .foregroundStyle(Color.champagneAccent)

                        Text("5-Axis Flavor Wheel")
                            .font(.luxeDishTitle)
                            .foregroundStyle(Color.textPrimary)

                        Text("Harmonic radar chart with spring interpolation and champagne glow.")
                            .font(.luxeSubheadline)
                            .foregroundStyle(Color.textSecondary)
                            .multilineTextAlignment(.center)
                            .padding(.horizontal, 20)
                    }
                    .padding(.top, 10)

                    // Preset Quick Pickers
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 10) {
                            ForEach(0..<presetDishes.count, id: \.self) { index in
                                let dish = presetDishes[index]
                                let isSelected = selectedDishIndex == index

                                Button {
                                    withAnimation(.spring(response: 0.6, dampingFraction: 0.75)) {
                                        selectedDishIndex = index
                                        umami = dish.profile.umami
                                        acidity = dish.profile.acidity
                                        sweetness = dish.profile.sweetness
                                        spiciness = dish.profile.spiciness
                                        texture = dish.profile.texture
                                    }
                                } label: {
                                    VStack(alignment: .leading, spacing: 2) {
                                        Text(dish.course.uppercased())
                                            .font(.system(size: 8, weight: .bold, design: .monospaced))
                                            .foregroundStyle(isSelected ? Color.champagneAccent : Color.textSecondary)
                                        Text(dish.name)
                                            .font(.system(size: 12, weight: .semibold, design: .serif))
                                            .foregroundStyle(isSelected ? Color.textPrimary : Color.textSecondary)
                                    }
                                    .padding(.horizontal, 14)
                                    .padding(.vertical, 8)
                                    .background(
                                        RoundedRectangle(cornerRadius: 12)
                                            .fill(isSelected ? Color.champagneAccent.opacity(0.18) : Color.cardSurface)
                                    )
                                    .overlay(
                                        RoundedRectangle(cornerRadius: 12)
                                            .stroke(isSelected ? Color.champagneAccent : Color.glassBorder, lineWidth: 1)
                                    )
                                }
                            }
                        }
                        .padding(.horizontal, 20)
                    }

                    // Main Radar Chart inside GlassCard
                    GlassCard(cornerRadius: 20, fillOpacity: 0.9, padding: 20) {
                        VStack(spacing: 16) {
                            FlavorWheelView(
                                profile: activeProfile,
                                title: presetDishes[selectedDishIndex].name,
                                language: language,
                                showValueLabels: showValues,
                                chartDiameter: 220
                            )

                            // Controls strip (Language & Values toggle)
                            HStack {
                                Picker("Language", selection: $language) {
                                    ForEach(AxisLanguage.allCases) { lang in
                                        Text(lang.rawValue).tag(lang)
                                    }
                                }
                                .pickerStyle(.segmented)
                                .frame(maxWidth: 160)

                                Spacer()

                                Button {
                                    withAnimation(.easeInOut(duration: 0.2)) {
                                        showValues.toggle()
                                    }
                                } label: {
                                    HStack(spacing: 4) {
                                        Image(systemName: showValues ? "checkmark.circle.fill" : "circle")
                                            .font(.system(size: 11))
                                        Text("Labels")
                                            .font(.system(size: 11, weight: .medium))
                                    }
                                    .foregroundStyle(showValues ? Color.champagneAccent : Color.textSecondary)
                                    .padding(.horizontal, 10)
                                    .padding(.vertical, 6)
                                    .background(Color.cardSurface)
                                    .clipShape(Capsule())
                                    .overlay(Capsule().stroke(Color.glassBorder, lineWidth: 1))
                                }
                            }
                        }
                    }
                    .padding(.horizontal, 20)

                    // Real-Time Axis Tuning Sliders
                    VStack(alignment: .leading, spacing: 14) {
                        Text("MANUAL SENSORY TUNING (LIVE MORPH)")
                            .font(.luxeMicroLabel)
                            .tracking(1.5)
                            .foregroundStyle(Color.textSecondary)

                        VStack(spacing: 12) {
                            sliderRow(title: "Умами (Umami)", value: $umami)
                            sliderRow(title: "Кислотность (Acidity)", value: $acidity)
                            sliderRow(title: "Сладость (Sweetness)", value: $sweetness)
                            sliderRow(title: "Пряность (Spiciness)", value: $spiciness)
                            sliderRow(title: "Текстура (Texture)", value: $texture)
                        }
                        .padding(16)
                        .glassCard(cornerRadius: 16, fillOpacity: 0.6)
                    }
                    .padding(.horizontal, 20)
                    .padding(.bottom, 30)
                }
            }
        }
    }

    private func sliderRow(title: String, value: Binding<Double>) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                Text(title)
                    .font(.system(size: 12, weight: .medium, design: .serif))
                    .foregroundStyle(Color.textPrimary)
                Spacer()
                Text("\(Int(value.wrappedValue * 100))%")
                    .font(.system(size: 12, weight: .bold, design: .monospaced))
                    .foregroundStyle(Color.champagneAccent)
            }

            Slider(value: value, in: 0.0...1.0, step: 0.02)
                .tint(Color.champagneAccent)
                .animation(.spring(response: 0.35, dampingFraction: 0.7), value: value.wrappedValue)
        }
    }
}
