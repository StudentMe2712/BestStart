//
//  GlassCard.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

// MARK: - ViewModifier for Frosted Luxe Glass Surface

/// ViewModifier providing an ultra-refined Dark Luxe frosted glass container
public struct GlassCardModifier: ViewModifier {
    public var cornerRadius: CGFloat
    public var strokeColor: Color
    public var fillOpacity: Double
    public var hasInnerGlow: Bool

    public init(
        cornerRadius: CGFloat = 16,
        strokeColor: Color = .glassBorder,
        fillOpacity: Double = 0.85,
        hasInnerGlow: Bool = true
    ) {
        self.cornerRadius = cornerRadius
        self.strokeColor = strokeColor
        self.fillOpacity = fillOpacity
        self.hasInnerGlow = hasInnerGlow
    }

    public func body(content: Content) -> some View {
        content
            .background {
                ZStack {
                    // Base dark surface
                    RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                        .fill(Color.cardSurface.opacity(fillOpacity))

                    // Material depth reflection
                    RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                        .fill(.ultraThinMaterial.opacity(0.25))

                    // Subtle top-down specular highlight sheen
                    if hasInnerGlow {
                        RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                            .fill(
                                LinearGradient(
                                    colors: [
                                        Color.white.opacity(0.04),
                                        Color.clear,
                                        Color.black.opacity(0.12)
                                    ],
                                    startPoint: .top,
                                    endPoint: .bottom
                                )
                            )
                    }
                }
            }
            .overlay {
                // Precision 1px frosted glass stroke with subtle dynamic gradient
                RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                    .strokeBorder(
                        LinearGradient(
                            colors: [
                                strokeColor.opacity(1.0),
                                strokeColor.opacity(0.45),
                                strokeColor.opacity(0.15)
                            ],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        ),
                        style: StrokeStyle(lineWidth: 1)
                    )
            }
            // Multi-layered ambient depth shadows
            .shadow(color: Color.black.opacity(0.55), radius: 16, x: 0, y: 8)
            .shadow(color: strokeColor.opacity(0.08), radius: 24, x: 0, y: 0)
    }
}

// MARK: - View Extension

extension View {
    /// Wraps the view inside a Dark Luxe frosted glass surface with customizable corner radius, stroke and opacity
    public func glassCard(
        cornerRadius: CGFloat = 16,
        strokeColor: Color = .glassBorder,
        fillOpacity: Double = 0.85,
        hasInnerGlow: Bool = true
    ) -> some View {
        modifier(
            GlassCardModifier(
                cornerRadius: cornerRadius,
                strokeColor: strokeColor,
                fillOpacity: fillOpacity,
                hasInnerGlow: hasInnerGlow
            )
        )
    }
}

// MARK: - Standalone GlassCard Container

/// Container view presenting arbitrary child views within a Dark Luxe frosted glass card
public struct GlassCard<Content: View>: View {
    private let cornerRadius: CGFloat
    private let strokeColor: Color
    private let fillOpacity: Double
    private let hasInnerGlow: Bool
    private let contentPadding: CGFloat?
    private let content: Content

    public init(
        cornerRadius: CGFloat = 16,
        strokeColor: Color = .glassBorder,
        fillOpacity: Double = 0.85,
        hasInnerGlow: Bool = true,
        padding: CGFloat? = 18,
        @ViewBuilder content: () -> Content
    ) {
        self.cornerRadius = cornerRadius
        self.strokeColor = strokeColor
        self.fillOpacity = fillOpacity
        self.hasInnerGlow = hasInnerGlow
        self.contentPadding = padding
        self.content = content()
    }

    public var body: some View {
        Group {
            if let padding = contentPadding {
                content
                    .padding(padding)
            } else {
                content
            }
        }
        .glassCard(
            cornerRadius: cornerRadius,
            strokeColor: strokeColor,
            fillOpacity: fillOpacity,
            hasInnerGlow: hasInnerGlow
        )
    }
}

// MARK: - Interactive Preview

#Preview("GlassCard Interactive Showcase") {
    GlassCardPreviewView()
}

/// Dedicated live canvas preview with customizable glass parameters and sample dish card
public struct GlassCardPreviewView: View {
    @State private var cornerRadius: CGFloat = 18
    @State private var fillOpacity: Double = 0.85
    @State private var selectedBorderIndex: Int = 0
    @State private var isAddedToFlight: Bool = false
    @State private var isBookmarked: Bool = false

    private let borderOptions: [(title: String, color: Color)] = [
        ("Glass Border", .glassBorder),
        ("Champagne", .champagneAccent),
        ("Burgundy", .burgundyAccent),
        ("Sage", .sageGreen)
    ]

    public init() {}

    public var body: some View {
        ZStack {
            Color.obsidianCanvas
                .ignoresSafeArea()

            // Subtle background decorative ambient glow
            RadialGradient(
                colors: [
                    Color.champagneAccent.opacity(0.08),
                    Color.clear
                ],
                center: .topTrailing,
                startRadius: 40,
                endRadius: 420
            )
            .ignoresSafeArea()

            ScrollView {
                VStack(spacing: 28) {
                    // Header
                    VStack(spacing: 6) {
                        Text("CARTE BLANCHE SURFACES")
                            .font(.luxeMicroLabel)
                            .tracking(2)
                            .foregroundStyle(Color.champagneAccent)

                        Text("Frosted GlassCard")
                            .font(.luxeDishTitle)
                            .foregroundStyle(Color.textPrimary)

                        Text("Tactile Michelin-grade glassmorphism with dynamic light sheen.")
                            .font(.luxeSubheadline)
                            .foregroundStyle(Color.textSecondary)
                            .multilineTextAlignment(.center)
                            .padding(.horizontal, 20)
                    }
                    .padding(.top, 12)

                    // Hero Sample Card
                    GlassCard(
                        cornerRadius: cornerRadius,
                        strokeColor: borderOptions[selectedBorderIndex].color,
                        fillOpacity: fillOpacity,
                        padding: 20
                    ) {
                        VStack(alignment: .leading, spacing: 16) {
                            // Top Badges & Bookmark Row
                            HStack {
                                HStack(spacing: 6) {
                                    Image(systemName: "crown.fill")
                                        .font(.system(size: 11))
                                        .foregroundStyle(Color.champagneAccent)
                                    Text("CHEF'S SIGNATURE")
                                        .font(.luxeMicroLabel)
                                        .tracking(1.2)
                                        .foregroundStyle(Color.champagneAccent)
                                }
                                .padding(.horizontal, 10)
                                .padding(.vertical, 5)
                                .background(Color.champagneAccent.opacity(0.12))
                                .clipShape(Capsule())
                                .overlay(
                                    Capsule()
                                        .stroke(Color.champagneAccent.opacity(0.3), lineWidth: 1)
                                )

                                Spacer()

                                Button {
                                    withAnimation(.spring(duration: 0.3)) {
                                        isBookmarked.toggle()
                                    }
                                } label: {
                                    Image(systemName: isBookmarked ? "bookmark.fill" : "bookmark")
                                        .font(.system(size: 15, weight: .medium))
                                        .foregroundStyle(isBookmarked ? Color.champagneAccent : Color.textSecondary)
                                        .frame(width: 32, height: 32)
                                        .background(Color.cardSurface)
                                        .clipShape(Circle())
                                        .overlay(Circle().stroke(Color.glassBorder, lineWidth: 1))
                                }
                            }

                            // Dish Title & Origin
                            VStack(alignment: .leading, spacing: 4) {
                                Text("Pan-Seared Hokkaido Scallops")
                                    .font(.luxeDishTitle)
                                    .foregroundStyle(Color.textPrimary)

                                HStack(spacing: 6) {
                                    Image(systemName: "mappin.and.ellipse")
                                        .font(.system(size: 11))
                                    Text("Okhotsk Cold Currents • Hokkaido, Japan")
                                        .font(.system(size: 12, weight: .regular))
                                }
                                .foregroundStyle(Color.textSecondary)
                            }

                            // Culinary Description
                            Text("Wild-caught diver scallops with yuzu kosho emulsion, charred leek oil, and crystalline sea grapes. Caramelized gently over Japanese binchotan charcoal.")
                                .font(.luxeBody)
                                .foregroundStyle(Color.textSecondary)
                                .lineSpacing(3)

                            Divider()
                                .background(Color.glassBorder)

                            // Sommelier Pairing Preview
                            HStack(spacing: 12) {
                                Image(systemName: "wineglass.fill")
                                    .font(.system(size: 16))
                                    .foregroundStyle(Color.burgundyAccent)
                                    .frame(width: 36, height: 36)
                                    .background(Color.burgundyAccent.opacity(0.15))
                                    .clipShape(Circle())

                                VStack(alignment: .leading, spacing: 2) {
                                    Text("SOMMELIER PAIRING")
                                        .font(.system(size: 9, weight: .bold, design: .monospaced))
                                        .tracking(1)
                                        .foregroundStyle(Color.burgundyAccent)
                                    Text("2021 Sancerre 'Les Monts Damnés'")
                                        .font(.system(size: 13, weight: .semibold, design: .serif))
                                        .foregroundStyle(Color.textPrimary)
                                }
                                Spacer()
                            }
                            .padding(10)
                            .background(Color.obsidianCanvas.opacity(0.5))
                            .clipShape(RoundedRectangle(cornerRadius: 10))

                            // Bottom Row: Price & Golden Action Button
                            HStack(alignment: .center) {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text("COURS I • PRELUDE")
                                        .font(.system(size: 9, weight: .semibold, design: .monospaced))
                                        .foregroundStyle(Color.textSecondary)
                                    Text("€34.00")
                                        .font(.luxePrice)
                                        .foregroundStyle(Color.champagneAccent)
                                }

                                Spacer()

                                Button {
                                    withAnimation(.spring(response: 0.35, dampingFraction: 0.65)) {
                                        isAddedToFlight.toggle()
                                    }
                                } label: {
                                    HStack(spacing: 6) {
                                        Image(systemName: isAddedToFlight ? "checkmark" : "plus")
                                            .font(.system(size: 12, weight: .bold))
                                        Text(isAddedToFlight ? "Added to Tasting" : "Add to Flight")
                                            .font(.system(size: 13, weight: .bold))
                                    }
                                    .padding(.horizontal, 16)
                                    .padding(.vertical, 10)
                                    .background(
                                        isAddedToFlight ?
                                        LinearGradient(colors: [Color.sageGreen, Color.sageGreen.opacity(0.8)], startPoint: .leading, endPoint: .trailing) :
                                        Color.goldGradient
                                    )
                                    .foregroundStyle(isAddedToFlight ? Color.white : Color.black)
                                    .clipShape(Capsule())
                                    .shadow(
                                        color: isAddedToFlight ? Color.sageGreen.opacity(0.3) : Color.champagneAccent.opacity(0.3),
                                        radius: 10,
                                        y: 4
                                    )
                                }
                            }
                        }
                    }
                    .padding(.horizontal, 20)

                    // Interactive Customizer Controls
                    VStack(alignment: .leading, spacing: 16) {
                        Text("INTERACTIVE CARD TUNING")
                            .font(.luxeMicroLabel)
                            .tracking(1.5)
                            .foregroundStyle(Color.textSecondary)

                        VStack(spacing: 16) {
                            // Stroke Preset Selector
                            VStack(alignment: .leading, spacing: 8) {
                                Text("Border Stroke Style")
                                    .font(.system(size: 12, weight: .medium))
                                    .foregroundStyle(Color.textPrimary)

                                HStack(spacing: 8) {
                                    ForEach(0..<borderOptions.count, id: \.self) { index in
                                        let option = borderOptions[index]
                                        Button {
                                            withAnimation(.spring(duration: 0.25)) {
                                                selectedBorderIndex = index
                                            }
                                        } label: {
                                            Text(option.title)
                                                .font(.system(size: 11, weight: .semibold))
                                                .padding(.horizontal, 12)
                                                .padding(.vertical, 6)
                                                .background(
                                                    Capsule()
                                                        .fill(selectedBorderIndex == index ? option.color.opacity(0.2) : Color.cardSurface)
                                                )
                                                .overlay(
                                                    Capsule()
                                                        .stroke(selectedBorderIndex == index ? option.color : Color.glassBorder, lineWidth: 1)
                                                )
                                                .foregroundStyle(selectedBorderIndex == index ? Color.textPrimary : Color.textSecondary)
                                        }
                                    }
                                }
                            }

                            // Corner Radius Slider
                            VStack(alignment: .leading, spacing: 6) {
                                HStack {
                                    Text("Corner Radius")
                                        .font(.system(size: 12, weight: .medium))
                                        .foregroundStyle(Color.textPrimary)
                                    Spacer()
                                    Text("\(Int(cornerRadius)) pt")
                                        .font(.system(size: 12, weight: .bold, design: .monospaced))
                                        .foregroundStyle(Color.champagneAccent)
                                }
                                Slider(value: $cornerRadius, in: 8...32, step: 1)
                                    .tint(Color.champagneAccent)
                            }

                            // Fill Opacity Slider
                            VStack(alignment: .leading, spacing: 6) {
                                HStack {
                                    Text("Surface Opacity")
                                        .font(.system(size: 12, weight: .medium))
                                        .foregroundStyle(Color.textPrimary)
                                    Spacer()
                                    Text("\(Int(fillOpacity * 100))%")
                                        .font(.system(size: 12, weight: .bold, design: .monospaced))
                                        .foregroundStyle(Color.champagneAccent)
                                }
                                Slider(value: $fillOpacity, in: 0.40...1.0, step: 0.05)
                                    .tint(Color.champagneAccent)
                            }
                        }
                        .padding(16)
                        .glassCard(cornerRadius: 14, strokeColor: .glassBorder, fillOpacity: 0.6)
                    }
                    .padding(.horizontal, 20)
                    .padding(.bottom, 30)
                }
            }
        }
    }
}
