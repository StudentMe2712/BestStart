//
//  Color+Theme.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

// MARK: - Haute Cuisine Palette Tokens

extension Color {
    /// Deep charcoal-obsidian canvas background (#0E1013)
    public static let obsidianCanvas = Color(hex: 0x0E1013)

    /// Elevated card surface backing (#16191F)
    public static let cardSurface = Color(hex: 0x16191F)

    /// Subtle frosted glass border stroke (#262B35)
    public static let glassBorder = Color(hex: 0x262B35)

    /// Warm muted champagne gold primary accent (#E5A962)
    public static let champagneAccent = Color(hex: 0xE5A962)

    /// Rich sommelier burgundy wine accent (#8C2D38)
    public static let burgundyAccent = Color(hex: 0x8C2D38)

    /// Secondary burgundy variant for subtle accents (#8C2D38)
    public static let burgundySubAccent = Color(hex: 0x8C2D38)

    /// Earthy botanical sage green for vegetarian/vegan tags (#5E8C6A)
    public static let sageGreen = Color(hex: 0x5E8C6A)

    /// Cool slate blue for allergen & dairy indicators (#4A6B82)
    public static let slateBlue = Color(hex: 0x4A6B82)

    /// Deep ocean teal for marine & pescatarian tags (#2C7A7B)
    public static let oceanTeal = Color(hex: 0x2C7A7B)

    /// Soft white primary editorial text with 95% opacity (#F5F6F8)
    public static let textPrimary = Color(hex: 0xF5F6F8).opacity(0.95)

    /// Slate muted secondary editorial text (#8E95A5)
    public static let textSecondary = Color(hex: 0x8E95A5)

    /// Luminous champagne gold gradient for buttons, highlights, and borders
    public static var goldGradient: LinearGradient {
        LinearGradient(
            colors: [
                Color(hex: 0xF7D89C),
                Color(hex: 0xE5A962),
                Color(hex: 0xBF823D)
            ],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )
    }

    /// Velvety vintage wine burgundy gradient for cellar selections and pairings
    public static var burgundyGradient: LinearGradient {
        LinearGradient(
            colors: [
                Color(hex: 0xB53A49),
                Color(hex: 0x8C2D38),
                Color(hex: 0x56151E)
            ],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )
    }

    /// Dark obsidian depth gradient for layered background surfaces
    public static var surfaceGradient: LinearGradient {
        LinearGradient(
            colors: [
                Color(hex: 0x1A1E26),
                Color(hex: 0x12151B)
            ],
            startPoint: .top,
            endPoint: .bottom
        )
    }
}

// MARK: - LinearGradient ShapeStyle Conformance

extension LinearGradient {
    /// Luminous champagne gold gradient
    public static var goldGradient: LinearGradient {
        Color.goldGradient
    }

    /// Rich vintage burgundy gradient
    public static var burgundyGradient: LinearGradient {
        Color.burgundyGradient
    }

    /// Deep dark surface gradient
    public static var surfaceGradient: LinearGradient {
        Color.surfaceGradient
    }
}

// MARK: - Hex Initializers

extension Color {
    /// Initialize a SwiftUI `Color` with a 24-bit RGB integer (e.g. `0x0E1013`) and optional alpha
    public init(hex: UInt32, alpha: Double = 1.0) {
        let red = Double((hex >> 16) & 0xFF) / 255.0
        let green = Double((hex >> 8) & 0xFF) / 255.0
        let blue = Double(hex & 0xFF) / 255.0
        self.init(.sRGB, red: red, green: green, blue: blue, opacity: alpha)
    }

    /// Initialize a SwiftUI `Color` with a hex string (e.g. `"#0E1013"` or `"0E1013"` or with alpha `"#0E1013FF"`)
    public init(hexString: String, defaultColor: Color = .clear) {
        let cleaned = hexString
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: "#", with: "")

        var hexInt: UInt64 = 0
        guard Scanner(string: cleaned).scanHexInt64(&hexInt) else {
            self = defaultColor
            return
        }

        switch cleaned.count {
        case 3: // RGB (12-bit)
            let r = Double((hexInt >> 8) * 17) / 255.0
            let g = Double(((hexInt >> 4) & 0xF) * 17) / 255.0
            let b = Double((hexInt & 0xF) * 17) / 255.0
            self.init(.sRGB, red: r, green: g, blue: b, opacity: 1.0)
        case 6: // RGB (24-bit)
            let r = Double((hexInt >> 16) & 0xFF) / 255.0
            let g = Double((hexInt >> 8) & 0xFF) / 255.0
            let b = Double(hexInt & 0xFF) / 255.0
            self.init(.sRGB, red: r, green: g, blue: b, opacity: 1.0)
        case 8: // ARGB or RGBA (32-bit: assumed RRGGBBAA)
            let r = Double((hexInt >> 24) & 0xFF) / 255.0
            let g = Double((hexInt >> 16) & 0xFF) / 255.0
            let b = Double((hexInt >> 8) & 0xFF) / 255.0
            let a = Double(hexInt & 0xFF) / 255.0
            self.init(.sRGB, red: r, green: g, blue: b, opacity: a)
        default:
            self = defaultColor
        }
    }
}

// MARK: - Dark Luxe Typography Hierarchy

extension Font {
    /// Michelin-guide serif title (Large Title, 34pt bold serif)
    public static let luxeLargeTitle = Font.system(.largeTitle, design: .serif).weight(.bold)

    /// Michelin-guide dish title (Title 2, 22pt bold serif)
    public static let luxeDishTitle = Font.system(.title2, design: .serif).weight(.bold)

    /// Subheading serif (Title 3, 20pt serif)
    public static let luxeTitle3 = Font.system(.title3, design: .serif).weight(.semibold)

    /// Haute cuisine course banner (Headline, serif)
    public static let luxeHeadline = Font.system(.headline, design: .serif).weight(.semibold)

    /// Editorial tasting description (Subheadline, San Francisco)
    public static let luxeSubheadline = Font.system(.subheadline, design: .default)

    /// Refined culinary story body text (Body, San Francisco)
    public static let luxeBody = Font.system(.body, design: .default)

    /// Monospaced decimal luxury price label (Title 3, Rounded, Monospaced Digit)
    public static let luxePrice = Font.system(.title3, design: .rounded).weight(.bold).monospacedDigit()

    /// Compact luxury price for list cards (Subheadline, Rounded, Monospaced Digit)
    public static let luxeCardPrice = Font.system(.subheadline, design: .rounded).weight(.bold).monospacedDigit()

    /// Micro tracking label (Caption 2, Monospaced, Bold)
    public static let luxeMicroLabel = Font.system(.caption2, design: .monospaced).weight(.bold)
}

// MARK: - Interactive Preview

#Preview("Dark Luxe Color Palette & Typography") {
    ColorThemePreviewView()
}

/// Standalone showcase view for Dark Luxe Editorial tokens
public struct ColorThemePreviewView: View {
    @State private var selectedSampleText: String = "Pan-Seared Hokkaido Scallops"

    private let swatches: [(name: String, color: Color, hexString: String, role: String)] = [
        ("Obsidian Canvas", .obsidianCanvas, "#0E1013", "Primary deep app background"),
        ("Card Surface", .cardSurface, "#16191F", "Frosted container backing"),
        ("Glass Border", .glassBorder, "#262B35", "Ultra-thin 1px glass perimeter"),
        ("Champagne Accent", .champagneAccent, "#E5A962", "Prices, chef picks, key buttons"),
        ("Burgundy Accent", .burgundyAccent, "#8C2D38", "Sommelier pairings, cellar notes"),
        ("Sage Green", .sageGreen, "#5E8C6A", "Dietary badges (Vegan, Garden)"),
        ("Slate Blue", .slateBlue, "#4A6B82", "Dairy-free & technical cues"),
        ("Ocean Teal", .oceanTeal, "#2C7A7B", "Seafood & pescatarian badges"),
        ("Text Primary", .textPrimary, "#F5F6F8 (95%)", "Crisp editorial headers & titles"),
        ("Text Secondary", .textSecondary, "#8E95A5", "Subtext, terroir, ingredients")
    ]

    public init() {}

    public var body: some View {
        ZStack {
            Color.obsidianCanvas
                .ignoresSafeArea()

            ScrollView {
                VStack(alignment: .leading, spacing: 28) {
                    // Header
                    VStack(alignment: .leading, spacing: 6) {
                        HStack(spacing: 8) {
                            Circle()
                                .fill(Color.champagneAccent)
                                .frame(width: 8, height: 8)
                            Text("DESIGN SYSTEM TOKENS")
                                .font(.luxeMicroLabel)
                                .tracking(2)
                                .foregroundStyle(Color.champagneAccent)
                        }

                        Text("Dark Luxe Editorial")
                            .font(.luxeLargeTitle)
                            .foregroundStyle(Color.textPrimary)

                        Text("Haute cuisine sensory aesthetic designed for Michelin-grade digital guidance.")
                            .font(.luxeSubheadline)
                            .foregroundStyle(Color.textSecondary)
                    }

                    // Gradients Banner
                    VStack(alignment: .leading, spacing: 12) {
                        Text("SIGNATURE GRADIENTS")
                            .font(.luxeMicroLabel)
                            .tracking(1.5)
                            .foregroundStyle(Color.textSecondary)

                        HStack(spacing: 12) {
                            gradientCard(
                                title: "Gold Champagne",
                                subtitle: "Primary Highlights & CTA",
                                gradient: Color.goldGradient,
                                textColor: .black
                            )

                            gradientCard(
                                title: "Vintage Burgundy",
                                subtitle: "Sommelier Cellar Reserve",
                                gradient: Color.burgundyGradient,
                                textColor: .white
                            )
                        }
                    }

                    // Swatches Grid
                    VStack(alignment: .leading, spacing: 12) {
                        Text("CORE PALETTE SWATCHES")
                            .font(.luxeMicroLabel)
                            .tracking(1.5)
                            .foregroundStyle(Color.textSecondary)

                        LazyVGrid(columns: [GridItem(.adaptive(minimum: 160), spacing: 12)], spacing: 12) {
                            ForEach(swatches, id: \.name) { swatch in
                                swatchCard(swatch)
                            }
                        }
                    }

                    // Typography Showcase
                    VStack(alignment: .leading, spacing: 16) {
                        Text("EDITORIAL TYPOGRAPHY HIERARCHY")
                            .font(.luxeMicroLabel)
                            .tracking(1.5)
                            .foregroundStyle(Color.textSecondary)

                        VStack(alignment: .leading, spacing: 14) {
                            typographyRow(
                                name: "Dish Title (Serif)",
                                sample: selectedSampleText,
                                font: .luxeDishTitle,
                                color: .textPrimary
                            )

                            typographyRow(
                                name: "Course Headline (Serif)",
                                sample: "I. PRELUDE — COLD AMUSE-BOUCHE",
                                font: .luxeHeadline,
                                color: .champagneAccent
                            )

                            typographyRow(
                                name: "Culinary Description (SF Pro)",
                                sample: "Wild-caught diver scallops with yuzu kosho emulsion, charred leek oil, and crystalline sea grapes.",
                                font: .luxeBody,
                                color: .textSecondary
                            )

                            typographyRow(
                                name: "Haute Price Display (Monospaced Rounded)",
                                sample: "€42.00 / glass",
                                font: .luxePrice,
                                color: .champagneAccent
                            )
                        }
                        .padding(16)
                        .background(Color.cardSurface)
                        .clipShape(RoundedRectangle(cornerRadius: 16))
                        .overlay(
                            RoundedRectangle(cornerRadius: 16)
                                .stroke(Color.glassBorder, lineWidth: 1)
                        )
                    }
                }
                .padding(20)
            }
        }
    }

    private func gradientCard(title: String, subtitle: String, gradient: LinearGradient, textColor: Color) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Spacer()
            Text(title)
                .font(.system(.subheadline, design: .serif, weight: .bold))
                .foregroundStyle(textColor)
            Text(subtitle)
                .font(.system(size: 11, weight: .medium))
                .foregroundStyle(textColor.opacity(0.85))
        }
        .frame(maxWidth: .infinity, minHeight: 90, alignment: .bottomLeading)
        .padding(14)
        .background(gradient)
        .clipShape(RoundedRectangle(cornerRadius: 14))
        .shadow(color: Color.black.opacity(0.3), radius: 8, y: 4)
    }

    private func swatchCard(_ swatch: (name: String, color: Color, hexString: String, role: String)) -> some View {
        HStack(spacing: 12) {
            RoundedRectangle(cornerRadius: 8)
                .fill(swatch.color)
                .frame(width: 42, height: 42)
                .overlay(
                    RoundedRectangle(cornerRadius: 8)
                        .stroke(Color.glassBorder.opacity(0.6), lineWidth: 1)
                )

            VStack(alignment: .leading, spacing: 2) {
                Text(swatch.name)
                    .font(.system(size: 13, weight: .semibold, design: .default))
                    .foregroundStyle(Color.textPrimary)
                Text(swatch.hexString)
                    .font(.system(size: 10, weight: .regular, design: .monospaced))
                    .foregroundStyle(Color.champagneAccent)
                Text(swatch.role)
                    .font(.system(size: 9, weight: .regular))
                    .foregroundStyle(Color.textSecondary)
                    .lineLimit(1)
            }
            Spacer()
        }
        .padding(10)
        .background(Color.cardSurface)
        .clipShape(RoundedRectangle(cornerRadius: 12))
        .overlay(
            RoundedRectangle(cornerRadius: 12)
                .stroke(Color.glassBorder, lineWidth: 1)
        )
    }

    private func typographyRow(name: String, sample: String, font: Font, color: Color) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(name.uppercased())
                .font(.system(size: 9, weight: .bold, design: .monospaced))
                .tracking(1)
                .foregroundStyle(Color.textSecondary)
            Text(sample)
                .font(font)
                .foregroundStyle(color)
        }
    }
}
