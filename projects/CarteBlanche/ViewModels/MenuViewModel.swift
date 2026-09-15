//
//  MenuViewModel.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI (Observation Framework)
//

import SwiftUI
import Foundation
import Observation

// MARK: - Dining Area Atmosphere

/// Luxury dining zones with curated ambiances
public enum DiningArea: String, CaseIterable, Identifiable, Hashable, Sendable {
    case mainDining = "Main Salon"
    case bar = "Chef's Bar"
    case terrace = "Veranda Terrace"

    public var id: String { rawValue }

    /// Curated subtitle describing the zone ambiance
    public var subtitle: String {
        switch self {
        case .mainDining:
            return "Grand Salon & Private Booths • Главный зал"
        case .bar:
            return "Front-Row Counter Experience • Бар шефа"
        case .terrace:
            return "Open-Air Garden & Sunset View • Веранда"
        }
    }

    /// SF Symbol representing the zone
    public var icon: String {
        switch self {
        case .mainDining:
            return "sparkles"
        case .bar:
            return "wineglass.fill"
        case .terrace:
            return "sun.horizon.fill"
        }
    }
}

// MARK: - Menu View Model

/// Primary observable state manager for Haute Cuisine menu exploration and tasting set curation
@Observable
public final class MenuViewModel {

    // MARK: - Dependencies

    private let dataService: MockDataService

    // MARK: - State Properties

    /// Active gastronomic course filter (nil represents "All Courses / Весь гид")
    public var selectedCategory: CourseCategory? = nil

    /// Real-time search query for dishes, ingredients, origins, and pairings
    public var searchQuery: String = ""

    /// Set of active dietary/allergen filters (items must satisfy ALL selected tags)
    public var activeDietaryFilters: Set<DietaryTag> = []

    /// Current dining zone / atmosphere selection
    public var selectedDiningArea: DiningArea = .mainDining

    /// Guest-curated tasting set of selected dishes
    public var tastingSet: [MenuItem] = []

    /// Target dish presented in DishDetailSheet (nil when sheet is dismissed)
    public var selectedDishForDetail: MenuItem? = nil

    /// Controls CoursePlannerSheet presentation
    public var isPlannerPresented: Bool = false

    // MARK: - Initializer

    public init(
        dataService: MockDataService = .shared,
        initialTastingSet: [MenuItem] = []
    ) {
        self.dataService = dataService
        self.tastingSet = initialTastingSet
    }

    // MARK: - Computed Properties (Reactive Filtering & Metrics)

    /// Reactive list of dishes matching category, search text, and dietary constraints
    public var filteredDishes: [MenuItem] {
        dataService.search(
            query: searchQuery,
            category: selectedCategory,
            dietary: activeDietaryFilters
        )
    }

    /// Total sum of all dishes selected in the tasting set
    public var tastingSetTotalPrice: Double {
        tastingSet.reduce(0.0) { $0 + $1.price }
    }

    /// Formatted total price string with Michelin currency standard
    public var formattedTastingSetPrice: String {
        String(format: "€%.2f", tastingSetTotalPrice)
    }

    /// Total number of unique dishes currently in the tasting set
    public var tastingSetCount: Int {
        tastingSet.count
    }

    /// Indicates whether any filters (search, category, or dietary) are actively applied
    public var hasActiveFilters: Bool {
        selectedCategory != nil || !activeDietaryFilters.isEmpty || !searchQuery.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    /// Verifies if a given dish is currently included in the tasting set
    public func isInTastingSet(dish: MenuItem) -> Bool {
        tastingSet.contains { $0.id == dish.id }
    }

    // MARK: - User Intent Actions

    /// Toggles inclusion of a dish in the guest's tasting set with animated state transition
    public func toggleTastingSet(dish: MenuItem) {
        if let index = tastingSet.firstIndex(where: { $0.id == dish.id }) {
            tastingSet.remove(at: index)
        } else {
            tastingSet.append(dish)
        }
    }

    /// Adds a dish to the tasting set if not already present
    public func addToTastingSet(dish: MenuItem) {
        guard !isInTastingSet(dish: dish) else { return }
        tastingSet.append(dish)
    }

    /// Removes a dish from the tasting set
    public func removeFromTastingSet(dish: MenuItem) {
        tastingSet.removeAll { $0.id == dish.id }
    }

    /// Toggles an allergen / dietary preference tag
    public func toggleDietaryFilter(_ tag: DietaryTag) {
        if activeDietaryFilters.contains(tag) {
            activeDietaryFilters.remove(tag)
        } else {
            activeDietaryFilters.insert(tag)
        }
    }

    /// Selects or switches the current course category
    public func selectCategory(_ category: CourseCategory?) {
        selectedCategory = category
    }

    /// Clears all search queries, category selections, and allergen filters
    public func clearFilters() {
        searchQuery = ""
        selectedCategory = nil
        activeDietaryFilters.removeAll()
    }

    /// Clears all dishes from the tasting set
    public func clearTastingSet() {
        tastingSet.removeAll()
    }

    /// Prepares a dish for detail presentation
    public func openDishDetail(_ dish: MenuItem) {
        selectedDishForDetail = dish
    }

    /// Dismisses the dish detail presentation
    public func closeDishDetail() {
        selectedDishForDetail = nil
    }

    /// Opens the Course Planner sheet
    public func openPlanner() {
        isPlannerPresented = true
    }

    /// Dismisses the Course Planner sheet
    public func closePlanner() {
        isPlannerPresented = false
    }
}

// MARK: - Interactive Preview & Diagnostic Showcase

#Preview("MenuViewModel State & Reactions") {
    MenuViewModelDiagnosticPreview()
}

private struct MenuViewModelDiagnosticPreview: View {
    @State private var viewModel = MenuViewModel()

    var body: some View {
        ZStack {
            Color.obsidianCanvas
                .ignoresSafeArea()

            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    // Header
                    VStack(alignment: .leading, spacing: 4) {
                        Text("VIEWMODEL DIAGNOSTIC")
                            .font(.luxeMicroLabel)
                            .tracking(2)
                            .foregroundStyle(Color.champagneAccent)

                        Text("MenuViewModel State Engine")
                            .font(.luxeDishTitle)
                            .foregroundStyle(Color.textPrimary)
                    }

                    // Area & Counts
                    HStack(spacing: 12) {
                        metricBox(title: "AREA", value: viewModel.selectedDiningArea.rawValue)
                        metricBox(title: "RESULTS", value: "\(viewModel.filteredDishes.count)")
                        metricBox(title: "IN SET", value: "\(viewModel.tastingSetCount)")
                        metricBox(title: "TOTAL", value: viewModel.formattedTastingSetPrice)
                    }

                    // Dining Area Selector
                    VStack(alignment: .leading, spacing: 6) {
                        Text("DINING AREA")
                            .font(.luxeMicroLabel)
                            .foregroundStyle(Color.textSecondary)

                        HStack(spacing: 8) {
                            ForEach(DiningArea.allCases) { area in
                                Button {
                                    viewModel.selectedDiningArea = area
                                } label: {
                                    HStack(spacing: 4) {
                                        Image(systemName: area.icon)
                                        Text(area.rawValue)
                                    }
                                    .font(.system(size: 11, weight: .semibold))
                                    .padding(.horizontal, 10)
                                    .padding(.vertical, 6)
                                    .background(viewModel.selectedDiningArea == area ? Color.champagneAccent : Color.cardSurface)
                                    .foregroundStyle(viewModel.selectedDiningArea == area ? Color.obsidianCanvas : Color.textSecondary)
                                    .clipShape(Capsule())
                                }
                            }
                        }
                    }

                    // Actions
                    HStack(spacing: 10) {
                        Button("Reset Filters") {
                            withAnimation { viewModel.clearFilters() }
                        }
                        .font(.system(size: 12, weight: .medium))
                        .foregroundStyle(Color.champagneAccent)

                        Spacer()

                        Button("Clear Set") {
                            withAnimation { viewModel.clearTastingSet() }
                        }
                        .font(.system(size: 12, weight: .medium))
                        .foregroundStyle(Color.burgundyAccent)
                    }

                    // First few dishes preview
                    VStack(spacing: 8) {
                        ForEach(viewModel.filteredDishes.prefix(3)) { dish in
                            HStack {
                                VStack(alignment: .leading, spacing: 2) {
                                    Text(dish.name)
                                        .font(.system(.subheadline, design: .serif, weight: .semibold))
                                        .foregroundStyle(Color.textPrimary)
                                    Text("\(dish.course.rawValue) • \(dish.formattedPrice)")
                                        .font(.system(size: 11))
                                        .foregroundStyle(Color.textSecondary)
                                }

                                Spacer()

                                Button {
                                    withAnimation { viewModel.toggleTastingSet(dish: dish) }
                                } label: {
                                    Image(systemName: viewModel.isInTastingSet(dish: dish) ? "checkmark.circle.fill" : "plus.circle")
                                        .font(.system(size: 20))
                                        .foregroundStyle(viewModel.isInTastingSet(dish: dish) ? Color.champagneAccent : Color.textSecondary)
                                }
                            }
                            .padding(12)
                            .background(Color.cardSurface)
                            .clipShape(RoundedRectangle(cornerRadius: 10))
                        }
                    }
                }
                .padding(20)
            }
        }
    }

    private func metricBox(title: String, value: String) -> some View {
        VStack(spacing: 2) {
            Text(title)
                .font(.system(size: 8, weight: .bold, design: .monospaced))
                .foregroundStyle(Color.textSecondary)
            Text(value)
                .font(.system(size: 13, weight: .bold, design: .serif))
                .foregroundStyle(Color.champagneAccent)
                .lineLimit(1)
                .minimumScaleFactor(0.8)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 8)
        .padding(.horizontal, 4)
        .background(Color.cardSurface)
        .clipShape(RoundedRectangle(cornerRadius: 8))
    }
}
