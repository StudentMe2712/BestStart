//
//  PairingCardView.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

/// Exquisite sommelier recommendation card styled with rich vintage burgundy tones and Dark Luxe glassmorphism
public struct PairingCardView: View {

    // MARK: - Properties

    public let pairing: Pairing
    public var showHeadline: Bool
    public var isCompact: Bool

    // MARK: - Initializer

    public init(
        pairing: Pairing,
        showHeadline: Bool = true,
        isCompact: Bool = false
    ) {
        self.pairing = pairing
        self.showHeadline = showHeadline
        self.isCompact = isCompact
    }

    // MARK: - Body

    public var body: some View {
        VStack(alignment: .leading, spacing: isCompact ? 10 : 14) {
            // Header: Sommelier badge, beverage icon & beverage type tag
            if showHeadline {
                headerView
            }

            // Beverage Name & Origin Details
            beverageInfoView

            // Service Temperature Indicator (if available)
            if let temperature = pairing.temperature, !temperature.isEmpty {
                temperaturePill(temperature)
            }

            // Sommelier Tasting Notes & Flavor Harmony
            tastingNotesView
        }
        .padding(isCompact ? 14 : 18)
        .background(cardBackground)
        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
        .overlay(cardBorder)
        .shadow(color: Color.burgundyAccent.opacity(0.18), radius: 16, x: 0, y: 6)
        .shadow(color: Color.black.opacity(0.45), radius: 10, x: 0, y: 4)
    }

    // MARK: - Subviews

    /// Top header strip with sommelier title, icon and beverage category badge
    private var headerView: some View {
        HStack(alignment: .center) {
            HStack(spacing: 8) {
                // Circular beverage icon badge
                ZStack {
                    Circle()
                        .fill(
                            LinearGradient(
                                colors: [
                                    Color.burgundyAccent.opacity(0.35),
                                    Color.burgundyAccent.opacity(0.15)
                                ],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            )
                        )
                        .frame(width: 28, height: 28)
                        .overlay(
                            Circle()
                                .stroke(Color.burgundyAccent.opacity(0.5), lineWidth: 1)
                        )

                    Image(systemName: beverageIcon)
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(Color.burgundyAccent)
                }

                VStack(alignment: .leading, spacing: 1) {
                    Text("SOMMELIER'S PAIRING")
                        .font(.luxeMicroLabel)
                        .tracking(1.8)
                        .foregroundStyle(Color.burgundyAccent)

                    Text("Винный пейринг")
                        .font(.system(size: 10, weight: .regular, design: .serif))
                        .foregroundStyle(Color.textSecondary.opacity(0.8))
                }
            }

            Spacer()

            // Beverage Type Pill (e.g. "White Wine", "Vintage Champagne", "Craft Sake")
            Text(pairing.type.uppercased())
                .font(.system(size: 9, weight: .bold, design: .monospaced))
                .tracking(1)
                .foregroundStyle(Color.burgundyAccent)
                .padding(.horizontal, 9)
                .padding(.vertical, 4)
                .background(
                    Capsule()
                        .fill(Color.burgundyAccent.opacity(0.14))
                )
                .overlay(
                    Capsule()
                        .stroke(Color.burgundyAccent.opacity(0.35), lineWidth: 1)
                )
        }
    }

    /// Title, producer and vintage details
    private var beverageInfoView: some View {
        VStack(alignment: .leading, spacing: 5) {
            // Beverage Name
            Text(pairing.name)
                .font(.system(isCompact ? .subheadline : .title3, design: .serif, weight: .bold))
                .foregroundStyle(Color.textPrimary)
                .fixedSize(horizontal: false, vertical: true)

            // Producer & Vintage Details
            if pairing.producer != nil || pairing.vintage != nil {
                HStack(spacing: 8) {
                    if let producer = pairing.producer, !producer.isEmpty {
                        HStack(spacing: 4) {
                            Image(systemName: "building.columns")
                                .font(.system(size: 10))
                                .foregroundStyle(Color.textSecondary)

                            Text(producer)
                                .font(.system(size: 11.5, weight: .medium))
                                .foregroundStyle(Color.textSecondary)
                                .lineLimit(1)
                        }
                    }

                    if pairing.producer != nil && pairing.vintage != nil {
                        Text("•")
                            .font(.system(size: 10))
                            .foregroundStyle(Color.textSecondary.opacity(0.6))
                    }

                    if let vintage = pairing.vintage, !vintage.isEmpty {
                        HStack(spacing: 3) {
                            Image(systemName: "calendar")
                                .font(.system(size: 10))
                                .foregroundStyle(Color.champagneAccent.opacity(0.85))

                            Text(vintage)
                                .font(.system(size: 11, weight: .semibold, design: .monospaced))
                                .foregroundStyle(Color.champagneAccent.opacity(0.95))
                        }
                    }
                }
            }
        }
    }

    /// Sommelier temperature serving tag
    private func temperaturePill(_ temperature: String) -> some View {
        HStack(spacing: 5) {
            Image(systemName: "thermometer.snowflake")
                .font(.system(size: 10.5))
                .foregroundStyle(Color.burgundyAccent)

            Text("Температура подачи:")
                .font(.system(size: 10.5, weight: .regular))
                .foregroundStyle(Color.textSecondary)

            Text(temperature)
                .font(.system(size: 11, weight: .bold, design: .monospaced))
                .foregroundStyle(Color.textPrimary)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 4.5)
        .background(
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .fill(Color.obsidianCanvas.opacity(0.7))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .stroke(Color.glassBorder, lineWidth: 1)
        )
    }

    /// Tasting notes block describing sensory harmony
    private var tastingNotesView: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 5) {
                Image(systemName: "quote.opening")
                    .font(.system(size: 10))
                    .foregroundStyle(Color.burgundyAccent)

                Text("ГАРМОНИЯ ПЕЙРИНГА / TASTING ACCORD")
                    .font(.system(size: 8.5, weight: .bold, design: .monospaced))
                    .tracking(1.2)
                    .foregroundStyle(Color.burgundyAccent.opacity(0.9))
            }

            Text(pairing.notes)
                .font(.system(size: isCompact ? 12 : 13, weight: .regular, design: .serif))
                .italic()
                .foregroundStyle(Color.textPrimary.opacity(0.92))
                .lineSpacing(3.5)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .fill(Color.obsidianCanvas.opacity(0.55))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .stroke(Color.burgundyAccent.opacity(0.25), lineWidth: 1)
        )
    }

    // MARK: - Styling Helpers

    /// Background with card surface and subtle wine radiance
    private var cardBackground: some View {
        ZStack {
            Color.cardSurface

            // Subtle burgundy gradient wash
            LinearGradient(
                colors: [
                    Color.burgundyAccent.opacity(0.14),
                    Color.burgundyAccent.opacity(0.04),
                    Color.clear
                ],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )

            // Very subtle specular top sheen
            LinearGradient(
                colors: [
                    Color.white.opacity(0.04),
                    Color.clear
                ],
                startPoint: .top,
                endPoint: .center
            )
        }
    }

    /// Fine glass-burgundy perimeter border
    private var cardBorder: some View {
        RoundedRectangle(cornerRadius: 16, style: .continuous)
            .strokeBorder(
                LinearGradient(
                    colors: [
                        Color.burgundyAccent.opacity(0.65),
                        Color.burgundyAccent.opacity(0.30),
                        Color.glassBorder.opacity(0.8)
                    ],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                ),
                lineWidth: 1
            )
    }

    /// Beverage icon selection based on beverage type and semantics
    private var beverageIcon: String {
        let lowerType = pairing.type.lowercased()
        let lowerName = pairing.name.lowercased()

        if lowerType.contains("tea") || lowerType.contains("чай") || lowerType.contains("sake") || lowerType.contains("саке") {
            return "cup.and.saucer.fill"
        } else if lowerType.contains("cocktail") || lowerType.contains("mixology") || lowerType.contains("whisky") || lowerType.contains("old fashioned") || lowerName.contains("fashioned") {
            return "flame.fill"
        } else if lowerType.contains("amuse") || lowerType.contains("sweet") || lowerType.contains("digestif") {
            return "sparkles"
        } else {
            return "wineglass.fill"
        }
    }
}

// MARK: - Interactive Preview

#Preview("PairingCardView Showcase") {
    PairingCardPreviewContainer()
}

/// Interactive showcase preview presenting multiple sommelier pairings (White Burgundy, Champagne, Aged Barolo, Cocktail)
public struct PairingCardPreviewContainer: View {

    @State private var selectedIndex: Int = 0

    private let samplePairings: [(title: String, dishName: String, pairing: Pairing)] = [
        (
            title: "Белое Бургундское",
            dishName: "Pan-Seared Hokkaido Scallops",
            pairing: Pairing(
                id: "sample-pair-1",
                name: "2021 Sancerre 'Les Monts Damnés'",
                type: "White Wine",
                notes: "Кристальная кремневая минеральность и вибрирующая цитрусовая кислотность прорезают маслянистую карамельную сладость обжаренного гребешка, создавая идеальный баланс вкуса.",
                temperature: "9-11°C",
                producer: "Domaine François Cotat",
                vintage: "2021"
            )
        ),
        (
            title: "Винтажное Шампанское",
            dishName: "A5 Wagyu & Imperial Caviar Tartlet",
            pairing: Pairing(
                id: "sample-pair-2",
                name: "Champagne Blanc de Blancs Extra Brut",
                type: "Champagne",
                notes: "Мягкая микро-перляж и ноты поджаренной бриоши с красной смородиной гармонично связываются с солоновато-ореховым вкусом осетровой икры и нежностью мраморной говядины.",
                temperature: "8-10°C",
                producer: "Pierre Péters Cuvée de Réserve",
                vintage: "NV"
            )
        ),
        (
            title: "Выдержанное Бордо / Бароло",
            dishName: "A5 Kagoshima Ribeye & Smoked Bone Marrow",
            pairing: Pairing(
                id: "sample-pair-3",
                name: "Barolo Monprivato",
                type: "Aged Red Wine",
                notes: "Благородные тона смолы, сушеной розы и аристократичные танины Неббиоло моментально растворяют плотные мраморные жиры Вагю А5, оставляя глубокое вишневое послевкусие.",
                temperature: "16-18°C",
                producer: "Giuseppe Mascarello",
                vintage: "2016"
            )
        ),
        (
            title: "Авторский Коктейль",
            dishName: "Smoked Rosemary Oolong Old Fashioned",
            pairing: Pairing(
                id: "sample-pair-4",
                name: "Smoked Rosemary Oolong Old Fashioned",
                type: "Craft Cocktail",
                notes: "Древесный дым яблони, 12-летний японский виски и концентрированный чай Да Хун Пао создают многослойное теплое послевкусие с оттенками горького шоколада и кедра.",
                temperature: "4-6°C",
                producer: "Carte Blanche Signature Mixology",
                vintage: "Master Craft 2024"
            )
        )
    ]

    public init() {}

    public var body: some View {
        ZStack {
            Color.obsidianCanvas
                .ignoresSafeArea()

            ScrollView {
                VStack(alignment: .leading, spacing: 22) {
                    // Masthead
                    VStack(alignment: .leading, spacing: 4) {
                        Text("SOMMELIER CELLAR COMPONENT")
                            .font(.luxeMicroLabel)
                            .tracking(2)
                            .foregroundStyle(Color.burgundyAccent)

                        Text("PairingCardView")
                            .font(.luxeDishTitle)
                            .foregroundStyle(Color.textPrimary)

                        Text("Wine pairings, serving temperatures, cellar provenance and gastronomic harmonies.")
                            .font(.luxeSubheadline)
                            .foregroundStyle(Color.textSecondary)
                    }

                    // Interactive Selector Pills
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 8) {
                            ForEach(0..<samplePairings.count, id: \.self) { idx in
                                let sample = samplePairings[idx]
                                let isSelected = selectedIndex == idx

                                Button {
                                    withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
                                        selectedIndex = idx
                                    }
                                } label: {
                                    Text(sample.title)
                                        .font(.system(size: 11.5, weight: .semibold))
                                        .padding(.horizontal, 12)
                                        .padding(.vertical, 7)
                                        .background(
                                            Capsule()
                                                .fill(isSelected ? Color.burgundyAccent.opacity(0.25) : Color.cardSurface)
                                        )
                                        .overlay(
                                            Capsule()
                                                .stroke(isSelected ? Color.burgundyAccent : Color.glassBorder, lineWidth: 1)
                                        )
                                        .foregroundStyle(isSelected ? Color.textPrimary : Color.textSecondary)
                                }
                            }
                        }
                    }

                    // Associated Dish Context
                    HStack(spacing: 6) {
                        Image(systemName: "fork.knife")
                            .font(.system(size: 11))
                            .foregroundStyle(Color.champagneAccent)

                        Text("РЕКОМЕНДОВАНО К БЛЮДУ:")
                            .font(.system(size: 9.5, weight: .bold, design: .monospaced))
                            .foregroundStyle(Color.champagneAccent)

                        Text(samplePairings[selectedIndex].dishName)
                            .font(.system(size: 12, weight: .medium, design: .serif))
                            .foregroundStyle(Color.textPrimary)
                            .lineLimit(1)
                    }
                    .padding(.horizontal, 12)
                    .padding(.vertical, 7)
                    .background(Color.cardSurface)
                    .clipShape(RoundedRectangle(cornerRadius: 10))
                    .overlay(
                        RoundedRectangle(cornerRadius: 10)
                            .stroke(Color.glassBorder, lineWidth: 1)
                    )

                    // Active Pairing Card Preview
                    PairingCardView(pairing: samplePairings[selectedIndex].pairing)

                    // Compact Variant Preview
                    VStack(alignment: .leading, spacing: 8) {
                        Text("COMPACT VARIANT (FOR MODALS & LISTS)")
                            .font(.luxeMicroLabel)
                            .tracking(1.5)
                            .foregroundStyle(Color.textSecondary)

                        PairingCardView(
                            pairing: samplePairings[selectedIndex].pairing,
                            showHeadline: false,
                            isCompact: true
                        )
                    }
                    .padding(.top, 10)
                }
                .padding(20)
            }
        }
    }
}
