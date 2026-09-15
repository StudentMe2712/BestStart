//
//  CategoryPillsView.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

/// Horizontal ribbon of gastronomic course pills with a floating matched-geometry selection indicator
public struct CategoryPillsView: View {

    // MARK: - Properties

    @Binding public var selectedCategory: CourseCategory?
    public var onSelect: ((CourseCategory?) -> Void)?

    @Namespace private var animationNamespace

    // MARK: - Initializer

    public init(
        selectedCategory: Binding<CourseCategory?>,
        onSelect: ((CourseCategory?) -> Void)? = nil
    ) {
        self._selectedCategory = selectedCategory
        self.onSelect = onSelect
    }

    // MARK: - Body

    public var body: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 8) {
                // "All / Весь гид" Option
                pillButton(
                    id: "all_categories",
                    title: "All / Весь гид",
                    systemImage: "sparkles",
                    isSelected: selectedCategory == nil
                ) {
                    selectCategory(nil)
                }

                // Course Category Cases
                ForEach(CourseCategory.allCases) { category in
                    pillButton(
                        id: category.id,
                        title: category.rawValue,
                        systemImage: category.systemImage,
                        isSelected: selectedCategory == category
                    ) {
                        selectCategory(category)
                    }
                }
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 6)
        }
        .sensoryFeedback(.selection, trigger: selectedCategory)
    }

    // MARK: - Pill Button ViewBuilder

    @ViewBuilder
    private func pillButton(
        id: String,
        title: String,
        systemImage: String,
        isSelected: Bool,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            HStack(spacing: 6) {
                Image(systemName: systemImage)
                    .font(.system(size: 11, weight: isSelected ? .bold : .medium))
                    .foregroundStyle(isSelected ? Color.obsidianCanvas : Color.champagneAccent)

                Text(title)
                    .font(.system(size: 13, weight: isSelected ? .bold : .medium, design: isSelected ? .serif : .default))
                    .foregroundStyle(isSelected ? Color.obsidianCanvas : Color.textPrimary.opacity(0.9))
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 8)
            .background {
                if isSelected {
                    Capsule()
                        .fill(Color.champagneAccent)
                        .matchedGeometryEffect(id: "activeCourseIndicator", in: animationNamespace)
                        .shadow(color: Color.champagneAccent.opacity(0.35), radius: 8, y: 3)
                } else {
                    Capsule()
                        .fill(Color.cardSurface.opacity(0.85))
                        .overlay(
                            Capsule()
                                .stroke(Color.glassBorder, lineWidth: 1)
                        )
                }
            }
        }
        .buttonStyle(PlainButtonStyle())
        .contentShape(Capsule())
    }

    // MARK: - Action Dispatch

    private func selectCategory(_ category: CourseCategory?) {
        withAnimation(.spring(response: 0.35, dampingFraction: 0.75)) {
            selectedCategory = category
            onSelect?(category)
        }
    }
}

// MARK: - Interactive Preview

#Preview("Category Pills Showcase") {
    CategoryPillsPreviewContainer()
}

private struct CategoryPillsPreviewContainer: View {
    @State private var selectedCategory: CourseCategory? = nil

    var body: some View {
        ZStack {
            Color.obsidianCanvas
                .ignoresSafeArea()

            VStack(alignment: .leading, spacing: 24) {
                // Editorial Header
                VStack(alignment: .leading, spacing: 4) {
                    Text("HAUTE CUISINE COURSES")
                        .font(.luxeMicroLabel)
                        .tracking(2)
                        .foregroundStyle(Color.champagneAccent)

                    Text("Course Progression")
                        .font(.luxeLargeTitle)
                        .foregroundStyle(Color.textPrimary)
                }
                .padding(.horizontal, 20)
                .padding(.top, 16)

                // Category Pills Ribbon
                CategoryPillsView(selectedCategory: $selectedCategory)

                // Detailed Course Info Card
                VStack(alignment: .leading, spacing: 10) {
                    HStack {
                        Image(systemName: selectedCategory?.systemImage ?? "sparkles")
                            .font(.system(size: 16))
                            .foregroundStyle(Color.champagneAccent)

                        Text(selectedCategory?.rawValue.uppercased() ?? "ALL COURSES • ВЕСЬ ГИД")
                            .font(.system(size: 12, weight: .bold, design: .monospaced))
                            .tracking(1.5)
                            .foregroundStyle(Color.champagneAccent)

                        Spacer()

                        Text(selectedCategory != nil ? "Filtered" : "Complete Guide")
                            .font(.system(size: 10, weight: .medium, design: .monospaced))
                            .padding(.horizontal, 8)
                            .padding(.vertical, 3)
                            .background(Color.champagneAccent.opacity(0.12))
                            .clipShape(Capsule())
                            .foregroundStyle(Color.champagneAccent)
                    }

                    Text(selectedCategory?.subtitle ?? "Curated gastronomic progression across all culinary chapters • Полный дегустационный маршрут")
                        .font(.luxeSubheadline)
                        .foregroundStyle(Color.textSecondary)
                        .lineSpacing(3)
                }
                .padding(18)
                .background(Color.cardSurface)
                .clipShape(RoundedRectangle(cornerRadius: 16))
                .overlay(
                    RoundedRectangle(cornerRadius: 16)
                        .stroke(Color.glassBorder, lineWidth: 1)
                )
                .padding(.horizontal, 20)

                Spacer()
            }
        }
    }
}
