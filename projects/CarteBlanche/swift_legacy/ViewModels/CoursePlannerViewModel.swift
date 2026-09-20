//
//  CoursePlannerViewModel.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI (Observation Framework)
//

import SwiftUI
import Foundation
import Observation

// MARK: - Tip Percentage Options

/// Gratuity and service compliment percentages for the Luxe Folio
public enum TipPercentage: Int, CaseIterable, Identifiable, Sendable {
    case zero = 0
    case ten = 10
    case fifteen = 15
    case twenty = 20

    public var id: Int { rawValue }

    /// Decimal rate used for bill multiplication
    public var rate: Double {
        Double(rawValue) / 100.0
    }

    /// Display title for chip buttons and receipts
    public var displayTitle: String {
        "\(rawValue)%"
    }

    /// Editorial descriptor in French & Russian
    public var subtitle: String {
        switch self {
        case .zero:
            return "Sans pourboire • Без чаевых"
        case .ten:
            return "Service Délicat • 10%"
        case .fifteen:
            return "Compliment du Sommelier • 15%"
        case .twenty:
            return "Excellence Haute Cuisine • 20%"
        }
    }
}

// MARK: - Dish Personal Feedback (Rating & Notes)

/// Offline record storing guest ratings (1-5 stars) and personal tasting notes
public struct DishFeedback: Codable, Hashable, Sendable {
    public var rating: Int // 0 to 5
    public var notes: String
    public var updatedAt: Date

    public init(rating: Int = 0, notes: String = "", updatedAt: Date = Date()) {
        self.rating = max(0, min(5, rating))
        self.notes = notes
        self.updatedAt = updatedAt
    }
}

// MARK: - Course Planner View Model

/// Observable state manager for tasting course timeline, offline ratings, and luxury guest folio bill calculation
@Observable
public final class CoursePlannerViewModel {

    // MARK: - Constants & Storage Keys

    private static let userDefaultsFeedbackKey = "carte_blanche_dish_feedback_v1"

    // MARK: - State Properties

    /// Current tasting set items curated by the guest
    public var tastingItems: [MenuItem] {
        didSet {
            onItemsChanged?(tastingItems)
        }
    }

    /// Number of dining guests for bill splitting (clamped to 1...8)
    public var guestCount: Int {
        didSet {
            if guestCount < 1 { guestCount = 1 }
            if guestCount > 8 { guestCount = 8 }
        }
    }

    /// Selected gratuity / service percentage
    public var selectedTipPercentage: TipPercentage

    /// In-memory dictionary of guest ratings and tasting notes keyed by dish identifier
    public var feedbackStore: [String: DishFeedback] = [:] {
        didSet {
            saveFeedbackToOfflineStorage()
        }
    }

    /// Optional closure notifying observers when items are removed or updated
    public var onItemsChanged: (([MenuItem]) -> Void)?

    // MARK: - Initializer

    public init(
        tastingItems: [MenuItem] = [],
        guestCount: Int = 2,
        selectedTipPercentage: TipPercentage = .fifteen,
        onItemsChanged: (([MenuItem]) -> Void)? = nil
    ) {
        self.tastingItems = tastingItems
        self.guestCount = min(8, max(1, guestCount))
        self.selectedTipPercentage = selectedTipPercentage
        self.onItemsChanged = onItemsChanged

        loadFeedbackFromOfflineStorage()
    }

    // MARK: - Computed Properties: Gastronomic Progression Order

    /// Dishes grouped by course category, strictly sorted according to the Haute Cuisine canon:
    /// 1. Prelude -> 2. Main Courses -> 3. Garden -> 4. Desserts & Fromage -> 5. Cellar
    public var groupedCourses: [(category: CourseCategory, items: [MenuItem])] {
        // CourseCategory.allCases is ordered: prelude, main, garden, dessert, cellar
        CourseCategory.allCases.compactMap { category in
            let items = tastingItems.filter { $0.course == category }
            return items.isEmpty ? nil : (category: category, items: items)
        }
    }

    /// Total count of courses currently represented in the set
    public var activeCoursesCount: Int {
        groupedCourses.count
    }

    /// Total count of unique dishes in the tasting flight
    public var totalDishesCount: Int {
        tastingItems.count
    }

    /// Canonical order index for a given category (1 to 5)
    public func canonicalIndex(for category: CourseCategory) -> Int {
        switch category {
        case .prelude: return 1
        case .main: return 2
        case .garden: return 3
        case .dessert: return 4
        case .cellar: return 5
        }
    }

    /// Roman numeral representation of the canonical course (I to V)
    public func canonicalRomanNumeral(for category: CourseCategory) -> String {
        switch category {
        case .prelude: return "I"
        case .main: return "II"
        case .garden: return "III"
        case .dessert: return "IV"
        case .cellar: return "V"
        }
    }

    /// Full Russian & French title of the course stage
    public func canonicalStageTitle(for category: CourseCategory) -> String {
        switch category {
        case .prelude:
            return "Подача I • Prelude (Закуски и аперитивы)"
        case .main:
            return "Подача II • Main Courses (Основные блюда)"
        case .garden:
            return "Подача III • Garden & Palette Cleansers (Растительные шедевры)"
        case .dessert:
            return "Подача IV • Desserts & Fromage (Сладкий финал)"
        case .cellar:
            return "Подача V • Cellar & Digestif (Дижестивы и напитки)"
        }
    }

    // MARK: - Computed Properties: Financials & Bill Folio

    /// Total sum of all dishes selected in the set (before tips)
    public var subtotal: Double {
        tastingItems.reduce(0.0) { $0 + $1.price }
    }

    /// Gratuity / Service charge calculated based on selected tip percentage
    public var tipAmount: Double {
        subtotal * selectedTipPercentage.rate
    }

    /// Final grand total of the bill including subtotal and gratuity
    public var totalAmount: Double {
        subtotal + tipAmount
    }

    /// Split amount per dining guest
    public var amountPerPerson: Double {
        let validGuests = max(1, guestCount)
        return totalAmount / Double(validGuests)
    }

    // MARK: - Formatted Currency Strings (Michelin Standard)

    public var formattedSubtotal: String {
        String(format: "€%.2f", subtotal)
    }

    public var formattedTipAmount: String {
        String(format: "€%.2f", tipAmount)
    }

    public var formattedTotalAmount: String {
        String(format: "€%.2f", totalAmount)
    }

    public var formattedAmountPerPerson: String {
        String(format: "€%.2f", amountPerPerson)
    }

    // MARK: - Guest Count Modification

    public func incrementGuests() {
        if guestCount < 8 {
            guestCount += 1
        }
    }

    public func decrementGuests() {
        if guestCount > 1 {
            guestCount -= 1
        }
    }

    // MARK: - Dish Set Management

    /// Removes a dish from the tasting set by identifier
    public func removeDish(id: String) {
        tastingItems.removeAll { $0.id == id }
    }

    /// Adds a dish to the set if not already present
    public func addDish(_ item: MenuItem) {
        guard !contains(dishId: item.id) else { return }
        tastingItems.append(item)
    }

    /// Adds multiple dishes (e.g. Chef's Selections) to the set
    public func addDishes(_ items: [MenuItem]) {
        for item in items where !contains(dishId: item.id) {
            tastingItems.append(item)
        }
    }

    /// Clears all dishes from the tasting flight
    public func clearAll() {
        tastingItems.removeAll()
    }

    /// Checks if a dish is currently included in the flight
    public func contains(dishId: String) -> Bool {
        tastingItems.contains { $0.id == dishId }
    }

    // MARK: - Offline Rating & Notes Management

    /// Returns guest's personal star rating for a dish (0 to 5)
    public func rating(for dishId: String) -> Int {
        feedbackStore[dishId]?.rating ?? 0
    }

    /// Returns guest's personal tasting notes for a dish
    public func notes(for dishId: String) -> String {
        feedbackStore[dishId]?.notes ?? ""
    }

    /// Updates personal star rating for a dish and triggers persistence
    public func updateRating(for dishId: String, rating: Int) {
        let clamped = max(0, min(5, rating))
        var entry = feedbackStore[dishId] ?? DishFeedback()
        entry.rating = clamped
        entry.updatedAt = Date()
        feedbackStore[dishId] = entry
    }

    /// Updates personal tasting notes for a dish and triggers persistence
    public func updateNotes(for dishId: String, notes: String) {
        var entry = feedbackStore[dishId] ?? DishFeedback()
        entry.notes = notes
        entry.updatedAt = Date()
        feedbackStore[dishId] = entry
    }

    // MARK: - Offline Persistence (UserDefaults & JSON)

    private func loadFeedbackFromOfflineStorage() {
        guard let data = UserDefaults.standard.data(forKey: Self.userDefaultsFeedbackKey) else {
            return
        }

        do {
            let decoded = try JSONDecoder().decode([String: DishFeedback].self, from: data)
            self.feedbackStore = decoded
        } catch {
            print("[CoursePlannerViewModel] Notice: Could not decode existing feedback JSON: \(error)")
        }
    }

    private func saveFeedbackToOfflineStorage() {
        do {
            let encoded = try JSONEncoder().encode(feedbackStore)
            UserDefaults.standard.set(encoded, forKey: Self.userDefaultsFeedbackKey)
        } catch {
            print("[CoursePlannerViewModel] Notice: Could not encode feedback to JSON: \(error)")
        }
    }
}

// MARK: - Interactive Preview

#Preview("CoursePlannerViewModel Calculations") {
    CoursePlannerViewModelDiagnosticView()
}

private struct CoursePlannerViewModelDiagnosticView: View {
    @State private var viewModel: CoursePlannerViewModel = {
        let sampleItems = Array(MockDataService.shared.items.prefix(4))
        let vm = CoursePlannerViewModel(tastingItems: sampleItems, guestCount: 2, selectedTipPercentage: .fifteen)
        vm.updateRating(for: sampleItems.first?.id ?? "", rating: 5)
        vm.updateNotes(for: sampleItems.first?.id ?? "", notes: "Идеальный баланс юдзу и трюфеля.")
        return vm
    }()

    var body: some View {
        ZStack {
            Color.obsidianCanvas
                .ignoresSafeArea()

            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    Text("COURSE PLANNER STATE ENGINE")
                        .font(.luxeMicroLabel)
                        .foregroundStyle(Color.champagneAccent)

                    HStack(spacing: 12) {
                        metricTile(title: "SUBTOTAL", value: viewModel.formattedSubtotal)
                        metricTile(title: "TIP (\(viewModel.selectedTipPercentage.displayTitle))", value: viewModel.formattedTipAmount)
                        metricTile(title: "TOTAL", value: viewModel.formattedTotalAmount)
                        metricTile(title: "PER GUEST", value: viewModel.formattedAmountPerPerson)
                    }

                    // Tip Selector
                    VStack(alignment: .leading, spacing: 6) {
                        Text("TIP PERCENTAGE")
                            .font(.luxeMicroLabel)
                            .foregroundStyle(Color.textSecondary)

                        HStack(spacing: 8) {
                            ForEach(TipPercentage.allCases) { tip in
                                Button(tip.displayTitle) {
                                    viewModel.selectedTipPercentage = tip
                                }
                                .font(.system(size: 12, weight: .bold))
                                .padding(.horizontal, 14)
                                .padding(.vertical, 8)
                                .background(viewModel.selectedTipPercentage == tip ? Color.champagneAccent : Color.cardSurface)
                                .foregroundStyle(viewModel.selectedTipPercentage == tip ? Color.obsidianCanvas : Color.textPrimary)
                                .clipShape(Capsule())
                            }
                        }
                    }

                    // Guest Stepper
                    HStack {
                        Text("Guests: \(viewModel.guestCount)")
                            .font(.luxeHeadline)
                            .foregroundStyle(Color.textPrimary)

                        Spacer()

                        Button("-") { viewModel.decrementGuests() }
                            .font(.title2)
                            .foregroundStyle(Color.champagneAccent)
                            .frame(width: 36, height: 36)
                            .background(Color.cardSurface)
                            .clipShape(Circle())

                        Button("+") { viewModel.incrementGuests() }
                            .font(.title2)
                            .foregroundStyle(Color.champagneAccent)
                            .frame(width: 36, height: 36)
                            .background(Color.cardSurface)
                            .clipShape(Circle())
                    }

                    // Grouped courses
                    VStack(alignment: .leading, spacing: 10) {
                        Text("GROUPED COURSES CANON (\(viewModel.groupedCourses.count))")
                            .font(.luxeMicroLabel)
                            .foregroundStyle(Color.textSecondary)

                        ForEach(viewModel.groupedCourses, id: \.category) { group in
                            VStack(alignment: .leading, spacing: 4) {
                                Text("\(viewModel.canonicalRomanNumeral(for: group.category)). \(group.category.rawValue)")
                                    .font(.system(size: 13, weight: .bold, design: .serif))
                                    .foregroundStyle(Color.champagneAccent)

                                ForEach(group.items) { item in
                                    HStack {
                                        Text(item.name)
                                            .font(.system(size: 12))
                                            .foregroundStyle(Color.textPrimary)
                                        Spacer()
                                        Text(item.formattedPrice)
                                            .font(.system(size: 12, design: .monospaced))
                                            .foregroundStyle(Color.textSecondary)
                                    }
                                }
                            }
                            .padding(10)
                            .background(Color.cardSurface)
                            .clipShape(RoundedRectangle(cornerRadius: 8))
                        }
                    }
                }
                .padding(20)
            }
        }
    }

    private func metricTile(title: String, value: String) -> some View {
        VStack(spacing: 3) {
            Text(title)
                .font(.system(size: 8, weight: .bold, design: .monospaced))
                .foregroundStyle(Color.textSecondary)
            Text(value)
                .font(.system(size: 13, weight: .bold, design: .serif))
                .foregroundStyle(Color.champagneAccent)
                .minimumScaleFactor(0.7)
                .lineLimit(1)
        }
        .frame(maxWidth: .infinity)
        .padding(8)
        .background(Color.cardSurface)
        .clipShape(RoundedRectangle(cornerRadius: 8))
    }
}
