import SwiftUI
import SwiftData

@main
public struct ChooseAVibeApp: App {
    public init() {}

    public var body: some Scene {
        WindowGroup {
            RootCoordinatorView()
        }
        .modelContainer(for: [CoupleProfile.self, AdventureSessionRecord.self])
    }
}

// MARK: - Корневой координатор приложения
public struct RootCoordinatorView: View {
    @Environment(\.modelContext) private var modelContext
    @Query private var profiles: [CoupleProfile]
    @Query(sort: \AdventureSessionRecord.startedAt, order: .reverse) private var sessions: [AdventureSessionRecord]

    @State private var selectedTab: Int = 0
    @State private var fullScreenSession: AdventureSessionRecord?

    public init() {}

    private var profile: CoupleProfile? {
        profiles.first
    }

    public var body: some View {
        Group {
            if let profile = profile {
                if !profile.hasCompletedOnboarding {
                    OnboardingFlowView(profile: profile)
                } else {
                    mainTabView
                }
            } else {
                ZStack {
                    AppTheme.midnightBg.ignoresSafeArea()
                    ProgressView()
                        .tint(AppTheme.primaryRose)
                }
            }
        }
        .preferredColorScheme(.dark)
        .onAppear {
            ensureInitialProfile()
        }
        .fullScreenCover(item: $fullScreenSession) { session in
            if let profile = profile, let adv = CatalogRepository.shared.adventure(by: session.adventureId) {
                ScratchCardContainer(
                    session: session,
                    adventure: adv,
                    profile: profile,
                    onDismiss: {
                        fullScreenSession = nil
                    }
                )
            }
        }
    }

    // MARK: - Главный TabView из 4 вкладок (Apple HIG iOS 18)
    private var mainTabView: some View {
        TabView(selection: $selectedTab) {
            TodayView()
                .tabItem {
                    Label("Сегодня", systemImage: "sparkles")
                }
                .tag(0)

            JourneyMapView()
                .tabItem {
                    Label("Путь", systemImage: "map.fill")
                }
                .tag(1)

            CollectionView()
                .tabItem {
                    Label("Коллекция", systemImage: "trophy.fill")
                }
                .tag(2)

            SettingsView()
                .tabItem {
                    Label("Пара", systemImage: "person.2.fill")
                }
                .tag(3)
        }
        .tint(AppTheme.primaryRose)
    }

    private func ensureInitialProfile() {
        if profiles.isEmpty {
            let initialProfile = CoupleProfile(
                partnerOneName: "Алишер",
                partnerTwoName: "Дана",
                totalXP: 0,
                unlockedAchievementIds: [],
                selectedScratchThemeId: "classic_silver",
                excludedBoundaryRawValues: [],
                hasCompletedOnboarding: false
            )
            modelContext.insert(initialProfile)
            try? modelContext.save()
        }
    }
}
