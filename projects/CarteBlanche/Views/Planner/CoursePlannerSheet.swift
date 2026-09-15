//
//  CoursePlannerSheet.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

#if canImport(UIKit)
import UIKit
#endif
import SwiftUI

/// Fullscreen or modal tasting flight presentation screen in Dark Luxe Editorial style
public struct CoursePlannerSheet: View {

    // MARK: - Environment & State

    @Environment(\.dismiss) private var dismiss
    @State public var viewModel: CoursePlannerViewModel
    private var externalBinding: Binding<[MenuItem]>?

    // MARK: - Initializers

    /// Primary initializer binding directly to MenuViewModel's tastingSet
    public init(tastingItems: Binding<[MenuItem]>) {
        self.externalBinding = tastingItems
        self._viewModel = State(
            initialValue: CoursePlannerViewModel(tastingItems: tastingItems.wrappedValue)
        )
    }

    /// Secondary initializer accepting an explicit CoursePlannerViewModel
    public init(viewModel: CoursePlannerViewModel) {
        self.externalBinding = nil
        self._viewModel = State(initialValue: viewModel)
    }

    // MARK: - Body

    public var body: some View {
        ZStack {
            // Obsidian Canvas Deep Background
            Color.obsidianCanvas
                .ignoresSafeArea()

            // Subtle Champagne Ambient Glow
            RadialGradient(
                colors: [
                    Color.champagneAccent.opacity(0.07),
                    Color.clear
                ],
                center: .top,
                startRadius: 30,
                endRadius: 460
            )
            .ignoresSafeArea()

            ScrollView {
                VStack(spacing: 24) {
                    // Top Presentation Masthead & Actions
                    topNavigationHeaderView

                    if viewModel.tastingItems.isEmpty {
                        // Empty State View
                        emptyStateView
                            .transition(.opacity.combined(with: .scale(scale: 0.95)))
                    } else {
                        // Chronological Course-by-Course Timeline
                        chronologicalTimelineView
                            .transition(.opacity)

                        // Embedded Offline Bill & Folio Calculator
                        OfflineBillCalculatorView(viewModel: viewModel)
                            .padding(.horizontal, 20)
                            .transition(.move(edge: .bottom).combined(with: .opacity))
                    }
                }
                .padding(.top, 16)
                .padding(.bottom, 36)
            }
        }
    }

    // MARK: - Subviews: Navigation Header

    private var topNavigationHeaderView: some View {
        HStack(alignment: .center) {
            // Dismiss Button
            Button {
                dismiss()
            } label: {
                Image(systemName: "xmark.circle.fill")
                    .font(.system(size: 26))
                    .foregroundStyle(Color.textSecondary)
            }
            .buttonStyle(PlainButtonStyle())

            Spacer()

            // Centered Editorial Title
            VStack(spacing: 3) {
                Text("TASTING JOURNEY")
                    .font(.luxeMicroLabel)
                    .tracking(2.6)
                    .foregroundStyle(Color.champagneAccent)

                Text("План Подач")
                    .font(.system(.title3, design: .serif, weight: .bold))
                    .foregroundStyle(Color.textPrimary)
            }

            Spacer()

            // Reset / Clear Flight Button
            if !viewModel.tastingItems.isEmpty {
                Button {
                    clearAll()
                } label: {
                    HStack(spacing: 4) {
                        Image(systemName: "trash")
                            .font(.system(size: 11, weight: .semibold))
                        Text("Очистить")
                            .font(.system(size: 11, weight: .semibold, design: .monospaced))
                    }
                    .foregroundStyle(Color.burgundyAccent)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 6)
                    .background(Color.burgundyAccent.opacity(0.12))
                    .clipShape(Capsule())
                    .overlay(
                        Capsule()
                            .stroke(Color.burgundyAccent.opacity(0.3), lineWidth: 1)
                    )
                }
                .buttonStyle(PlainButtonStyle())
            } else {
                // Invisible balance spacer for perfect centering
                Color.clear
                    .frame(width: 60, height: 28)
            }
        }
        .padding(.horizontal, 20)
    }

    // MARK: - Subviews: Chronological Course Timeline

    private var chronologicalTimelineView: some View {
        VStack(alignment: .leading, spacing: 24) {
            // Timeline Intro Banner
            HStack(spacing: 8) {
                Image(systemName: "clock.arrow.circlepath")
                    .font(.system(size: 12))
                    .foregroundStyle(Color.champagneAccent)

                Text("CHRONOLOGICAL FLIGHT • ГАСТРОНОМИЧЕСКИЙ КАНОН")
                    .font(.luxeMicroLabel)
                    .tracking(1.4)
                    .foregroundStyle(Color.textSecondary)

                Spacer()

                Text("\(viewModel.totalDishesCount) поз. / \(viewModel.activeCoursesCount) под.")
                    .font(.system(size: 11, weight: .medium, design: .monospaced))
                    .foregroundStyle(Color.champagneAccent)
            }
            .padding(.horizontal, 20)

            // Course Nodes
            VStack(spacing: 22) {
                ForEach(viewModel.groupedCourses, id: \.category) { group in
                    courseStageSectionView(category: group.category, items: group.items)
                }
            }
            .padding(.horizontal, 20)
        }
    }

    /// Single stage node in the tasting sequence
    private func courseStageSectionView(category: CourseCategory, items: [MenuItem]) -> some View {
        VStack(alignment: .leading, spacing: 14) {
            // Stage Header Bar
            HStack(spacing: 10) {
                // Roman Numeral Badge
                ZStack {
                    Circle()
                        .fill(Color.cardSurface)
                        .frame(width: 32, height: 32)
                        .overlay(
                            Circle()
                                .stroke(Color.champagneAccent.opacity(0.5), lineWidth: 1)
                        )

                    Text(viewModel.canonicalRomanNumeral(for: category))
                        .font(.system(size: 12, weight: .bold, design: .serif))
                        .foregroundStyle(Color.champagneAccent)
                }

                // Stage Titles
                VStack(alignment: .leading, spacing: 2) {
                    HStack(spacing: 6) {
                        Image(systemName: category.systemImage)
                            .font(.system(size: 10))
                            .foregroundStyle(Color.champagneAccent)

                        Text(stageDisplayHeader(for: category))
                            .font(.system(size: 10, weight: .bold, design: .monospaced))
                            .tracking(1.2)
                            .foregroundStyle(Color.champagneAccent)
                    }

                    Text(stageSubtitle(for: category))
                        .font(.system(size: 12, weight: .medium, design: .serif))
                        .foregroundStyle(Color.textPrimary)
                }

                Spacer()

                Text("\(items.count) \(items.count == 1 ? "dish" : "dishes")")
                    .font(.system(size: 10, weight: .bold, design: .monospaced))
                    .foregroundStyle(Color.textSecondary)
            }

            // Dishes in this course stage
            VStack(spacing: 12) {
                ForEach(items) { dish in
                    DishPlannerRowView(
                        dish: dish,
                        rating: viewModel.rating(for: dish.id),
                        initialNotes: viewModel.notes(for: dish.id),
                        onRatingChanged: { newRating in
                            viewModel.updateRating(for: dish.id, rating: newRating)
                        },
                        onNotesChanged: { newNotes in
                            viewModel.updateNotes(for: dish.id, notes: newNotes)
                        },
                        onRemove: {
                            removeDish(id: dish.id)
                        }
                    )
                }
            }
        }
    }

    private func stageDisplayHeader(for category: CourseCategory) -> String {
        switch category {
        case .prelude:
            return "ПОДАЧА 1 • PRELUDE"
        case .main:
            return "ПОДАЧА 2 • MAIN COURSES"
        case .garden:
            return "ПОДАЧА 3 • GARDEN & PALETTE"
        case .dessert:
            return "ПОДАЧА 4 • DESSERTS & FROMAGE"
        case .cellar:
            return "ПОДАЧА 5 • CELLAR & DIGESTIF"
        }
    }

    private func stageSubtitle(for category: CourseCategory) -> String {
        switch category {
        case .prelude:
            return "Закуски и аперитивы"
        case .main:
            return "Основные подачи и фирменные блюда"
        case .garden:
            return "Растительные шедевры и очищение рецепторов"
        case .dessert:
            return "Сладкий финал и сырная тарелка"
        case .cellar:
            return "Дижестивы, винные раритеты и бар шефа"
        }
    }

    // MARK: - Subviews: Empty State

    private var emptyStateView: some View {
        VStack(spacing: 22) {
            ZStack {
                Circle()
                    .fill(Color.cardSurface)
                    .frame(width: 84, height: 84)
                    .overlay(
                        Circle()
                            .stroke(Color.glassBorder, lineWidth: 1)
                    )

                Image(systemName: "fork.knife.circle")
                    .font(.system(size: 42))
                    .foregroundStyle(Color.champagneAccent.opacity(0.85))
            }
            .padding(.top, 30)

            VStack(spacing: 8) {
                Text("Ваш дегустационный сет пуст")
                    .font(.system(.title2, design: .serif, weight: .bold))
                    .foregroundStyle(Color.textPrimary)

                Text("В сете пока нет выбранных позиций. Добавьте авторские блюда из меню или начните с идеальной гармонии шефа.")
                    .font(.luxeSubheadline)
                    .foregroundStyle(Color.textSecondary)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 36)
                    .lineSpacing(3)
            }

            // Quick Add Chef's Recommendations Button
            Button {
                addChefSelections()
            } label: {
                HStack(spacing: 8) {
                    Image(systemName: "crown.fill")
                        .font(.system(size: 13))
                    Text("Добавить рекомендации шефа")
                        .font(.system(size: 13, weight: .bold, design: .serif))
                }
                .padding(.horizontal, 20)
                .padding(.vertical, 12)
                .background(Color.goldGradient)
                .foregroundStyle(Color.obsidianCanvas)
                .clipShape(Capsule())
                .shadow(color: Color.champagneAccent.opacity(0.3), radius: 10, y: 4)
            }
            .buttonStyle(PlainButtonStyle())

            // Return to Menu Button
            Button {
                dismiss()
            } label: {
                HStack(spacing: 6) {
                    Image(systemName: "arrow.left")
                        .font(.system(size: 11, weight: .bold))
                    Text("Вернуться в меню")
                        .font(.system(size: 12, weight: .semibold, design: .monospaced))
                }
                .foregroundStyle(Color.champagneAccent)
                .padding(.top, 4)
            }
            .buttonStyle(PlainButtonStyle())
        }
        .padding(.vertical, 40)
        .frame(maxWidth: .infinity)
    }

    // MARK: - Actions

    private func removeDish(id: String) {
        withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
            viewModel.removeDish(id: id)
            externalBinding?.wrappedValue = viewModel.tastingItems
        }
    }

    private func clearAll() {
        withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
            viewModel.clearAll()
            externalBinding?.wrappedValue = []
        }
    }

    private func addChefSelections() {
        let chefItems = MockDataService.shared.chefSelections
        withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
            viewModel.addDishes(chefItems)
            externalBinding?.wrappedValue = viewModel.tastingItems
        }
    }
}

// MARK: - Dish Planner Row Component

/// Interactive card for an individual dish within the tasting timeline
private struct DishPlannerRowView: View {
    let dish: MenuItem
    let rating: Int
    let initialNotes: String
    let onRatingChanged: (Int) -> Void
    let onNotesChanged: (String) -> Void
    let onRemove: () -> Void

    @State private var notesText: String = ""
    @State private var isNoteExpanded: Bool = false

    var body: some View {
        GlassCard(
            cornerRadius: 16,
            strokeColor: Color.glassBorder,
            fillOpacity: 0.82,
            padding: 16
        ) {
            VStack(alignment: .leading, spacing: 12) {
                // Top Row: Title, Price, and Remove Button
                HStack(alignment: .top, spacing: 10) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text(dish.name)
                            .font(.system(.subheadline, design: .serif, weight: .bold))
                            .foregroundStyle(Color.textPrimary)

                        // Mini-Pairing Tag
                        HStack(spacing: 5) {
                            Image(systemName: "wineglass.fill")
                                .font(.system(size: 10))
                                .foregroundStyle(Color.burgundyAccent)

                            Text(dish.pairing.name)
                                .font(.system(size: 11, weight: .medium))
                                .foregroundStyle(Color.textSecondary)
                                .lineLimit(1)
                        }
                    }

                    Spacer()

                    VStack(alignment: .trailing, spacing: 6) {
                        Text(dish.formattedPrice)
                            .font(.luxeCardPrice)
                            .foregroundStyle(Color.champagneAccent)

                        // Remove Trash Button
                        Button {
                            onRemove()
                        } label: {
                            Image(systemName: "trash")
                                .font(.system(size: 13))
                                .foregroundStyle(Color.burgundyAccent.opacity(0.85))
                                .frame(width: 28, height: 28)
                                .background(Color.burgundyAccent.opacity(0.1))
                                .clipShape(Circle())
                        }
                        .buttonStyle(PlainButtonStyle())
                    }
                }

                Divider()
                    .background(Color.glassBorder.opacity(0.6))

                // Bottom Row: 5-Star Interactive Rating
                HStack(spacing: 12) {
                    VStack(alignment: .leading, spacing: 2) {
                        Text("ОЦЕНКА БЛЮДА")
                            .font(.system(size: 8.5, weight: .bold, design: .monospaced))
                            .tracking(1)
                            .foregroundStyle(Color.textSecondary)

                        starRatingBar
                    }

                    Spacer()

                    // Toggle Note Button
                    Button {
                        withAnimation(.spring(response: 0.3, dampingFraction: 0.75)) {
                            isNoteExpanded.toggle()
                        }
                    } label: {
                        HStack(spacing: 4) {
                            Image(systemName: isNoteExpanded ? "chevron.up" : "square.and.pencil")
                                .font(.system(size: 10))
                            Text(isNoteExpanded ? "Скрыть" : (notesText.isEmpty ? "Заметка" : "Заметка •"))
                                .font(.system(size: 11, weight: .medium, design: .monospaced))
                        }
                        .foregroundStyle(notesText.isEmpty ? Color.textSecondary : Color.champagneAccent)
                        .padding(.horizontal, 8)
                        .padding(.vertical, 4)
                        .background(Color.obsidianCanvas.opacity(0.6))
                        .clipShape(Capsule())
                        .overlay(
                            Capsule()
                                .stroke(notesText.isEmpty ? Color.glassBorder : Color.champagneAccent.opacity(0.4), lineWidth: 1)
                        )
                    }
                    .buttonStyle(PlainButtonStyle())
                }

                // Expandable Personal Tasting Note Field
                if isNoteExpanded || !notesText.isEmpty {
                    VStack(alignment: .leading, spacing: 4) {
                        HStack(spacing: 6) {
                            Image(systemName: "quote.opening")
                                .font(.system(size: 9))
                                .foregroundStyle(Color.champagneAccent)

                            Text("ЛИЧНАЯ ЗАМЕТКА")
                                .font(.system(size: 8.5, weight: .bold, design: .monospaced))
                                .foregroundStyle(Color.textSecondary)
                        }

                        HStack(spacing: 8) {
                            TextField("Идеальный баланс юдзу и трюфеля...", text: $notesText)
                                .font(.system(size: 12))
                                .foregroundStyle(Color.textPrimary)
                                .onChange(of: notesText) { _, newValue in
                                    onNotesChanged(newValue)
                                }

                            if !notesText.isEmpty {
                                Button {
                                    notesText = ""
                                    onNotesChanged("")
                                } label: {
                                    Image(systemName: "xmark.circle.fill")
                                        .font(.system(size: 12))
                                        .foregroundStyle(Color.textSecondary)
                                }
                                .buttonStyle(PlainButtonStyle())
                            }
                        }
                        .padding(.horizontal, 10)
                        .padding(.vertical, 8)
                        .background(Color.obsidianCanvas.opacity(0.8))
                        .clipShape(RoundedRectangle(cornerRadius: 8))
                        .overlay(
                            RoundedRectangle(cornerRadius: 8)
                                .stroke(Color.glassBorder, lineWidth: 1)
                        )
                    }
                    .transition(.opacity.combined(with: .move(edge: .top)))
                }
            }
        }
        .onAppear {
            self.notesText = initialNotes
            if !initialNotes.isEmpty {
                self.isNoteExpanded = true
            }
        }
    }

    /// Interactive 1 to 5 star rating strip with gold fill and haptics
    private var starRatingBar: some View {
        HStack(spacing: 6) {
            ForEach(1...5, id: \.self) { star in
                Button {
                    triggerTactileFeedback()
                    let next = (rating == star) ? 0 : star
                    onRatingChanged(next)
                } label: {
                    Image(systemName: star <= rating ? "star.fill" : "star")
                        .font(.system(size: 14))
                        .foregroundStyle(star <= rating ? Color.champagneAccent : Color.textSecondary.opacity(0.35))
                        .contentShape(Rectangle())
                }
                .buttonStyle(PlainButtonStyle())
            }
        }
    }

    private func triggerTactileFeedback() {
        #if canImport(UIKit)
        let generator = UIImpactFeedbackGenerator(style: .light)
        generator.impactOccurred()
        #endif
    }
}

// MARK: - Interactive Preview

#Preview("CoursePlannerSheet Full Tasting Journey") {
    CoursePlannerSheetPreviewContainer()
}

private struct CoursePlannerSheetPreviewContainer: View {
    @State private var sampleSet: [MenuItem] = Array(MockDataService.shared.items.prefix(4))

    var body: some View {
        CoursePlannerSheet(tastingItems: $sampleSet)
    }
}
