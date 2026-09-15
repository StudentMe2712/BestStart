//
//  MenuHomeView.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

/// Fullscreen Haute Cuisine Menu & Tasting Guide home view in Dark Luxe Editorial style
public struct MenuHomeView: View {

    // MARK: - State Management

    @State public var viewModel: MenuViewModel

    // MARK: - Initializer

    public init(viewModel: MenuViewModel = MenuViewModel()) {
        _viewModel = State(initialValue: viewModel)
    }

    // MARK: - Body

    public var body: some View {
        ZStack(alignment: .bottom) {
            // Obsidian Canvas Background
            Color.obsidianCanvas
                .ignoresSafeArea()

            // Subtle Ambient Atmospheric Radiance
            RadialGradient(
                colors: [
                    Color.champagneAccent.opacity(0.06),
                    Color.clear
                ],
                center: .topTrailing,
                startRadius: 50,
                endRadius: 500
            )
            .ignoresSafeArea()

            // Primary Content Stream
            ScrollView {
                VStack(spacing: 20) {
                    // Top Haute Cuisine Header with Zone Menu
                    topHeaderView

                    // Frosted Glass Search Bar
                    searchBarView

                    // Category Navigation Ribbon
                    CategoryPillsView(selectedCategory: $viewModel.selectedCategory)

                    // Quick Dietary & Allergen Filters Strip
                    dietaryFilterRibbonView

                    // Results Counter & Active Filter Indicators
                    resultsStatusBarView

                    // Dish Cards List or Empty State
                    if viewModel.filteredDishes.isEmpty {
                        emptyStateView
                    } else {
                        dishCardsStreamView
                    }
                }
                .padding(.top, 8)
                .padding(.bottom, viewModel.tastingSetCount > 0 ? 110 : 36)
            }
            .scrollDismissesKeyboard(.interactively)

            // Floating Tasting Set Order Bar (Bottom Anchor)
            if viewModel.tastingSetCount > 0 {
                floatingTastingSetBar
                    .transition(.move(edge: .bottom).combined(with: .opacity))
            }
        }
        .animation(.spring(response: 0.35, dampingFraction: 0.75), value: viewModel.tastingSetCount)
        // Dish Detail Sheet
        .sheet(item: $viewModel.selectedDishForDetail) { dish in
            DishDetailSheet(
                dish: dish,
                isInTastingSet: viewModel.isInTastingSet(dish: dish),
                onToggleTastingSet: {
                    viewModel.toggleTastingSet(dish: dish)
                }
            )
        }
        // Course Planner Sheet
        .sheet(isPresented: $viewModel.isPlannerPresented) {
            CoursePlannerSheet(tastingItems: $viewModel.tastingSet)
                .presentationDetents([.large])
                .presentationDragIndicator(.visible)
                .presentationBackground(Color.obsidianCanvas)
        }
    }

    // MARK: - Subviews: Header & Navigation

    /// Editorial masthead header with dining area switcher
    private var topHeaderView: some View {
        HStack(alignment: .center) {
            VStack(alignment: .leading, spacing: 3) {
                Text("CARTE BLANCHE")
                    .font(.system(size: 24, weight: .bold, design: .serif))
                    .tracking(3)
                    .foregroundStyle(Color.textPrimary)

                Text("HAUTE CUISINE & TASTING GUIDE")
                    .font(.system(size: 9.5, weight: .bold, design: .monospaced))
                    .tracking(2)
                    .foregroundStyle(Color.champagneAccent)
            }

            Spacer()

            // Dining Area Switcher Menu
            Menu {
                ForEach(DiningArea.allCases) { area in
                    Button {
                        withAnimation(.spring(response: 0.3, dampingFraction: 0.7)) {
                            viewModel.selectedDiningArea = area
                        }
                    } label: {
                        Label(area.rawValue, systemImage: area.icon)
                    }
                }
            } label: {
                HStack(spacing: 6) {
                    Image(systemName: viewModel.selectedDiningArea.icon)
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(Color.champagneAccent)

                    Text(viewModel.selectedDiningArea.rawValue)
                        .font(.system(size: 12, weight: .semibold, design: .serif))
                        .foregroundStyle(Color.textPrimary)

                    Image(systemName: "chevron.up.chevron.down")
                        .font(.system(size: 9, weight: .bold))
                        .foregroundStyle(Color.champagneAccent.opacity(0.8))
                }
                .padding(.horizontal, 12)
                .padding(.vertical, 8)
                .background(Color.cardSurface)
                .clipShape(Capsule())
                .overlay(
                    Capsule()
                        .stroke(Color.glassBorder, lineWidth: 1)
                )
            }
        }
        .padding(.horizontal, 20)
        .padding(.top, 10)
    }

    /// Frosted glass search input bar
    private var searchBarView: some View {
        HStack(spacing: 10) {
            Image(systemName: "magnifyingglass")
                .font(.system(size: 14, weight: .medium))
                .foregroundStyle(Color.champagneAccent.opacity(0.85))

            TextField("Search dishes, ingredients, sommelier pairings...", text: $viewModel.searchQuery)
                .font(.system(.subheadline, design: .default))
                .foregroundStyle(Color.textPrimary)
                .autocorrectionDisabled()
                .textInputAutocapitalization(.never)

            if !viewModel.searchQuery.isEmpty {
                Button {
                    withAnimation(.spring(duration: 0.25)) {
                        viewModel.searchQuery = ""
                    }
                } label: {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 14))
                        .foregroundStyle(Color.textSecondary)
                }
                .buttonStyle(PlainButtonStyle())
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 11)
        .background(Color.cardSurface.opacity(0.92))
        .clipShape(RoundedRectangle(cornerRadius: 14))
        .overlay(
            RoundedRectangle(cornerRadius: 14)
                .stroke(Color.glassBorder, lineWidth: 1)
        )
        .padding(.horizontal, 20)
    }

    /// Horizontal dietary tag ribbon with quick reset button
    private var dietaryFilterRibbonView: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                HStack(spacing: 5) {
                    Image(systemName: "slider.horizontal.3")
                        .font(.system(size: 10, weight: .bold))
                        .foregroundStyle(Color.textSecondary)

                    Text("DIETARY & ALLERGEN FILTERS")
                        .font(.luxeMicroLabel)
                        .tracking(1.2)
                        .foregroundStyle(Color.textSecondary)
                }

                Spacer()

                if !viewModel.activeDietaryFilters.isEmpty {
                    Button {
                        withAnimation(.spring(response: 0.3, dampingFraction: 0.7)) {
                            viewModel.activeDietaryFilters.removeAll()
                        }
                    } label: {
                        HStack(spacing: 4) {
                            Text("Reset / Сброс")
                                .font(.system(size: 10.5, weight: .semibold, design: .monospaced))
                            Text("(\(viewModel.activeDietaryFilters.count))")
                                .font(.system(size: 10.5, weight: .bold, design: .monospaced))
                        }
                        .foregroundStyle(Color.champagneAccent)
                    }
                    .buttonStyle(PlainButtonStyle())
                }
            }
            .padding(.horizontal, 20)

            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 8) {
                    ForEach(DietaryTag.allCases) { tag in
                        let isSelected = viewModel.activeDietaryFilters.contains(tag)
                        DietaryBadgeView(tag: tag, isSelected: isSelected) {
                            withAnimation(.spring(response: 0.3, dampingFraction: 0.7)) {
                                viewModel.toggleDietaryFilter(tag)
                            }
                        }
                    }
                }
                .padding(.horizontal, 20)
                .padding(.vertical, 2)
            }
        }
    }

    /// Dynamic counter indicating visible dish count and filter resets
    private var resultsStatusBarView: some View {
        HStack {
            Text(statusCountTitle)
                .font(.system(size: 12, weight: .semibold, design: .serif))
                .foregroundStyle(Color.textSecondary)

            Spacer()

            if viewModel.hasActiveFilters {
                Button {
                    withAnimation(.spring(response: 0.3, dampingFraction: 0.7)) {
                        viewModel.clearFilters()
                    }
                } label: {
                    HStack(spacing: 4) {
                        Image(systemName: "xmark")
                            .font(.system(size: 9, weight: .bold))
                        Text("Clear All")
                            .font(.system(size: 11, weight: .semibold, design: .monospaced))
                    }
                    .foregroundStyle(Color.champagneAccent)
                }
                .buttonStyle(PlainButtonStyle())
            }
        }
        .padding(.horizontal, 20)
    }

    private var statusCountTitle: String {
        let count = viewModel.filteredDishes.count
        if let category = viewModel.selectedCategory {
            return "\(category.rawValue) • \(count) \(count == 1 ? "Dish" : "Dishes")"
        }
        return "Tasting Menu • \(count) \(count == 1 ? "Dish" : "Dishes")"
    }

    // MARK: - Subviews: Dish Cards & Empty State

    /// Lazy vertical stack of Dark Luxe dish cards
    private var dishCardsStreamView: some View {
        LazyVStack(spacing: 16) {
            ForEach(viewModel.filteredDishes) { dish in
                DishCardView(
                    dish: dish,
                    isInTastingSet: viewModel.isInTastingSet(dish: dish),
                    onSelect: {
                        viewModel.openDishDetail(dish)
                    },
                    onToggleTastingSet: {
                        viewModel.toggleTastingSet(dish: dish)
                    }
                )
            }
        }
        .padding(.horizontal, 20)
    }

    /// Empty state view presented when active filters match zero dishes
    private var emptyStateView: some View {
        VStack(spacing: 16) {
            ZStack {
                Circle()
                    .fill(Color.cardSurface)
                    .frame(width: 72, height: 72)
                    .overlay(
                        Circle()
                            .stroke(Color.glassBorder, lineWidth: 1)
                    )

                Image(systemName: "fork.knife.circle")
                    .font(.system(size: 34))
                    .foregroundStyle(Color.champagneAccent.opacity(0.8))
            }

            VStack(spacing: 6) {
                Text("No Gastronomic Dishes Found")
                    .font(.luxeHeadline)
                    .foregroundStyle(Color.textPrimary)

                Text("No culinary items match your current search query or allergen restrictions.")
                    .font(.luxeSubheadline)
                    .foregroundStyle(Color.textSecondary)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 36)
            }

            Button {
                withAnimation(.spring(response: 0.35, dampingFraction: 0.7)) {
                    viewModel.clearFilters()
                }
            } label: {
                HStack(spacing: 6) {
                    Image(systemName: "arrow.counterclockwise")
                        .font(.system(size: 11, weight: .bold))
                    Text("Reset All Filters / Сбросить фильтры")
                        .font(.system(size: 11.5, weight: .bold, design: .monospaced))
                }
                .padding(.horizontal, 18)
                .padding(.vertical, 10)
                .background(Color.champagneAccent)
                .foregroundStyle(Color.obsidianCanvas)
                .clipShape(Capsule())
                .shadow(color: Color.champagneAccent.opacity(0.35), radius: 8, y: 3)
            }
            .buttonStyle(PlainButtonStyle())
            .padding(.top, 6)
        }
        .padding(.vertical, 48)
        .frame(maxWidth: .infinity)
    }

    // MARK: - Subviews: Floating Tasting Set Bar

    /// Floating bottom bar displaying guest's curated flight metrics and planner action
    private var floatingTastingSetBar: some View {
        HStack(spacing: 14) {
            // Tasting Set Metrics
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 6) {
                    Circle()
                        .fill(Color.champagneAccent)
                        .frame(width: 6, height: 6)

                    Text("\(viewModel.tastingSetCount) \(viewModel.tastingSetCount == 1 ? "COURSE" : "COURSES") SELECTED")
                        .font(.system(size: 9.5, weight: .bold, design: .monospaced))
                        .tracking(1)
                        .foregroundStyle(Color.textSecondary)
                }

                Text(viewModel.formattedTastingSetPrice)
                    .font(.luxePrice)
                    .foregroundStyle(Color.champagneAccent)
            }

            Spacer()

            // Call to action button: Дегустационный сет / Подачи
            Button {
                viewModel.openPlanner()
            } label: {
                HStack(spacing: 8) {
                    Text("Дегустационный сет")
                        .font(.system(size: 13, weight: .bold, design: .serif))

                    Image(systemName: "arrow.right")
                        .font(.system(size: 11, weight: .bold))
                }
                .padding(.horizontal, 16)
                .padding(.vertical, 11)
                .background(Color.goldGradient)
                .foregroundStyle(Color.obsidianCanvas)
                .clipShape(Capsule())
                .shadow(color: Color.champagneAccent.opacity(0.35), radius: 10, y: 3)
            }
            .buttonStyle(PlainButtonStyle())
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 12)
        .background(
            RoundedRectangle(cornerRadius: 22, style: .continuous)
                .fill(Color.cardSurface.opacity(0.96))
                .overlay(
                    RoundedRectangle(cornerRadius: 22, style: .continuous)
                        .stroke(Color.champagneAccent.opacity(0.45), lineWidth: 1)
                )
                .shadow(color: Color.black.opacity(0.65), radius: 24, y: 10)
        )
        .padding(.horizontal, 16)
        .padding(.bottom, 12)
    }
}



// MARK: - Interactive Preview

#Preview("MenuHomeView Full Interactive Experience") {
    MenuHomeViewPreviewContainer()
}

private struct MenuHomeViewPreviewContainer: View {
    @State private var viewModel = MenuViewModel()

    var body: some View {
        MenuHomeView(viewModel: viewModel)
    }
}
