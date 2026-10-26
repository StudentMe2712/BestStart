import SwiftUI
import SwiftData

public struct OnboardingFlowView: View {
    @Environment(\.modelContext) private var modelContext
    @Bindable var profile: CoupleProfile

    @State private var currentStep: Int = 1
    @State private var partnerOne: String = ""
    @State private var partnerTwo: String = ""
    @State private var selectedBoundaries: Set<BoundaryTag> = []

    public init(profile: CoupleProfile) {
        self.profile = profile
    }

    public var body: some View {
        ZStack {
            // Фон приложения
            AppTheme.midnightBg
                .ignoresSafeArea()

            // Декоративные световые круги фона
            GeometryReader { geo in
                Circle()
                    .fill(AppTheme.primaryRose.opacity(0.18))
                    .frame(width: 320, height: 320)
                    .blur(radius: 60)
                    .offset(x: -80, y: -60)

                Circle()
                    .fill(Color.purple.opacity(0.18))
                    .frame(width: 300, height: 300)
                    .blur(radius: 70)
                    .offset(x: geo.size.width - 180, y: geo.size.height - 240)
            }

            VStack(spacing: 24) {
                // Индикатор шагов
                HStack(spacing: 8) {
                    ForEach(1...4, id: \.self) { step in
                        Capsule()
                            .fill(step <= currentStep ? AppTheme.primaryRose : Color.white.opacity(0.2))
                            .frame(height: 5)
                            .animation(.spring(duration: 0.3), value: currentStep)
                    }
                }
                .padding(.horizontal, 28)
                .padding(.top, 16)

                // Содержимое текущего шага
                TabView(selection: $currentStep) {
                    stepOneWelcomeView
                        .tag(1)

                    stepTwoNamesView
                        .tag(2)

                    stepThreeComfortFilterView
                        .tag(3)

                    stepFourReadyView
                        .tag(4)
                }
                .tabViewStyle(.page(indexDisplayMode: .never))
                .animation(.easeInOut(duration: 0.35), value: currentStep)

                // Кнопки управления внизу
                bottomNavigationButtons
                    .padding(.horizontal, 24)
                    .padding(.bottom, 24)
            }
        }
        .onAppear {
            partnerOne = profile.partnerOneName.isEmpty ? "Алишер" : profile.partnerOneName
            partnerTwo = profile.partnerTwoName.isEmpty ? "Дана" : profile.partnerTwoName
            selectedBoundaries = profile.excludedBoundariesSet
        }
    }

    // MARK: - Шаг 1: Приветствие
    private var stepOneWelcomeView: some View {
        VStack(spacing: 28) {
            Spacer()

            ZStack {
                Circle()
                    .fill(AppTheme.heroGradient.opacity(0.25))
                    .frame(width: 140, height: 140)

                Image(systemName: "sparkles.rectangle.stack.fill")
                    .font(.system(size: 64))
                    .foregroundStyle(AppTheme.heroGradient)
            }

            VStack(spacing: 12) {
                Text("Choose a Vibe")
                    .font(.system(size: 16, weight: .bold, design: .rounded))
                    .textCase(.uppercase)
                    .tracking(2)
                    .foregroundStyle(AppTheme.warmGold)

                Text("Превратите вечер\nв приключение для двоих")
                    .font(.system(size: 28, weight: .bold, design: .rounded))
                    .multilineTextAlignment(.center)
                    .foregroundStyle(.white)

                Text("Выбирайте настроение, стирайте защитный слой карточек и проходите 4 неожиданных задания прямо сегодня.")
                    .font(.system(size: 16, weight: .regular))
                    .multilineTextAlignment(.center)
                    .foregroundStyle(AppTheme.softText)
                    .lineSpacing(4)
                    .padding(.horizontal, 16)
            }

            Spacer()
        }
        .padding(.horizontal, 20)
    }

    // MARK: - Шаг 2: Имена партнёров
    private var stepTwoNamesView: some View {
        VStack(spacing: 24) {
            Spacer()

            VStack(spacing: 8) {
                Text("Как вас зовут?")
                    .font(.system(size: 26, weight: .bold, design: .rounded))
                    .foregroundStyle(.white)

                Text("Телефон будет переходить из рук в руки: нечётные карточки стирает первый партнёр, чётные — второй.")
                    .font(.system(size: 15))
                    .multilineTextAlignment(.center)
                    .foregroundStyle(AppTheme.softText)
                    .padding(.horizontal, 12)
            }

            VStack(spacing: 16) {
                VStack(alignment: .leading, spacing: 8) {
                    Label("Партнёр 1 (Этапы 1 и 3)", systemImage: "person.fill")
                        .font(.system(size: 13, weight: .medium))
                        .foregroundStyle(AppTheme.warmGold)

                    TextField("Алишер", text: $partnerOne)
                        .padding()
                        .background(AppTheme.cardSurface)
                        .clipShape(RoundedRectangle(cornerRadius: 14))
                        .overlay(
                            RoundedRectangle(cornerRadius: 14)
                                .stroke(Color.white.opacity(0.12), lineWidth: 1)
                        )
                        .foregroundStyle(.white)
                }

                VStack(alignment: .leading, spacing: 8) {
                    Label("Партнёр 2 (Этапы 2 и 4)", systemImage: "person.2.fill")
                        .font(.system(size: 13, weight: .medium))
                        .foregroundStyle(AppTheme.primaryRose)

                    TextField("Дана", text: $partnerTwo)
                        .padding()
                        .background(AppTheme.cardSurface)
                        .clipShape(RoundedRectangle(cornerRadius: 14))
                        .overlay(
                            RoundedRectangle(cornerRadius: 14)
                                .stroke(Color.white.opacity(0.12), lineWidth: 1)
                        )
                        .foregroundStyle(.white)
                }
            }
            .padding(.horizontal, 8)

            Spacer()
        }
        .padding(.horizontal, 20)
    }

    // MARK: - Шаг 3: Комфорт-фильтр
    private var stepThreeComfortFilterView: some View {
        VStack(spacing: 20) {
            VStack(spacing: 8) {
                Text("Комфорт-фильтр")
                    .font(.system(size: 26, weight: .bold, design: .rounded))
                    .foregroundStyle(.white)

                Text("Отметьте то, чего вы точно не хотите в заданиях. Мы никогда не предложим такие карточки.")
                    .font(.system(size: 15))
                    .multilineTextAlignment(.center)
                    .foregroundStyle(AppTheme.softText)
                    .padding(.horizontal, 12)
            }
            .padding(.top, 16)

            VStack(spacing: 12) {
                ForEach(BoundaryTag.allCases) { tag in
                    let isExcluded = selectedBoundaries.contains(tag)
                    Button {
                        AppTheme.hapticSoft()
                        if isExcluded {
                            selectedBoundaries.remove(tag)
                        } else {
                            selectedBoundaries.insert(tag)
                        }
                    } label: {
                        HStack(spacing: 14) {
                            Image(systemName: tag.systemIcon)
                                .font(.system(size: 20))
                                .foregroundStyle(isExcluded ? AppTheme.primaryRose : AppTheme.softText)
                                .frame(width: 28)

                            Text(tag.rawValue)
                                .font(.system(size: 16, weight: .medium))
                                .foregroundStyle(.white)

                            Spacer()

                            Image(systemName: isExcluded ? "checkmark.circle.fill" : "circle")
                                .font(.system(size: 22))
                                .foregroundStyle(isExcluded ? AppTheme.primaryRose : Color.white.opacity(0.3))
                        }
                        .padding()
                        .background(isExcluded ? AppTheme.cardSurfaceLight : AppTheme.cardSurface)
                        .clipShape(RoundedRectangle(cornerRadius: 16))
                        .overlay(
                            RoundedRectangle(cornerRadius: 16)
                                .stroke(isExcluded ? AppTheme.primaryRose.opacity(0.6) : Color.white.opacity(0.08), lineWidth: 1)
                        )
                    }
                    .buttonStyle(.plain)
                }
            }

            Spacer()
        }
        .padding(.horizontal, 20)
    }

    // MARK: - Шаг 4: Финал онбординга
    private var stepFourReadyView: some View {
        VStack(spacing: 24) {
            Spacer()

            ZStack {
                Circle()
                    .fill(AppTheme.warmGold.opacity(0.2))
                    .frame(width: 140, height: 140)

                Image(systemName: "map.fill")
                    .font(.system(size: 64))
                    .foregroundStyle(AppTheme.goldGradient)
            }

            VStack(spacing: 12) {
                Text("Всё готово для старта!")
                    .font(.system(size: 28, weight: .bold, design: .rounded))
                    .multilineTextAlignment(.center)
                    .foregroundStyle(.white)

                Text("Команда: \(partnerOne.isEmpty ? "Партнёр 1" : partnerOne) & \(partnerTwo.isEmpty ? "Партнёр 2" : partnerTwo)")
                    .font(.system(size: 17, weight: .semibold))
                    .foregroundStyle(AppTheme.warmGold)

                Text("Вас ждут 3 сюжетные главы, 12 приключений и стираемые скретч-карты. Начните с первого вечера прямо сейчас!")
                    .font(.system(size: 15))
                    .multilineTextAlignment(.center)
                    .foregroundStyle(AppTheme.softText)
                    .lineSpacing(4)
                    .padding(.horizontal, 16)
            }

            Spacer()
        }
        .padding(.horizontal, 20)
    }

    // MARK: - Нижняя навигация
    private var bottomNavigationButtons: some View {
        HStack(spacing: 14) {
            if currentStep > 1 {
                Button {
                    AppTheme.hapticSoft()
                    withAnimation {
                        currentStep -= 1
                    }
                } label: {
                    Text("Назад")
                        .font(.system(size: 16, weight: .semibold))
                        .foregroundStyle(AppTheme.softText)
                        .frame(maxWidth: .infinity)
                        .frame(height: 54)
                        .background(AppTheme.cardSurface)
                        .clipShape(RoundedRectangle(cornerRadius: 16))
                }
            }

            Button {
                AppTheme.hapticMedium()
                if currentStep < 4 {
                    withAnimation {
                        currentStep += 1
                    }
                } else {
                    finishOnboarding()
                }
            } label: {
                HStack(spacing: 8) {
                    Text(currentStep == 4 ? "Открыть карту приключений" : "Далее")
                        .font(.system(size: 17, weight: .bold))

                    Image(systemName: currentStep == 4 ? "sparkles" : "arrow.right")
                        .font(.system(size: 16, weight: .bold))
                }
                .foregroundStyle(.white)
                .frame(maxWidth: .infinity)
                .frame(height: 54)
                .background(AppTheme.heroGradient)
                .clipShape(RoundedRectangle(cornerRadius: 16))
                .shadow(color: AppTheme.primaryRose.opacity(0.35), radius: 10, y: 5)
            }
        }
    }

    private func finishOnboarding() {
        profile.partnerOneName = partnerOne.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? "Алишер" : partnerOne
        profile.partnerTwoName = partnerTwo.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? "Дана" : partnerTwo
        profile.excludedBoundaries = Array(selectedBoundaries)
        profile.hasCompletedOnboarding = true
        profile.updatedAt = .now
        try? modelContext.save()
        AppTheme.hapticSuccess()
    }
}
