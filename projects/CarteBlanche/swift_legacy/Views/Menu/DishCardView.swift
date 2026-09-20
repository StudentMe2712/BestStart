//
//  DishCardView.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

/// Haute cuisine dish card formatted in Dark Luxe Editorial style with tactile GlassCard surfacing
public struct DishCardView: View {

    // MARK: - Properties

    public let dish: MenuItem
    public var isInTastingSet: Bool
    public var onSelect: () -> Void
    public var onToggleTastingSet: () -> Void

    // MARK: - Initializer

    public init(
        dish: MenuItem,
        isInTastingSet: Bool = false,
        onSelect: @escaping () -> Void = {},
        onToggleTastingSet: @escaping () -> Void = {}
    ) {
        self.dish = dish
        self.isInTastingSet = isInTastingSet
        self.onSelect = onSelect
        self.onToggleTastingSet = onToggleTastingSet
    }

    // MARK: - Body

    public var body: some View {
        GlassCard(
            cornerRadius: 18,
            strokeColor: isInTastingSet ? Color.champagneAccent.opacity(0.55) : Color.glassBorder,
            fillOpacity: isInTastingSet ? 0.92 : 0.85,
            padding: 18
        ) {
            VStack(alignment: .leading, spacing: 14) {
                // Top Row: Course Icon, Origin & Chef Selection Badge
                topMetadataRow

                // Dish Title
                Text(dish.name)
                    .font(.luxeDishTitle)
                    .foregroundStyle(Color.textPrimary)
                    .lineLimit(2)
                    .multilineTextAlignment(.leading)

                // Gastronomic Description
                Text(dish.description)
                    .font(.luxeSubheadline)
                    .foregroundStyle(Color.textSecondary)
                    .lineSpacing(3)
                    .lineLimit(3)

                // Dietary & Allergen Chips
                if !dish.dietaryTags.isEmpty {
                    dietaryTagsRow
                }

                // Sommelier Pairing Suggestion
                pairingRecommendationRow

                Divider()
                    .background(Color.glassBorder.opacity(0.8))

                // Bottom Action Bar: Price & Quick Tasting Set Toggle
                bottomActionBar
            }
        }
        .contentShape(RoundedRectangle(cornerRadius: 18))
        .onTapGesture {
            onSelect()
        }
        .animation(.spring(response: 0.35, dampingFraction: 0.7), value: isInTastingSet)
    }

    // MARK: - Subviews

    /// Top metadata line containing course iconography, terroir origin, and chef badge
    private var topMetadataRow: some View {
        HStack(alignment: .center, spacing: 8) {
            HStack(spacing: 5) {
                Image(systemName: dish.course.systemImage)
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(Color.champagneAccent)

                Text(dish.course.rawValue.uppercased())
                    .font(.system(size: 10, weight: .bold, design: .monospaced))
                    .tracking(1.2)
                    .foregroundStyle(Color.textSecondary)

                if let origin = dish.origin {
                    Text("•")
                        .foregroundStyle(Color.glassBorder)

                    Image(systemName: "mappin.circle.fill")
                        .font(.system(size: 10))
                        .foregroundStyle(Color.textSecondary.opacity(0.7))

                    Text(origin)
                        .font(.system(size: 11, weight: .medium))
                        .foregroundStyle(Color.textSecondary)
                        .lineLimit(1)
                }
            }

            Spacer(minLength: 6)

            if dish.isChefSelection {
                HStack(spacing: 4) {
                    Image(systemName: "crown.fill")
                        .font(.system(size: 9))
                    Text("CHEF'S SELECTION")
                        .font(.system(size: 9, weight: .bold, design: .monospaced))
                        .tracking(1)
                }
                .padding(.horizontal, 8)
                .padding(.vertical, 4)
                .background(Color.champagneAccent.opacity(0.12))
                .clipShape(Capsule())
                .overlay(
                    Capsule()
                        .stroke(Color.champagneAccent.opacity(0.35), lineWidth: 1)
                )
                .foregroundStyle(Color.champagneAccent)
            }
        }
    }

    /// Dietary chips horizontal list
    private var dietaryTagsRow: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 6) {
                ForEach(dish.dietaryTags) { tag in
                    DietaryBadgeView(tag: tag)
                }
            }
        }
    }

    /// Sommelier wine or mixology pairing snippet
    private var pairingRecommendationRow: some View {
        HStack(spacing: 8) {
            Image(systemName: "wineglass.fill")
                .font(.system(size: 11))
                .foregroundStyle(Color.burgundyAccent)

            Text(dish.pairing.type.uppercased())
                .font(.system(size: 9, weight: .bold, design: .monospaced))
                .tracking(1)
                .foregroundStyle(Color.burgundyAccent)

            Text("•")
                .foregroundStyle(Color.glassBorder)

            Text(dish.pairing.name)
                .font(.system(size: 12, weight: .medium, design: .serif))
                .foregroundStyle(Color.textPrimary.opacity(0.9))
                .lineLimit(1)

            Spacer()
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 6)
        .background(Color.burgundyAccent.opacity(0.08))
        .clipShape(RoundedRectangle(cornerRadius: 8))
        .overlay(
            RoundedRectangle(cornerRadius: 8)
                .stroke(Color.burgundyAccent.opacity(0.22), lineWidth: 1)
        )
    }

    /// Bottom bar with price, rating, and add to tasting set button
    private var bottomActionBar: some View {
        HStack(alignment: .center) {
            VStack(alignment: .leading, spacing: 2) {
                Text("COURSE PRICE")
                    .font(.system(size: 8.5, weight: .bold, design: .monospaced))
                    .tracking(1.2)
                    .foregroundStyle(Color.textSecondary)

                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    Text(dish.formattedPrice)
                        .font(.luxePrice)
                        .foregroundStyle(Color.champagneAccent)

                    if let rating = dish.rating {
                        HStack(spacing: 3) {
                            Image(systemName: "star.fill")
                                .font(.system(size: 9.5))
                                .foregroundStyle(Color.champagneAccent)
                            Text(String(format: "%.1f", rating))
                                .font(.system(size: 11, weight: .bold, design: .monospaced))
                                .foregroundStyle(Color.textSecondary)
                        }
                    }
                }
            }

            Spacer()

            // Quick Tasting Set Toggle Button
            Button {
                withAnimation(.spring(response: 0.35, dampingFraction: 0.65)) {
                    onToggleTastingSet()
                }
            } label: {
                ZStack {
                    Circle()
                        .fill(
                            isInTastingSet ?
                            LinearGradient(
                                colors: [Color(hex: 0xF7D89C), Color(hex: 0xE5A962), Color(hex: 0xBF823D)],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            ) :
                            LinearGradient(
                                colors: [Color.cardSurface, Color.cardSurface.opacity(0.7)],
                                startPoint: .top,
                                endPoint: .bottom
                            )
                        )
                        .frame(width: 42, height: 42)
                        .overlay(
                            Circle()
                                .stroke(
                                    isInTastingSet ? Color.champagneAccent : Color.glassBorder,
                                    lineWidth: 1.2
                                )
                        )
                        .shadow(
                            color: isInTastingSet ? Color.champagneAccent.opacity(0.4) : Color.black.opacity(0.3),
                            radius: isInTastingSet ? 8 : 4,
                            y: 2
                        )

                    Image(systemName: isInTastingSet ? "checkmark" : "plus")
                        .font(.system(size: 15, weight: .bold))
                        .foregroundStyle(isInTastingSet ? Color.obsidianCanvas : Color.champagneAccent)
                        .contentTransition(.symbolEffect(.replace))
                }
            }
            .buttonStyle(PlainButtonStyle())
            .sensoryFeedback(.selection, trigger: isInTastingSet)
        }
    }
}

// MARK: - Interactive Preview

#Preview("Dish Card Comparison (Standard vs In Set)") {
    DishCardPreviewContainer()
}

private struct DishCardPreviewContainer: View {
    @State private var item1InSet: Bool = false
    @State private var item2InSet: Bool = true

    private let sample1 = MockDataService.defaultSampleItem
    private let sample2 = MockDataService.shared.items.first { $0.course == .main } ?? MockDataService.defaultSampleItem

    var body: some View {
        ZStack {
            Color.obsidianCanvas
                .ignoresSafeArea()

            ScrollView {
                VStack(spacing: 20) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("DARK LUXE EDITORIAL")
                            .font(.luxeMicroLabel)
                            .tracking(2)
                            .foregroundStyle(Color.champagneAccent)

                        Text("Dish Cards Preview")
                            .font(.luxeLargeTitle)
                            .foregroundStyle(Color.textPrimary)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.horizontal, 20)
                    .padding(.top, 16)

                    // Standard Card
                    VStack(alignment: .leading, spacing: 6) {
                        Text("STANDARD STATE (TAP TO TOGGLE)")
                            .font(.luxeMicroLabel)
                            .foregroundStyle(Color.textSecondary)
                            .padding(.horizontal, 20)

                        DishCardView(
                            dish: sample1,
                            isInTastingSet: item1InSet,
                            onSelect: {
                                item1InSet.toggle()
                            },
                            onToggleTastingSet: {
                                item1InSet.toggle()
                            }
                        )
                        .padding(.horizontal, 20)
                    }

                    // Added to Set Card
                    VStack(alignment: .leading, spacing: 6) {
                        Text("ADDED TO TASTING SET STATE")
                            .font(.luxeMicroLabel)
                            .foregroundStyle(Color.champagneAccent)
                            .padding(.horizontal, 20)

                        DishCardView(
                            dish: sample2,
                            isInTastingSet: item2InSet,
                            onSelect: {
                                item2InSet.toggle()
                            },
                            onToggleTastingSet: {
                                item2InSet.toggle()
                            }
                        )
                        .padding(.horizontal, 20)
                    }
                }
                .padding(.bottom, 30)
            }
        }
    }
}
