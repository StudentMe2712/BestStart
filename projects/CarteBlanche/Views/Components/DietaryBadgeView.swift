//
//  DietaryBadgeView.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

// MARK: - DietaryTag Color Palette Extension

extension DietaryTag {
    /// Semantic accent color mapped to Dark Luxe design tokens
    public var accentColor: Color {
        switch self {
        case .vegan, .vegetarian:
            return .sageGreen
        case .glutenFree, .nutFree:
            return .champagneAccent
        case .halal:
            return .burgundyAccent
        case .dairyFree:
            return .slateBlue
        case .pescatarian:
            return .oceanTeal
        }
    }
}

// MARK: - Dietary Badge View

/// Compact chip displaying dietary and allergen tags with Dark Luxe styling and interactive filter support
public struct DietaryBadgeView: View {
    public let tag: DietaryTag
    public var isInteractive: Bool
    public var isSelected: Bool
    public var action: (() -> Void)?

    /// Static display badge initializer (for dish cards and detail sheets)
    public init(tag: DietaryTag) {
        self.tag = tag
        self.isInteractive = false
        self.isSelected = false
        self.action = nil
    }

    /// Interactive filter chip initializer with action callback
    public init(
        tag: DietaryTag,
        isSelected: Bool,
        action: (() -> Void)? = nil
    ) {
        self.tag = tag
        self.isInteractive = true
        self.isSelected = isSelected
        self.action = action
    }

    public var body: some View {
        if isInteractive {
            Button(action: {
                action?()
            }) {
                badgeContent
            }
            .buttonStyle(PlainButtonStyle())
            .sensoryFeedback(.selection, trigger: isSelected)
        } else {
            badgeContent
        }
    }

    // MARK: - Content Presentation

    private var badgeContent: some View {
        HStack(spacing: 5) {
            Image(systemName: tag.icon)
                .font(.system(size: isInteractive ? 11 : 9.5, weight: .semibold))

            Text(tag.rawValue)
                .font(.caption2.weight(.medium))

            if isInteractive && isSelected {
                Image(systemName: "checkmark")
                    .font(.system(size: 8.5, weight: .bold))
                    .foregroundStyle(tag.accentColor)
                    .transition(.scale.combined(with: .opacity))
            }
        }
        .padding(.horizontal, isInteractive ? 11 : 8)
        .padding(.vertical, isInteractive ? 6 : 3.5)
        .background(backgroundView)
        .overlay(overlayBorder)
        .foregroundStyle(foregroundColor)
        .animation(.spring(response: 0.3, dampingFraction: 0.7), value: isSelected)
    }

    // MARK: - Dynamic Styling

    @ViewBuilder
    private var backgroundView: some View {
        if isInteractive {
            if isSelected {
                Capsule()
                    .fill(tag.accentColor.opacity(0.20))
            } else {
                Capsule()
                    .fill(Color.cardSurface.opacity(0.9))
            }
        } else {
            // Static badge
            Capsule()
                .fill(tag.accentColor.opacity(0.12))
        }
    }

    private var overlayBorder: some View {
        Capsule()
            .stroke(
                isInteractive ?
                (isSelected ? tag.accentColor : Color.glassBorder) :
                tag.accentColor.opacity(0.35),
                lineWidth: 1
            )
    }

    private var foregroundColor: Color {
        if isInteractive {
            return isSelected ? tag.accentColor : Color.textSecondary
        } else {
            return tag.accentColor
        }
    }
}

// MARK: - Interactive Preview

#Preview("Dietary Badges Showcase & Filter Bar") {
    DietaryBadgePreviewView()
}

/// Standalone showcase view for dietary badges and interactive allergen filtering
public struct DietaryBadgePreviewView: View {
    @State private var selectedTags: Set<DietaryTag> = [.glutenFree, .pescatarian]

    public init() {}

    public var body: some View {
        ZStack {
            Color.obsidianCanvas
                .ignoresSafeArea()

            ScrollView {
                VStack(alignment: .leading, spacing: 32) {
                    // Header
                    VStack(alignment: .leading, spacing: 6) {
                        Text("DIETARY & ALLERGEN SYSTEM")
                            .font(.luxeMicroLabel)
                            .tracking(2)
                            .foregroundStyle(Color.champagneAccent)

                        Text("DietaryBadgeView")
                            .font(.luxeDishTitle)
                            .foregroundStyle(Color.textPrimary)

                        Text("Compact Michelin-styled tags with color semantics and filter toggles.")
                            .font(.luxeSubheadline)
                            .foregroundStyle(Color.textSecondary)
                    }

                    // Section 1: Static Dish Badges Row (as shown on menu cards)
                    VStack(alignment: .leading, spacing: 12) {
                        Text("STATIC BADGES IN DISH CARD CONTEXT")
                            .font(.luxeMicroLabel)
                            .tracking(1.5)
                            .foregroundStyle(Color.textSecondary)

                        VStack(alignment: .leading, spacing: 10) {
                            Text("Pan-Seared Hokkaido Scallops")
                                .font(.system(.subheadline, design: .serif, weight: .bold))
                                .foregroundStyle(Color.textPrimary)

                            HStack(spacing: 8) {
                                DietaryBadgeView(tag: .glutenFree)
                                DietaryBadgeView(tag: .pescatarian)
                                DietaryBadgeView(tag: .nutFree)
                            }
                        }
                        .padding(16)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .glassCard(cornerRadius: 14)

                        VStack(alignment: .leading, spacing: 10) {
                            Text("Salt-Baked Heirloom Beetroot")
                                .font(.system(.subheadline, design: .serif, weight: .bold))
                                .foregroundStyle(Color.textPrimary)

                            HStack(spacing: 8) {
                                DietaryBadgeView(tag: .vegan)
                                DietaryBadgeView(tag: .vegetarian)
                                DietaryBadgeView(tag: .dairyFree)
                            }
                        }
                        .padding(16)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .glassCard(cornerRadius: 14)
                    }

                    // Section 2: Interactive Filter Bar (Horizontally scrollable)
                    VStack(alignment: .leading, spacing: 12) {
                        HStack {
                            Text("INTERACTIVE FILTER STRIP (HORIZONTAL)")
                                .font(.luxeMicroLabel)
                                .tracking(1.5)
                                .foregroundStyle(Color.textSecondary)

                            Spacer()

                            if !selectedTags.isEmpty {
                                Button("Clear (\(selectedTags.count))") {
                                    withAnimation(.spring(duration: 0.25)) {
                                        selectedTags.removeAll()
                                    }
                                }
                                .font(.system(size: 11, weight: .semibold, design: .monospaced))
                                .foregroundStyle(Color.champagneAccent)
                            }
                        }

                        ScrollView(.horizontal, showsIndicators: false) {
                            HStack(spacing: 8) {
                                ForEach(DietaryTag.allCases) { tag in
                                    let isSelected = selectedTags.contains(tag)
                                    DietaryBadgeView(tag: tag, isSelected: isSelected) {
                                        withAnimation(.spring(response: 0.3, dampingFraction: 0.7)) {
                                            if isSelected {
                                                selectedTags.remove(tag)
                                            } else {
                                                selectedTags.insert(tag)
                                            }
                                        }
                                    }
                                }
                            }
                            .padding(.vertical, 4)
                        }
                    }

                    // Section 3: All Available Tags Grid Showcase
                    VStack(alignment: .leading, spacing: 12) {
                        Text("ALL 7 GASTRONOMIC DIETARY TAGS")
                            .font(.luxeMicroLabel)
                            .tracking(1.5)
                            .foregroundStyle(Color.textSecondary)

                        LazyVGrid(columns: [GridItem(.adaptive(minimum: 140), spacing: 10)], spacing: 10) {
                            ForEach(DietaryTag.allCases) { tag in
                                let isSelected = selectedTags.contains(tag)
                                DietaryBadgeView(tag: tag, isSelected: isSelected) {
                                    withAnimation(.spring(response: 0.3, dampingFraction: 0.7)) {
                                        if isSelected {
                                            selectedTags.remove(tag)
                                        } else {
                                            selectedTags.insert(tag)
                                        }
                                    }
                                }
                            }
                        }
                        .padding(14)
                        .glassCard(cornerRadius: 14, fillOpacity: 0.6)
                    }

                    // Section 4: Live Filter Diagnostics
                    VStack(alignment: .leading, spacing: 8) {
                        Text("ACTIVE FILTER STATE")
                            .font(.luxeMicroLabel)
                            .tracking(1.5)
                            .foregroundStyle(Color.champagneAccent)

                        if selectedTags.isEmpty {
                            Text("No dietary restrictions active — displaying full 12-dish Carte Blanche tasting menu.")
                                .font(.system(size: 12))
                                .foregroundStyle(Color.textSecondary)
                        } else {
                            Text("Filtering dishes satisfying all: \(selectedTags.map { $0.rawValue }.sorted().joined(separator: ", "))")
                                .font(.system(size: 12, design: .monospaced))
                                .foregroundStyle(Color.textPrimary)
                        }
                    }
                    .padding(14)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(Color.cardSurface)
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                    .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.glassBorder, lineWidth: 1))
                }
                .padding(20)
            }
        }
    }
}
