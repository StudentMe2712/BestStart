import SwiftUI

/// Интерактивный гид из 3 слайдов с демонстрацией механик Choose a Vibe:
/// 1. Выбор настроения
/// 2. Стирание скретч-слоя пальцем
/// 3. Pass & Play и прогрессия вдвоём
public struct InteractiveWalkthroughSheet: View {
    @Environment(\.dismiss) private var dismiss

    @State private var currentSlide: Int = 0

    // Состояния интерактива Слайда 1 (выбор вайба)
    @State private var previewSelectedVibe: VibeTag = .romantic

    // Состояния интерактива Слайда 2 (скретч-демо)
    @State private var scratchPoints: [[CGPoint]] = []
    @State private var currentStroke: [CGPoint] = []
    @State private var isScratchRevealed: Bool = false
    @State private var scratchedCellsCount: Int = 0

    public init() {}

    public var body: some View {
        NavigationStack {
            ZStack {
                AppTheme.midnightBg
                    .ignoresSafeArea()

                VStack(spacing: 0) {
                    // Основной пейджер слайдов
                    TabView(selection: $currentSlide) {
                        slideOneVibePicker
                            .tag(0)

                        slideTwoScratchDemo
                            .tag(1)

                        slideThreeCoopProgress
                            .tag(2)
                    }
                    .tabViewStyle(.page(indexDisplayMode: .always))
                    .animation(.easeInOut(duration: 0.3), value: currentSlide)

                    // Нижняя панель действий
                    bottomNavigationControls
                        .padding(.horizontal, 24)
                        .padding(.bottom, 24)
                        .padding(.top, 12)
                }
            }
            .navigationTitle("Как играть")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        dismiss()
                    } label: {
                        Image(systemName: "xmark.circle.fill")
                            .font(.system(size: 22))
                            .foregroundStyle(AppTheme.softText.opacity(0.8))
                    }
                }
            }
        }
    }

    // MARK: - Слайд 1: Выбор настроения вечера
    private var slideOneVibePicker: some View {
        VStack(spacing: 22) {
            Spacer(minLength: 10)

            // Заголовок слайда
            VStack(spacing: 8) {
                Text("ШАГ 1 ИЗ 3")
                    .font(.system(size: 12, weight: .heavy, design: .rounded))
                    .tracking(1.5)
                    .foregroundStyle(AppTheme.warmGold)

                Text("Выберите настроение")
                    .font(.system(size: 24, weight: .bold, design: .rounded))
                    .foregroundStyle(.white)

                Text("Укажите вайб, свободное время и комфортный бюджет. Алгоритм бережно подберёт маршрут вечера без спойлеров.")
                    .font(.system(size: 15))
                    .foregroundStyle(AppTheme.softText)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 20)
            }

            // Интерактивная витрина карточек-вайбов
            VStack(spacing: 12) {
                HStack(spacing: 10) {
                    vibePillButton(for: .romantic)
                    vibePillButton(for: .playful)
                }

                HStack(spacing: 10) {
                    vibePillButton(for: .cozy)
                    vibePillButton(for: .spontaneous)
                    vibePillButton(for: .deep)
                }
            }
            .padding(.horizontal, 16)

            // Динамическая карточка предпросмотра выбранного вайба
            let palette = AppTheme.palette(for: previewSelectedVibe)
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    ZStack {
                        Circle()
                            .fill(palette.accentGradient)
                            .frame(width: 38, height: 38)

                        Image(systemName: palette.iconName)
                            .font(.system(size: 18))
                            .foregroundStyle(.white)
                    }

                    VStack(alignment: .leading, spacing: 2) {
                        Text(previewSelectedVibe.displayName)
                            .font(.system(size: 16, weight: .bold, design: .rounded))
                            .foregroundStyle(.white)

                        Text("Ведущий акцент сценария")
                            .font(.system(size: 12))
                            .foregroundStyle(AppTheme.softText)
                    }

                    Spacer()

                    Image(systemName: "checkmark.seal.fill")
                        .foregroundStyle(AppTheme.warmGold)
                        .font(.system(size: 20))
                }

                Text(previewSelectedVibe.description)
                    .font(.system(size: 14))
                    .foregroundStyle(.white.opacity(0.85))
            }
            .padding(18)
            .background(AppTheme.cardSurface)
            .clipShape(RoundedRectangle(cornerRadius: 20))
            .overlay(
                RoundedRectangle(cornerRadius: 20)
                    .stroke(palette.primary.opacity(0.5), lineWidth: 1.5)
            )
            .padding(.horizontal, 24)
            .shadow(color: palette.glowColor, radius: 16)

            Spacer(minLength: 20)
        }
    }

    private func vibePillButton(for vibe: VibeTag) -> some View {
        let isSelected = previewSelectedVibe == vibe
        let palette = AppTheme.palette(for: vibe)

        return Button {
            AppTheme.hapticLight()
            withAnimation(.spring(response: 0.3, dampingFraction: 0.7)) {
                previewSelectedVibe = vibe
            }
        } label: {
            HStack(spacing: 6) {
                Image(systemName: vibe.iconName)
                    .font(.system(size: 13, weight: .bold))

                Text(vibe.displayName)
                    .font(.system(size: 14, weight: .semibold, design: .rounded))
            }
            .foregroundStyle(isSelected ? .white : AppTheme.softText)
            .padding(.horizontal, 14)
            .padding(.vertical, 10)
            .background(
                isSelected ? palette.accentGradient : LinearGradient(colors: [Color.white.opacity(0.08), Color.white.opacity(0.04)], startPoint: .top, endPoint: .bottom)
            )
            .clipShape(Capsule())
            .overlay(
                Capsule()
                    .stroke(isSelected ? palette.primary : Color.white.opacity(0.12), lineWidth: isSelected ? 1.5 : 1)
            )
            .scaleEffect(isSelected ? 1.05 : 1.0)
        }
        .buttonStyle(.plain)
    }

    // MARK: - Слайд 2: Интерактивный скретч-слой
    private var slideTwoScratchDemo: some View {
        VStack(spacing: 20) {
            Spacer(minLength: 10)

            // Заголовок слайда
            VStack(spacing: 8) {
                Text("ШАГ 2 ИЗ 3")
                    .font(.system(size: 12, weight: .heavy, design: .rounded))
                    .tracking(1.5)
                    .foregroundStyle(AppTheme.warmGold)

                Text("Сотрите защитный слой")
                    .font(.system(size: 24, weight: .bold, design: .rounded))
                    .foregroundStyle(.white)

                Text("Попробуйте стереть карточку прямо сейчас! Проведите пальцем по серебристому покрытию, чтобы раскрыть секрет.")
                    .font(.system(size: 15))
                    .foregroundStyle(AppTheme.softText)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 20)
            }

            // Интерактивная карточка-демо со скретч-эффектом
            ZStack {
                // Подложка с секретом (открывается при стирании)
                VStack(spacing: 12) {
                    ZStack {
                        Circle()
                            .fill(AppTheme.Gradients.goldReward)
                            .frame(width: 52, height: 52)

                        Image(systemName: "sparkles")
                            .font(.system(size: 24, weight: .bold))
                            .foregroundStyle(.black)
                    }

                    Text("Свидание ждёт вас! ✨")
                        .font(.system(size: 18, weight: .bold, design: .rounded))
                        .foregroundStyle(.white)

                    Text("Каждая карточка хранит интригу до последнего момента.")
                        .font(.system(size: 13))
                        .foregroundStyle(AppTheme.softText)
                        .multilineTextAlignment(.center)
                        .padding(.horizontal, 16)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(
                    LinearGradient(
                        colors: [Color(red: 0.18, green: 0.12, blue: 0.28), Color(red: 0.10, green: 0.08, blue: 0.18)],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    )
                )

                // Защитный стираемый слой
                if !isScratchRevealed {
                    ZStack {
                        // Металлическое напыление
                        LinearGradient(
                            colors: [
                                Color(white: 0.88),
                                Color(white: 0.72),
                                Color(white: 0.84),
                                Color(white: 0.68)
                            ],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )

                        // Подсказка на рубашке
                        VStack(spacing: 8) {
                            Image(systemName: "hand.draw.fill")
                                .font(.system(size: 28))
                                .foregroundStyle(Color.black.opacity(0.6))

                            Text("Потрите пальцем здесь")
                                .font(.system(size: 14, weight: .bold, design: .rounded))
                                .foregroundStyle(Color.black.opacity(0.7))
                        }

                        // Стирание маской через Canvas
                        Canvas { context, _ in
                            for stroke in scratchPoints {
                                var path = Path()
                                path.addLines(stroke)
                                context.stroke(
                                    path,
                                    with: .color(.black),
                                    style: StrokeStyle(lineWidth: 42, lineCap: .round, lineJoin: .round)
                                )
                            }
                            if !currentStroke.isEmpty {
                                var activePath = Path()
                                activePath.addLines(currentStroke)
                                context.stroke(
                                    activePath,
                                    with: .color(.black),
                                    style: StrokeStyle(lineWidth: 42, lineCap: .round, lineJoin: .round)
                                )
                            }
                        }
                        .blendMode(.destinationOut)
                    }
                    .compositingGroup()
                    .gesture(
                        DragGesture(minimumDistance: 0)
                            .onChanged { value in
                                let loc = value.location
                                currentStroke.append(loc)
                                scratchedCellsCount += 1
                                if scratchedCellsCount % 4 == 0 {
                                    AppTheme.hapticSoft()
                                }
                                if scratchedCellsCount > 35 && !isScratchRevealed {
                                    AppTheme.hapticSuccess()
                                    withAnimation(.easeOut(duration: 0.4)) {
                                        isScratchRevealed = true
                                    }
                                }
                            }
                            .onEnded { _ in
                                if !currentStroke.isEmpty {
                                    scratchPoints.append(currentStroke)
                                    currentStroke = []
                                }
                            }
                    )
                }
            }
            .frame(height: 200)
            .clipShape(RoundedRectangle(cornerRadius: 22))
            .overlay(
                RoundedRectangle(cornerRadius: 22)
                    .stroke(Color.white.opacity(0.18), lineWidth: 1.5)
            )
            .shadow(color: AppTheme.primaryRose.opacity(0.25), radius: 16)
            .padding(.horizontal, 28)

            // Кнопка быстрого сброса/раскрытия демо
            Button {
                AppTheme.hapticLight()
                withAnimation(.spring(duration: 0.4)) {
                    isScratchRevealed.toggle()
                    if !isScratchRevealed {
                        scratchPoints = []
                        currentStroke = []
                        scratchedCellsCount = 0
                    }
                }
            } label: {
                HStack(spacing: 6) {
                    Image(systemName: isScratchRevealed ? "arrow.counterclockwise" : "wand.and.stars")
                    Text(isScratchRevealed ? "Попробовать снова" : "Открыть сразу")
                }
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(AppTheme.warmGold)
                .padding(.horizontal, 14)
                .padding(.vertical, 6)
                .background(AppTheme.warmGold.opacity(0.12))
                .clipShape(Capsule())
            }

            Spacer(minLength: 20)
        }
    }

    // MARK: - Слайд 3: Совместное прохождение и прогрессия
    private var slideThreeCoopProgress: some View {
        VStack(spacing: 22) {
            Spacer(minLength: 10)

            // Заголовок слайда
            VStack(spacing: 8) {
                Text("ШАГ 3 ИЗ 3")
                    .font(.system(size: 12, weight: .heavy, design: .rounded))
                    .tracking(1.5)
                    .foregroundStyle(AppTheme.warmGold)

                Text("Выполняйте шаги вдвоём")
                    .font(.system(size: 24, weight: .bold, design: .rounded))
                    .foregroundStyle(.white)

                Text("Передавайте телефон друг другу. 4 секретных этапа за вечер, очки опыта XP, коллекционные рубашки и значки.")
                    .font(.system(size: 15))
                    .foregroundStyle(AppTheme.softText)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 20)
            }

            // Интерактивная плашка очерёдности (Pass & Play)
            VStack(spacing: 16) {
                HStack(spacing: 12) {
                    partnerPill(name: "Партнёр 1", isCurrentTurn: true, icon: "person.crop.circle.fill")
                    Image(systemName: "arrow.left.arrow.right")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(AppTheme.softText)
                    partnerPill(name: "Партнёр 2", isCurrentTurn: false, icon: "person.crop.circle")
                }

                // 4 шага вечера
                HStack(spacing: 8) {
                    stepBubble(index: 1, title: "Разминка", isCompleted: true)
                    stepConnector
                    stepBubble(index: 2, title: "Секрет", isCompleted: true)
                    stepConnector
                    stepBubble(index: 3, title: "Действие", isCurrent: true)
                    stepConnector
                    stepBubble(index: 4, title: "Финал", isLocked: true)
                }
                .padding(.horizontal, 8)

                // Награда
                HStack(spacing: 8) {
                    Image(systemName: "trophy.fill")
                        .font(.system(size: 14))
                        .foregroundStyle(AppTheme.warmGold)

                    Text("+10 XP за карточку · +25 XP за вечер вдвоём")
                        .font(.system(size: 12, weight: .bold, design: .rounded))
                        .foregroundStyle(AppTheme.warmGold)
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 8)
                .background(AppTheme.warmGold.opacity(0.12))
                .clipShape(Capsule())
            }
            .padding(20)
            .background(AppTheme.cardSurface)
            .clipShape(RoundedRectangle(cornerRadius: 22))
            .overlay(
                RoundedRectangle(cornerRadius: 22)
                    .stroke(Color.white.opacity(0.10), lineWidth: 1)
            )
            .padding(.horizontal, 24)

            Spacer(minLength: 20)
        }
    }

    private func partnerPill(name: String, isCurrentTurn: Bool, icon: String) -> some View {
        HStack(spacing: 6) {
            Image(systemName: icon)
                .font(.system(size: 15))
            Text(name)
                .font(.system(size: 14, weight: .bold, design: .rounded))
        }
        .foregroundStyle(isCurrentTurn ? .white : AppTheme.softText)
        .padding(.horizontal, 14)
        .padding(.vertical, 8)
        .background(isCurrentTurn ? AppTheme.primaryRose : Color.white.opacity(0.06))
        .clipShape(Capsule())
        .shadow(color: isCurrentTurn ? AppTheme.primaryRose.opacity(0.4) : Color.clear, radius: 8)
    }

    private func stepBubble(index: Int, title: String, isCompleted: Bool = false, isCurrent: Bool = false, isLocked: Bool = false) -> some View {
        VStack(spacing: 4) {
            ZStack {
                Circle()
                    .fill(
                        isCompleted ? AnyShapeStyle(AppTheme.Gradients.goldReward) :
                        isCurrent ? AnyShapeStyle(AppTheme.heroGradient) :
                        AnyShapeStyle(Color.white.opacity(0.08))
                    )
                    .frame(width: 32, height: 32)

                if isCompleted {
                    Image(systemName: "checkmark")
                        .font(.system(size: 12, weight: .heavy))
                        .foregroundStyle(.black)
                } else if isCurrent {
                    Image(systemName: "sparkle")
                        .font(.system(size: 13, weight: .bold))
                        .foregroundStyle(.white)
                } else {
                    Image(systemName: "lock.fill")
                        .font(.system(size: 11))
                        .foregroundStyle(AppTheme.softText.opacity(0.6))
                }
            }

            Text("Шаг \(index)")
                .font(.system(size: 10, weight: .medium))
                .foregroundStyle(isCurrent ? .white : AppTheme.softText)
        }
    }

    private var stepConnector: some View {
        Rectangle()
            .fill(Color.white.opacity(0.15))
            .frame(height: 2)
            .frame(maxWidth: .infinity)
            .offset(y: -8)
    }

    // MARK: - Нижняя панель действий (Далее / Начать)
    private var bottomNavigationControls: some View {
        HStack(spacing: 12) {
            if currentSlide < 2 {
                Button("Пропустить") {
                    dismiss()
                }
                .font(.system(size: 15, weight: .semibold))
                .foregroundStyle(AppTheme.softText)
                .frame(width: 100, height: 52)

                Button {
                    AppTheme.hapticLight()
                    withAnimation {
                        currentSlide += 1
                    }
                } label: {
                    HStack(spacing: 8) {
                        Text("Далее")
                            .font(.system(size: 16, weight: .bold, design: .rounded))
                        Image(systemName: "arrow.right")
                            .font(.system(size: 14, weight: .bold))
                    }
                    .foregroundStyle(.white)
                    .frame(maxWidth: .infinity)
                    .frame(height: 52)
                    .background(AppTheme.heroGradient)
                    .clipShape(RoundedRectangle(cornerRadius: 16))
                    .shadow(color: AppTheme.primaryRose.opacity(0.35), radius: 10)
                }
            } else {
                Button {
                    AppTheme.hapticSuccess()
                    dismiss()
                } label: {
                    Text("Всё понятно, поехали!")
                        .font(.system(size: 16, weight: .bold, design: .rounded))
                        .foregroundStyle(.white)
                        .frame(maxWidth: .infinity)
                        .frame(height: 52)
                        .background(AppTheme.heroGradient)
                        .clipShape(RoundedRectangle(cornerRadius: 16))
                        .shadow(color: AppTheme.primaryRose.opacity(0.4), radius: 12)
                }
            }
        }
    }
}
