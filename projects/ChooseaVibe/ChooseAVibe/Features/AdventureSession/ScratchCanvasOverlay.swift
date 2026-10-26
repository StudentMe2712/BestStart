import SwiftUI

public struct ScratchCanvasOverlay: View {
    public let themeId: String
    public let stepIndex: Int
    public let turnPartnerName: String
    public let onScratchedComplete: () -> Void

    @State private var strokes: [[CGPoint]] = []
    @State private var currentStroke: [CGPoint] = []
    @State private var scratchedCells: Set<Int> = []
    @State private var lastHapticCellCount: Int = 0
    @State private var isRevealed: Bool = false
    @State private var containerSize: CGSize = .zero

    public init(
        themeId: String = "classic_silver",
        stepIndex: Int,
        turnPartnerName: String,
        onScratchedComplete: @escaping () -> Void
    ) {
        self.themeId = themeId
        self.stepIndex = stepIndex
        self.turnPartnerName = turnPartnerName
        self.onScratchedComplete = onScratchedComplete
    }

    public var body: some View {
        ZStack {
            if !isRevealed {
                ZStack {
                    // Базовый защитный слой с текстурой и надписями
                    coatingSurfaceView

                    // Canvas со стёртыми линиями (destinationOut)
                    Canvas { context, size in
                        for stroke in strokes {
                            var path = Path()
                            path.addLines(stroke)
                            context.stroke(
                                path,
                                with: .color(.black),
                                style: StrokeStyle(lineWidth: 46, lineCap: .round, lineJoin: .round)
                            )
                        }

                        if !currentStroke.isEmpty {
                            var activePath = Path()
                            activePath.addLines(currentStroke)
                            context.stroke(
                                activePath,
                                with: .color(.black),
                                style: StrokeStyle(lineWidth: 46, lineCap: .round, lineJoin: .round)
                            )
                        }
                    }
                    .blendMode(.destinationOut)
                }
                .compositingGroup()
                .background(
                    GeometryReader { geo in
                        Color.clear
                            .onAppear {
                                containerSize = geo.size
                            }
                            .onChange(of: geo.size) { _, newSize in
                                containerSize = newSize
                            }
                    }
                )
                .gesture(scratchDragGesture)
                .transition(.opacity.combined(with: .scale(scale: 1.04)))

                // Кнопка доступности "Открыть сразу" внизу карточки
                VStack {
                    Spacer()
                    Button {
                        revealCardImmediately()
                    } label: {
                        HStack(spacing: 6) {
                            Image(systemName: "hand.tap.fill")
                                .font(.system(size: 13))
                            Text("Открыть сразу")
                                .font(.system(size: 13, weight: .semibold))
                        }
                        .foregroundStyle(.white.opacity(0.85))
                        .padding(.horizontal, 14)
                        .padding(.vertical, 8)
                        .background(Color.black.opacity(0.4))
                        .clipShape(Capsule())
                    }
                    .padding(.bottom, 16)
                }
            }
        }
    }

    // MARK: - Поверхность скретч-рубашки
    private var coatingSurfaceView: some View {
        ZStack {
            AppTheme.scratchThemeGradient(for: themeId)

            // Декоративный геометрический узор
            VStack(spacing: 16) {
                Image(systemName: "sparkles")
                    .font(.system(size: 40, weight: .bold))
                    .foregroundStyle(.white.opacity(0.35))

                VStack(spacing: 6) {
                    Text("ЗАЩИТНЫЙ СЛОЙ")
                        .font(.system(size: 12, weight: .heavy, design: .rounded))
                        .tracking(3)
                        .foregroundStyle(.white.opacity(0.6))

                    Text("Стирай пальцем!")
                        .font(.system(size: 20, weight: .bold, design: .rounded))
                        .foregroundStyle(.white)

                    Text("Ход: \(turnPartnerName)")
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(AppTheme.warmGold)
                        .padding(.top, 4)
                }
            }
            .padding()

            // Рамка карточки
            RoundedRectangle(cornerRadius: 24)
                .stroke(Color.white.opacity(0.25), lineWidth: 2)
        }
        .clipShape(RoundedRectangle(cornerRadius: 24))
        .shadow(color: Color.black.opacity(0.3), radius: 10, y: 5)
    }

    // MARK: - Жест стирания
    private var scratchDragGesture: some Gesture {
        DragGesture(minimumDistance: 0)
            .onChanged { value in
                let point = value.location
                currentStroke.append(point)
                registerGridPoint(point)
            }
            .onEnded { _ in
                if !currentStroke.isEmpty {
                    strokes.append(currentStroke)
                    currentStroke = []
                }
            }
    }

    // MARK: - Трекинг виртуальной сетки 10x10
    private func registerGridPoint(_ point: CGPoint) {
        guard containerSize.width > 0, containerSize.height > 0 else { return }

        let cellWidth = containerSize.width / 10.0
        let cellHeight = containerSize.height / 10.0

        let centerCol = Int(point.x / cellWidth)
        let centerRow = Int(point.y / cellHeight)

        // Захватываем радиус кисти (~1 ячейку вокруг центра)
        for r in max(0, centerRow - 1)...min(9, centerRow + 1) {
            for c in max(0, centerCol - 1)...min(9, centerCol + 1) {
                let cellIndex = r * 10 + c
                scratchedCells.insert(cellIndex)
            }
        }

        // Тактильный отклик каждые 8 новых ячеек
        if scratchedCells.count - lastHapticCellCount >= 8 {
            AppTheme.hapticSoft()
            lastHapticCellCount = scratchedCells.count
        }

        // Авто-раскрытие при достижении порога 45% (45 ячеек из 100)
        if scratchedCells.count >= 45 && !isRevealed {
            revealCardWithAnimation()
        }
    }

    private func revealCardWithAnimation() {
        AppTheme.hapticSuccess()
        withAnimation(.spring(duration: 0.45)) {
            isRevealed = true
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) {
            onScratchedComplete()
        }
    }

    private func revealCardImmediately() {
        AppTheme.hapticMedium()
        withAnimation(.spring(duration: 0.35)) {
            isRevealed = true
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) {
            onScratchedComplete()
        }
    }
}
