import SwiftUI
import SwiftData

public struct SettingsView: View {
    @Environment(\.modelContext) private var modelContext
    @Query private var profiles: [CoupleProfile]
    @Query private var allSessions: [AdventureSessionRecord]

    @State private var partnerOneName: String = ""
    @State private var partnerTwoName: String = ""
    @State private var excludedBoundaries: Set<BoundaryTag> = []
    @AppStorage("hapticsEnabled") private var hapticsEnabled: Bool = true
    @AppStorage("soundEffectsEnabled") private var soundEffectsEnabled: Bool = true

    @State private var showingResetAlert: Bool = false
    @State private var showingSaveConfirmation: Bool = false

    public init() {}

    private var profile: CoupleProfile {
        profiles.first ?? CoupleProfile()
    }

    public var body: some View {
        NavigationStack {
            ZStack {
                AppTheme.midnightBg
                    .ignoresSafeArea()

                ScrollView(showsIndicators: false) {
                    VStack(spacing: 24) {
                        // 1. Имена партнёров
                        partnersSection

                        // 2. Личные границы (Комфорт-фильтр)
                        comfortFilterSection

                        // 3. Звук и тактильный отклик
                        hapticsAndSoundSection

                        // 4. О приложении
                        aboutSection

                        // 5. Опасная зона (Сброс прогресса)
                        dangerZoneSection

                        Color.clear.frame(height: 20)
                    }
                    .padding(20)
                }
            }
            .navigationTitle("Настройки")
            .navigationBarTitleDisplayMode(.inline)
            .onAppear {
                partnerOneName = profile.partnerOneName
                partnerTwoName = profile.partnerTwoName
                excludedBoundaries = profile.excludedBoundariesSet
            }
            .alert("Сбросить весь прогресс?", isPresented: $showingResetAlert) {
                Button("Отмена", role: .cancel) {}
                Button("Сбросить всё", role: .destructive) {
                    resetAllProgress()
                }
            } message: {
                Text("Это действие удалит весь накопленный опыт, открытые значки и историю прохождений. Онбординг запустится заново.")
            }
            .overlay(alignment: .bottom) {
                if showingSaveConfirmation {
                    Text("Настройки сохранены")
                        .font(.system(size: 14, weight: .bold))
                        .foregroundStyle(.white)
                        .padding(.horizontal, 16)
                        .padding(.vertical, 10)
                        .background(Color.black.opacity(0.85))
                        .clipShape(Capsule())
                        .transition(.move(edge: .bottom).combined(with: .opacity))
                        .padding(.bottom, 24)
                }
            }
        }
    }

    // MARK: - 1. Имена партнёров
    private var partnersSection: some View {
        VStack(alignment: .leading, spacing: 14) {
            Label("Имена пары", systemImage: "person.2.fill")
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(.white)

            VStack(spacing: 12) {
                VStack(alignment: .leading, spacing: 6) {
                    Text("Партнёр 1 (нечётные задания)")
                        .font(.system(size: 12))
                        .foregroundStyle(AppTheme.softText)

                    TextField("Имя 1", text: $partnerOneName)
                        .padding()
                        .background(Color.white.opacity(0.06))
                        .clipShape(RoundedRectangle(cornerRadius: 12))
                        .foregroundStyle(.white)
                }

                VStack(alignment: .leading, spacing: 6) {
                    Text("Партнёр 2 (чётные задания)")
                        .font(.system(size: 12))
                        .foregroundStyle(AppTheme.softText)

                    TextField("Имя 2", text: $partnerTwoName)
                        .padding()
                        .background(Color.white.opacity(0.06))
                        .clipShape(RoundedRectangle(cornerRadius: 12))
                        .foregroundStyle(.white)
                }

                Button {
                    saveNames()
                } label: {
                    Text("Сохранить имена")
                        .font(.system(size: 14, weight: .bold))
                        .foregroundStyle(.white)
                        .frame(maxWidth: .infinity)
                        .frame(height: 44)
                        .background(AppTheme.heroGradient)
                        .clipShape(RoundedRectangle(cornerRadius: 12))
                }
                .padding(.top, 4)
            }
            .padding(16)
            .background(AppTheme.cardSurface)
            .clipShape(RoundedRectangle(cornerRadius: 18))
        }
    }

    private func saveNames() {
        AppTheme.hapticSuccess()
        let trimmed1 = partnerOneName.trimmingCharacters(in: .whitespacesAndNewlines)
        let trimmed2 = partnerTwoName.trimmingCharacters(in: .whitespacesAndNewlines)
        profile.partnerOneName = trimmed1.isEmpty ? "Алишер" : trimmed1
        profile.partnerTwoName = trimmed2.isEmpty ? "Дана" : trimmed2
        profile.updatedAt = .now
        try? modelContext.save()
        showToast()
    }

    // MARK: - 2. Комфорт-фильтр
    private var comfortFilterSection: some View {
        VStack(alignment: .leading, spacing: 14) {
            Label("Комфорт-фильтр", systemImage: "shield.lefthalf.filled")
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(.white)

            VStack(spacing: 10) {
                ForEach(BoundaryTag.allCases) { tag in
                    let isExcluded = excludedBoundaries.contains(tag)
                    Toggle(isOn: Binding(
                        get: { isExcluded },
                        set: { shouldExclude in
                            AppTheme.hapticSoft()
                            if shouldExclude {
                                excludedBoundaries.insert(tag)
                            } else {
                                excludedBoundaries.remove(tag)
                            }
                            profile.excludedBoundaries = Array(excludedBoundaries)
                            try? modelContext.save()
                        }
                    )) {
                        HStack(spacing: 12) {
                            Image(systemName: tag.systemIcon)
                                .font(.system(size: 16))
                                .foregroundStyle(isExcluded ? AppTheme.primaryRose : AppTheme.softText)

                            Text(tag.rawValue)
                                .font(.system(size: 14, weight: .medium))
                                .foregroundStyle(.white)
                        }
                    }
                    .tint(AppTheme.primaryRose)
                    .padding(.vertical, 4)

                    if tag != BoundaryTag.allCases.last {
                        Divider().background(Color.white.opacity(0.06))
                    }
                }
            }
            .padding(16)
            .background(AppTheme.cardSurface)
            .clipShape(RoundedRectangle(cornerRadius: 18))
        }
    }

    // MARK: - 3. Звук и вибрация
    private var hapticsAndSoundSection: some View {
        VStack(alignment: .leading, spacing: 14) {
            Label("Отклик и эффекты", systemImage: "iphone.radiowaves.left.and.right")
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(.white)

            VStack(spacing: 10) {
                Toggle(isOn: $hapticsEnabled) {
                    HStack(spacing: 12) {
                        Image(systemName: "hand.tap")
                            .font(.system(size: 16))
                            .foregroundStyle(AppTheme.warmGold)

                        Text("Тактильный отклик (Haptics)")
                            .font(.system(size: 14, weight: .medium))
                            .foregroundStyle(.white)
                    }
                }
                .tint(AppTheme.warmGold)
                .padding(.vertical, 4)

                Divider().background(Color.white.opacity(0.06))

                Toggle(isOn: $soundEffectsEnabled) {
                    HStack(spacing: 12) {
                        Image(systemName: "speaker.wave.2")
                            .font(.system(size: 16))
                            .foregroundStyle(AppTheme.warmGold)

                        Text("Звуковые эффекты")
                            .font(.system(size: 14, weight: .medium))
                            .foregroundStyle(.white)
                    }
                }
                .tint(AppTheme.warmGold)
                .padding(.vertical, 4)
            }
            .padding(16)
            .background(AppTheme.cardSurface)
            .clipShape(RoundedRectangle(cornerRadius: 18))
        }
    }

    // MARK: - 4. О приложении
    private var aboutSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("Choose a Vibe · Выбери Вайб")
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(.white)
                Spacer()
                Text("Версия 1.0 (MVP)")
                    .font(.system(size: 12))
                    .foregroundStyle(AppTheme.softText)
            }

            Text("Каталог: 12 приключений • 48 карточек • 8 значков • Валюта: Казахстанский тенге (₸)")
                .font(.system(size: 11))
                .foregroundStyle(AppTheme.softText.opacity(0.7))
                .lineSpacing(2)
        }
        .padding(14)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(AppTheme.cardSurface.opacity(0.5))
        .clipShape(RoundedRectangle(cornerRadius: 14))
    }

    // MARK: - 5. Сброс прогресса
    private var dangerZoneSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Button {
                AppTheme.hapticWarning()
                showingResetAlert = true
            } label: {
                HStack(spacing: 8) {
                    Image(systemName: "trash.fill")
                        .font(.system(size: 14))

                    Text("Сбросить весь прогресс")
                        .font(.system(size: 14, weight: .bold))
                }
                .foregroundStyle(Color.red.opacity(0.9))
                .frame(maxWidth: .infinity)
                .frame(height: 48)
                .background(Color.red.opacity(0.12))
                .clipShape(RoundedRectangle(cornerRadius: 14))
                .overlay(
                    RoundedRectangle(cornerRadius: 14)
                        .stroke(Color.red.opacity(0.3), lineWidth: 1)
                )
            }
        }
    }

    private func resetAllProgress() {
        AppTheme.hapticMedium()
        for session in allSessions {
            modelContext.delete(session)
        }
        profile.totalXP = 0
        profile.unlockedAchievementIds = []
        profile.selectedScratchThemeId = "classic_silver"
        profile.excludedBoundaryRawValues = []
        profile.hasCompletedOnboarding = false
        profile.updatedAt = .now
        try? modelContext.save()
    }

    private func showToast() {
        withAnimation {
            showingSaveConfirmation = true
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 2) {
            withAnimation {
                showingSaveConfirmation = false
            }
        }
    }
}
