//
//  DishDetailSheet.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

/// Premium Haute Cuisine modal detail sheet featuring terroir provenance, sensory radar architecture, and sommelier pairing
public struct DishDetailSheet: View {

    // MARK: - Properties

    public let dish: MenuItem
    public var isInTastingSet: Bool
    public var onToggleTastingSet: () -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var isBookmarkedInSet: Bool = false
    @State private var radarLanguage: AxisLanguage = .dual

    // MARK: - Initializer

    public init(
        dish: MenuItem,
        isInTastingSet: Bool = false,
        onToggleTastingSet: @escaping () -> Void = {}
    ) {
        self.dish = dish
        self.isInTastingSet = isInTastingSet
        self.onToggleTastingSet = onToggleTastingSet
    }

    // MARK: - Body

    public var body: some View {
        ZStack(alignment: .bottom) {
            // Obsidian Canvas Background
            Color.obsidianCanvas
                .ignoresSafeArea()

            // Subtle Champagne Ambient Radiance from top
            RadialGradient(
                colors: [
                    Color.champagneAccent.opacity(0.12),
                    Color.champagneAccent.opacity(0.03),
                    Color.clear
                ],
                center: .top,
                startRadius: 20,
                endRadius: 380
            )
            .ignoresSafeArea()

            // Scrollable Haute Cuisine Story & Details
            ScrollView {
                VStack(alignment: .leading, spacing: 22) {
                    // 1. Top Navigation & Action Controls Bar
                    topBarView

                    // 2. Course Category, Terroir Origin & Chef's Badge
                    dishMetadataRow

                    // 3. Main Dish Title & Luxury Monospaced Price
                    titleAndPriceView

                    // 4. Dietary & Allergen Badges
                    if !dish.dietaryTags.isEmpty {
                        dietaryTagsRow
                    }

                    Divider()
                        .background(Color.glassBorder)

                    // 5. Section: The Dish / О блюде (Composition Description)
                    theDishSection

                    // 6. Section: Culinary Craftsmanship & Provenance (Chef's Story in GlassCard)
                    culinaryCraftsmanshipSection

                    // 7. Section: Flavor Architecture (Interactive 5-Axis Radar Chart)
                    flavorArchitectureSection

                    // 8. Section: Sommelier's Selection (Rich Burgundy Pairing Card)
                    sommelierSelectionSection

                    // Bottom clearance for the floating action CTA button
                    Spacer(minLength: 88)
                }
                .padding(.horizontal, 20)
                .padding(.top, 16)
            }
            .scrollDismissesKeyboard(.interactively)

            // Fixed Bottom Action CTA: Add / Remove from Tasting Set
            bottomFloatingActionBar
        }
        .presentationDetents([.medium, .large])
        .presentationDragIndicator(.visible)
        .presentationBackground(Color.obsidianCanvas)
        .onAppear {
            isBookmarkedInSet = isInTastingSet
        }
        .onChange(of: isInTastingSet) { _, newValue in
            isBookmarkedInSet = newValue
        }
    }

    // MARK: - Subviews: Navigation & Header

    /// Top frosted navigation bar with dismiss and quick bookmark/tasting star buttons
    private var topBarView: some View {
        HStack(alignment: .center) {
            // Dismiss button in frosted circular glass
            Button {
                dismiss()
            } label: {
                Image(systemName: "xmark")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundStyle(Color.textPrimary)
                    .frame(width: 36, height: 36)
                    .background(Color.cardSurface.opacity(0.85))
                    .clipShape(Circle())
                    .overlay(
                        Circle()
                            .stroke(Color.glassBorder, lineWidth: 1)
                    )
            }
            .buttonStyle(PlainButtonStyle())

            Spacer()

            // Quick Tasting Set toggle (Star / Checkmark)
            Button {
                withAnimation(.spring(response: 0.35, dampingFraction: 0.7)) {
                    isBookmarkedInSet.toggle()
                    onToggleTastingSet()
                }
            } label: {
                HStack(spacing: 6) {
                    Image(systemName: isBookmarkedInSet ? "star.fill" : "star")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundStyle(isBookmarkedInSet ? Color.champagneAccent : Color.textSecondary)

                    Text(isBookmarkedInSet ? "В сете" : "В сет")
                        .font(.system(size: 11.5, weight: .bold, design: .serif))
                        .foregroundStyle(isBookmarkedInSet ? Color.champagneAccent : Color.textSecondary)
                }
                .padding(.horizontal, 12)
                .padding(.vertical, 7)
                .background(
                    Capsule()
                        .fill(isBookmarkedInSet ? Color.champagneAccent.opacity(0.18) : Color.cardSurface.opacity(0.85))
                )
                .overlay(
                    Capsule()
                        .stroke(isBookmarkedInSet ? Color.champagneAccent.opacity(0.6) : Color.glassBorder, lineWidth: 1)
                )
            }
            .buttonStyle(PlainButtonStyle())
        }
    }

    /// Course badge, terroir origin and Chef's Selection badge
    private var dishMetadataRow: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 8) {
                // Course Category Pill
                HStack(spacing: 5) {
                    Image(systemName: dish.course.systemImage)
                        .font(.system(size: 10, weight: .semibold))
                        .foregroundStyle(Color.champagneAccent)

                    Text(dish.course.rawValue.uppercased())
                        .font(.luxeMicroLabel)
                        .tracking(1.4)
                        .foregroundStyle(Color.champagneAccent)
                }
                .padding(.horizontal, 9)
                .padding(.vertical, 4.5)
                .background(Color.champagneAccent.opacity(0.12))
                .clipShape(Capsule())
                .overlay(
                    Capsule()
                        .stroke(Color.champagneAccent.opacity(0.3), lineWidth: 1)
                )

                // Terroir Origin indicator
                if let origin = dish.origin, !origin.isEmpty {
                    HStack(spacing: 4) {
                        Image(systemName: "mappin.and.ellipse")
                            .font(.system(size: 10))
                            .foregroundStyle(Color.textSecondary)

                        Text(origin)
                            .font(.system(size: 11, weight: .medium))
                            .foregroundStyle(Color.textSecondary)
                            .lineLimit(1)
                    }
                }

                Spacer()
            }

            // Chef's Selection Golden Badge (if applicable)
            if dish.isChefSelection {
                HStack(spacing: 5) {
                    Image(systemName: "crown.fill")
                        .font(.system(size: 10))
                        .foregroundStyle(Color.champagneAccent)

                    Text("CHEF'S SELECTION • ВЫБОР ШЕФ-ПОВАРА")
                        .font(.system(size: 9, weight: .bold, design: .monospaced))
                        .tracking(1.2)
                        .foregroundStyle(Color.champagneAccent)
                }
                .padding(.horizontal, 10)
                .padding(.vertical, 4)
                .background(
                    LinearGradient(
                        colors: [
                            Color.champagneAccent.opacity(0.18),
                            Color.champagneAccent.opacity(0.06)
                        ],
                        startPoint: .leading,
                        endPoint: .trailing
                    )
                )
                .clipShape(Capsule())
                .overlay(
                    Capsule()
                        .stroke(Color.champagneAccent.opacity(0.4), lineWidth: 1)
                )
            }
        }
    }

    /// Title with Michelin serif typography and bold luxury price in monospaced format
    private var titleAndPriceView: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(dish.name)
                .font(.luxeLargeTitle)
                .foregroundStyle(Color.textPrimary)
                .fixedSize(horizontal: false, vertical: true)

            HStack(alignment: .firstTextBaseline) {
                Text(dish.formattedPrice)
                    .font(.system(size: 26, weight: .bold, design: .serif))
                    .monospacedDigit()
                    .foregroundStyle(Color.champagneAccent)

                Text("haute carte")
                    .font(.system(size: 11, weight: .medium, design: .monospaced))
                    .foregroundStyle(Color.textSecondary.opacity(0.7))

                Spacer()

                if let rating = dish.rating {
                    HStack(spacing: 3) {
                        Image(systemName: "star.fill")
                            .font(.system(size: 11))
                            .foregroundStyle(Color.champagneAccent)

                        Text(String(format: "%.1f", rating))
                            .font(.system(size: 12, weight: .bold, design: .monospaced))
                            .foregroundStyle(Color.textPrimary)
                    }
                    .padding(.horizontal, 8)
                    .padding(.vertical, 4)
                    .background(Color.cardSurface)
                    .clipShape(Capsule())
                    .overlay(Capsule().stroke(Color.glassBorder, lineWidth: 1))
                }
            }
        }
    }

    /// Horizontal flow of dietary & allergen badges
    private var dietaryTagsRow: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 7) {
                ForEach(dish.dietaryTags) { tag in
                    DietaryBadgeView(tag: tag)
                }
            }
        }
    }

    // MARK: - Subviews: Gastronomic Sections

    /// Gastronomic composition description
    private var theDishSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 6) {
                Image(systemName: "fork.knife")
                    .font(.system(size: 11))
                    .foregroundStyle(Color.champagneAccent)

                Text("THE DISH / О БЛЮДЕ")
                    .font(.luxeMicroLabel)
                    .tracking(1.6)
                    .foregroundStyle(Color.champagneAccent)
            }

            Text(dish.description)
                .font(.luxeBody)
                .foregroundStyle(Color.textSecondary)
                .lineSpacing(4)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    /// Culinary narrative & culinary story inside Dark Luxe GlassCard
    private var culinaryCraftsmanshipSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 6) {
                Image(systemName: "flame.fill")
                    .font(.system(size: 11))
                    .foregroundStyle(Color.champagneAccent)

                Text("CULINARY CRAFTSMANSHIP & PROVENANCE / ИСТОРИЯ И ТЕХНИКА")
                    .font(.luxeMicroLabel)
                    .tracking(1.4)
                    .foregroundStyle(Color.champagneAccent)
            }

            GlassCard(cornerRadius: 16, strokeColor: Color.glassBorder, fillOpacity: 0.85, padding: 18) {
                VStack(alignment: .leading, spacing: 12) {
                    HStack(spacing: 6) {
                        Image(systemName: "quote.opening")
                            .font(.system(size: 14))
                            .foregroundStyle(Color.champagneAccent.opacity(0.8))

                        Text("GASTRONOMIC INTENT")
                            .font(.system(size: 9, weight: .bold, design: .monospaced))
                            .tracking(1.2)
                            .foregroundStyle(Color.champagneAccent.opacity(0.85))
                    }

                    Text(dish.culinaryStory)
                        .font(.system(size: 14, weight: .regular, design: .serif))
                        .foregroundStyle(Color.textPrimary.opacity(0.95))
                        .lineSpacing(5)
                        .fixedSize(horizontal: false, vertical: true)

                    Divider()
                        .background(Color.glassBorder.opacity(0.6))

                    HStack {
                        Text("— Carte Blanche Executive Culinary Atelier")
                            .font(.system(size: 11, weight: .medium, design: .serif))
                            .italic()
                            .foregroundStyle(Color.champagneAccent.opacity(0.85))

                        Spacer()

                        Image(systemName: "sparkles")
                            .font(.system(size: 11))
                            .foregroundStyle(Color.champagneAccent.opacity(0.6))
                    }
                }
            }
        }
    }

    /// Full-size interactive sensory flavor radar chart
    private var flavorArchitectureSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                HStack(spacing: 6) {
                    Image(systemName: "dial.low.fill")
                        .font(.system(size: 11))
                        .foregroundStyle(Color.champagneAccent)

                    Text("FLAVOR ARCHITECTURE / АРХИТЕКТУРА ВКУСА")
                        .font(.luxeMicroLabel)
                        .tracking(1.4)
                        .foregroundStyle(Color.champagneAccent)
                }

                Spacer()

                // Language toggle (RU / EN / DUAL)
                Picker("Language", selection: $radarLanguage) {
                    ForEach(AxisLanguage.allCases) { lang in
                        Text(lang.rawValue).tag(lang)
                    }
                }
                .pickerStyle(.segmented)
                .frame(maxWidth: 130)
            }

            GlassCard(cornerRadius: 18, strokeColor: Color.glassBorder, fillOpacity: 0.88, padding: 16) {
                VStack(spacing: 6) {
                    FlavorWheelView(
                        profile: dish.flavorProfile,
                        title: nil,
                        language: radarLanguage,
                        showValueLabels: true,
                        chartDiameter: 210
                    )
                    .frame(maxWidth: .infinity)

                    Text("5-осевой сенсорный профиль вкусового баланса блюда")
                        .font(.system(size: 10.5, weight: .regular, design: .serif))
                        .foregroundStyle(Color.textSecondary.opacity(0.75))
                        .padding(.top, 4)
                }
            }
        }
    }

    /// Sommelier Pairing Card
    private var sommelierSelectionSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 6) {
                Image(systemName: "wineglass.fill")
                    .font(.system(size: 11))
                    .foregroundStyle(Color.burgundyAccent)

                Text("SOMMELIER'S SELECTION / РЕКОМЕНДАЦИЯ СОМЕЛЬЕ")
                    .font(.luxeMicroLabel)
                    .tracking(1.4)
                    .foregroundStyle(Color.burgundyAccent)
            }

            PairingCardView(pairing: dish.pairing, showHeadline: true)
        }
    }

    // MARK: - Subviews: Floating Bottom Action Button

    /// Fixed bottom bar offering tactile tasting set toggle with luxurious gold gradient
    private var bottomFloatingActionBar: some View {
        VStack(spacing: 0) {
            // Subtle top fade out gradient
            LinearGradient(
                colors: [
                    Color.obsidianCanvas.opacity(0.0),
                    Color.obsidianCanvas.opacity(0.95),
                    Color.obsidianCanvas
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .frame(height: 24)
            .allowsHitTesting(false)

            HStack {
                Button {
                    withAnimation(.spring(response: 0.35, dampingFraction: 0.7)) {
                        isBookmarkedInSet.toggle()
                        onToggleTastingSet()
                    }
                } label: {
                    HStack(spacing: 10) {
                        Image(systemName: isBookmarkedInSet ? "checkmark.circle.fill" : "plus.circle.fill")
                            .font(.system(size: 16, weight: .bold))

                        Text(isBookmarkedInSet ? "В дегустационном сете • In Tasting Set" : "Добавить в дегустационный сет • Add to Tasting Set")
                            .font(.system(size: 13.5, weight: .bold, design: .serif))
                    }
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 14)
                    .background(
                        isBookmarkedInSet ?
                        LinearGradient(colors: [Color.cardSurface, Color.cardSurface.opacity(0.95)], startPoint: .top, endPoint: .bottom) :
                        Color.goldGradient
                    )
                    .foregroundStyle(isBookmarkedInSet ? Color.champagneAccent : Color.black)
                    .clipShape(Capsule())
                    .overlay(
                        Capsule()
                            .stroke(isBookmarkedInSet ? Color.champagneAccent.opacity(0.7) : Color.clear, lineWidth: 1.5)
                    )
                    .shadow(
                        color: isBookmarkedInSet ? Color.clear : Color.champagneAccent.opacity(0.35),
                        radius: 12,
                        y: 4
                    )
                }
                .buttonStyle(PlainButtonStyle())
            }
            .padding(.horizontal, 20)
            .padding(.bottom, 18)
            .background(Color.obsidianCanvas)
        }
    }
}

// MARK: - Interactive Preview

#Preview("DishDetailSheet Interactive Live Preview") {
    DishDetailSheetPreviewContainer()
}

/// Standalone showcase container allowing live switching between all 12 gourmet dishes
public struct DishDetailSheetPreviewContainer: View {

    @State private var service = MockDataService.shared
    @State private var selectedIndex: Int = 0
    @State private var inTastingSetIds: Set<String> = ["prelude-01"]

    public init() {}

    private var currentDish: MenuItem {
        let items = service.allItems
        if items.indices.contains(selectedIndex) {
            return items[selectedIndex]
        }
        return MockDataService.defaultSampleItem
    }

    public var body: some View {
        ZStack {
            Color.obsidianCanvas
                .ignoresSafeArea()

            VStack(spacing: 0) {
                // Quick Picker for all 12 dishes
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 8) {
                        ForEach(0..<service.allItems.count, id: \.self) { idx in
                            let item = service.allItems[idx]
                            let isSelected = selectedIndex == idx

                            Button {
                                withAnimation(.easeInOut(duration: 0.2)) {
                                    selectedIndex = idx
                                }
                            } label: {
                                Text(item.name)
                                    .font(.system(size: 11, weight: .medium, design: .serif))
                                    .padding(.horizontal, 10)
                                    .padding(.vertical, 6)
                                    .background(
                                        Capsule()
                                            .fill(isSelected ? Color.champagneAccent.opacity(0.25) : Color.cardSurface)
                                    )
                                    .overlay(
                                        Capsule()
                                            .stroke(isSelected ? Color.champagneAccent : Color.glassBorder, lineWidth: 1)
                                    )
                                    .foregroundStyle(isSelected ? Color.textPrimary : Color.textSecondary)
                            }
                        }
                    }
                    .padding(.horizontal, 16)
                    .padding(.vertical, 10)
                }
                .background(Color.cardSurface)

                // The Actual DishDetailSheet
                DishDetailSheet(
                    dish: currentDish,
                    isInTastingSet: inTastingSetIds.contains(currentDish.id),
                    onToggleTastingSet: {
                        if inTastingSetIds.contains(currentDish.id) {
                            inTastingSetIds.remove(currentDish.id)
                        } else {
                            inTastingSetIds.insert(currentDish.id)
                        }
                    }
                )
            }
        }
    }
}
