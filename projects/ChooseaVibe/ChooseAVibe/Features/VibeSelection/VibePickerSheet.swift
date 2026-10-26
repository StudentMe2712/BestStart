import SwiftUI

public struct VibePickerSheet: View {
    @Environment(\.dismiss) private var dismiss

    public let profile: CoupleProfile
    public let completedAdventureIds: Set<String>
    public let onAdventureSelected: (AdventureDefinition) -> Void

    @State private var selectedVibes: Set<VibeTag> = [.romantic]
    @State private var selectedDuration: DurationTier = .medium
    @State private var selectedBudget: BudgetTier = .low
    @State private var selectedLocation: LocationType = .home

    public init(
        profile: CoupleProfile,
        completedAdventureIds: Set<String>,
        onAdventureSelected: @escaping (AdventureDefinition) -> Void
    ) {
        self.profile = profile
        self.completedAdventureIds = completedAdventureIds
        self.onAdventureSelected = onAdventureSelected
    }

    public var body: some View {
        NavigationStack {
            ZStack {
                AppTheme.midnightBg
                    .ignoresSafeArea()

                ScrollView(showsIndicators: false) {
                    VStack(alignment: .leading, spacing: 24) {
                        // 1. Секция: Настроение (до 2)
                        vibeSection

                        // 2. Секция: Время
                        durationSection

                        // 3. Секция: Бюджет
                        budgetSection

                        // 4. Секция: Место
                        locationSection

                        // Кнопка подбора
                        surpriseButton
                            .padding(.top, 12)
                            .padding(.bottom, 24)
                    }
                    .padding(20)
                }
            }
            .navigationTitle("Настройка вечера")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Закрыть") {
                        dismiss()
                    }
                    .foregroundStyle(AppTheme.softText)
                }
            }
        }
    }

    // MARK: - 1. Настроение
    private var vibeSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text("Настроение")
                    .font(.system(size: 17, weight: .bold))
                    .foregroundStyle(.white)

                Text("(выберите 1 или 2)")
                    .font(.system(size: 13))
                    .foregroundStyle(AppTheme.softText)
            }

            VStack(spacing: 8) {
                ForEach(VibeTag.allCases) { vibe in
                    let isSelected = selectedVibes.contains(vibe)
                    Button {
                        AppTheme.hapticSoft()
                        toggleVibe(vibe)
                    } label: {
                        HStack(spacing: 12) {
                            Image(systemName: vibe.systemIcon)
                                .font(.system(size: 18))
                                .foregroundStyle(isSelected ? .white : AppTheme.softText)

                            Text(vibe.rawValue)
                                .font(.system(size: 15, weight: .semibold))
                                .foregroundStyle(isSelected ? .white : AppTheme.softText)

                            Spacer()

                            if isSelected {
                                Image(systemName: "checkmark")
                                    .font(.system(size: 14, weight: .bold))
                                    .foregroundStyle(.white)
                            }
                        }
                        .padding(.horizontal, 16)
                        .padding(.vertical, 12)
                        .background(isSelected ? AppTheme.gradient(for: vibe) : AppTheme.cardSurface)
                        .clipShape(RoundedRectangle(cornerRadius: 14))
                        .overlay(
                            RoundedRectangle(cornerRadius: 14)
                                .stroke(isSelected ? Color.clear : Color.white.opacity(0.08), lineWidth: 1)
                        )
                    }
                    .buttonStyle(.plain)
                }
            }
        }
    }

    private func toggleVibe(_ vibe: VibeTag) {
        if selectedVibes.contains(vibe) {
            if selectedVibes.count > 1 {
                selectedVibes.remove(vibe)
            }
        } else {
            if selectedVibes.count >= 2 {
                if let first = selectedVibes.first {
                    selectedVibes.remove(first)
                }
            }
            selectedVibes.insert(vibe)
        }
    }

    // MARK: - 2. Время
    private var durationSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Сколько у вас времени?")
                .font(.system(size: 17, weight: .bold))
                .foregroundStyle(.white)

            HStack(spacing: 8) {
                ForEach(DurationTier.allCases) { dur in
                    let isSelected = selectedDuration == dur
                    Button {
                        AppTheme.hapticSoft()
                        selectedDuration = dur
                    } label: {
                        VStack(spacing: 6) {
                            Image(systemName: dur.systemIcon)
                                .font(.system(size: 16))

                            Text(dur.rawValue)
                                .font(.system(size: 12, weight: .semibold))
                        }
                        .foregroundStyle(isSelected ? .white : AppTheme.softText)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 14)
                        .background(isSelected ? AppTheme.primaryRose : AppTheme.cardSurface)
                        .clipShape(RoundedRectangle(cornerRadius: 14))
                        .overlay(
                            RoundedRectangle(cornerRadius: 14)
                                .stroke(isSelected ? Color.clear : Color.white.opacity(0.08), lineWidth: 1)
                        )
                    }
                    .buttonStyle(.plain)
                }
            }
        }
    }

    // MARK: - 3. Бюджет (₸)
    private var budgetSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Бюджет вечера (₸)")
                .font(.system(size: 17, weight: .bold))
                .foregroundStyle(.white)

            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 8) {
                ForEach(BudgetTier.allCases) { tier in
                    let isSelected = selectedBudget == tier
                    Button {
                        AppTheme.hapticSoft()
                        selectedBudget = tier
                    } label: {
                        Text(tier.displayText)
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundStyle(isSelected ? .white : AppTheme.softText)
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 14)
                            .background(isSelected ? AppTheme.warmGold.opacity(0.85) : AppTheme.cardSurface)
                            .clipShape(RoundedRectangle(cornerRadius: 14))
                            .overlay(
                                RoundedRectangle(cornerRadius: 14)
                                    .stroke(isSelected ? Color.clear : Color.white.opacity(0.08), lineWidth: 1)
                            )
                    }
                    .buttonStyle(.plain)
                }
            }
        }
    }

    // MARK: - 4. Место
    private var locationSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Где вы хотите провести вечер?")
                .font(.system(size: 17, weight: .bold))
                .foregroundStyle(.white)

            HStack(spacing: 8) {
                ForEach(LocationType.allCases) { loc in
                    let isSelected = selectedLocation == loc
                    Button {
                        AppTheme.hapticSoft()
                        selectedLocation = loc
                    } label: {
                        HStack(spacing: 6) {
                            Image(systemName: loc.systemIcon)
                                .font(.system(size: 14))

                            Text(loc.rawValue)
                                .font(.system(size: 13, weight: .semibold))
                        }
                        .foregroundStyle(isSelected ? .white : AppTheme.softText)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 14)
                        .background(isSelected ? AppTheme.deepPlum.opacity(0.8) : AppTheme.cardSurface)
                        .clipShape(RoundedRectangle(cornerRadius: 14))
                        .overlay(
                            RoundedRectangle(cornerRadius: 14)
                                .stroke(isSelected ? AppTheme.primaryRose.opacity(0.6) : Color.white.opacity(0.08), lineWidth: 1)
                        )
                    }
                    .buttonStyle(.plain)
                }
            }
        }
    }

    // MARK: - Кнопка "Удиви нас"
    private var surpriseButton: some View {
        Button {
            AppTheme.hapticMedium()
            runMatching()
        } label: {
            HStack(spacing: 10) {
                Image(systemName: "sparkles")
                    .font(.system(size: 18, weight: .bold))

                Text("Удиви нас")
                    .font(.system(size: 17, weight: .bold))
            }
            .foregroundStyle(.white)
            .frame(maxWidth: .infinity)
            .frame(height: 56)
            .background(AppTheme.heroGradient)
            .clipShape(RoundedRectangle(cornerRadius: 16))
            .shadow(color: AppTheme.primaryRose.opacity(0.4), radius: 12, y: 5)
        }
    }

    private func runMatching() {
        let criteria = VibeCriteria(
            vibes: selectedVibes,
            duration: selectedDuration,
            budget: selectedBudget,
            location: selectedLocation
        )
        let adventures = CatalogRepository.shared.adventures
        let match = VibeMatcherService.shared.matchAdventure(
            criteria: criteria,
            adventures: adventures,
            completedIds: completedAdventureIds,
            excludedBoundaries: profile.excludedBoundariesSet,
            currentChapterId: "ch_01"
        )
        onAdventureSelected(match.adventure)
    }
}
