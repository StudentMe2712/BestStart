//
//  OfflineBillCalculatorView.swift
//  CarteBlanche
//
//  Created for Carte Blanche - Offline Luxe Menu & Tasting Guide
//  iOS 17.0+ SwiftUI
//

import SwiftUI

/// Aesthetic interactive bill and folio calculator in Dark Luxe Editorial style ("Luxe Guest Folio")
public struct OfflineBillCalculatorView: View {

    // MARK: - Bindings & Observed Model

    public var viewModel: CoursePlannerViewModel

    // MARK: - Initializer

    public init(viewModel: CoursePlannerViewModel) {
        self.viewModel = viewModel
    }

    // MARK: - Body

    public var body: some View {
        GlassCard(
            cornerRadius: 20,
            strokeColor: Color.champagneAccent.opacity(0.35),
            fillOpacity: 0.90,
            padding: 22
        ) {
            VStack(alignment: .leading, spacing: 20) {
                // Folio Masthead
                folioHeaderView

                Divider()
                    .background(Color.glassBorder)

                // Tip / Gratuity Selector Strip
                tipSelectorSectionView

                // Guest Count Split Stepper
                guestSplitSectionView

                Divider()
                    .background(Color.glassBorder)

                // Financial Breakdown Ledger
                financialLedgerView

                // Per-Guest Gold Callout Box
                perGuestCalloutBox
            }
        }
    }

    // MARK: - Subviews: Folio Header

    private var folioHeaderView: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 8) {
                Image(systemName: "doc.text.fill")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(Color.champagneAccent)

                Text("LUXE GUEST FOLIO")
                    .font(.luxeMicroLabel)
                    .tracking(2.2)
                    .foregroundStyle(Color.champagneAccent)

                Spacer()

                HStack(spacing: 5) {
                    Circle()
                        .fill(Color.sageGreen)
                        .frame(width: 6, height: 6)
                    Text("OFFLINE AUDIT")
                        .font(.system(size: 9, weight: .bold, design: .monospaced))
                        .foregroundStyle(Color.sageGreen)
                }
                .padding(.horizontal, 8)
                .padding(.vertical, 3.5)
                .background(Color.sageGreen.opacity(0.12))
                .clipShape(Capsule())
            }

            Text("Расчет Дегустационного Счета")
                .font(.system(.title3, design: .serif, weight: .bold))
                .foregroundStyle(Color.textPrimary)

            Text("Автономный расчет стоимости подачи курсов, сплита на компанию и сервисного сбора.")
                .font(.system(size: 11.5, weight: .regular))
                .foregroundStyle(Color.textSecondary)
                .lineSpacing(2)
        }
    }

    // MARK: - Subviews: Tip Selector Strip

    private var tipSelectorSectionView: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                HStack(spacing: 6) {
                    Image(systemName: "sparkles")
                        .font(.system(size: 11))
                        .foregroundStyle(Color.champagneAccent)

                    Text("SERVICE COMPLIMENT / ЧАЕВЫЕ")
                        .font(.luxeMicroLabel)
                        .tracking(1.4)
                        .foregroundStyle(Color.textSecondary)
                }

                Spacer()

                Text(viewModel.selectedTipPercentage.displayTitle)
                    .font(.system(size: 12, weight: .bold, design: .monospaced))
                    .foregroundStyle(Color.champagneAccent)
            }

            // Segmented Chips
            HStack(spacing: 8) {
                ForEach(TipPercentage.allCases) { tip in
                    let isSelected = viewModel.selectedTipPercentage == tip
                    Button {
                        withAnimation(.spring(response: 0.3, dampingFraction: 0.7)) {
                            viewModel.selectedTipPercentage = tip
                        }
                    } label: {
                        VStack(spacing: 2) {
                            Text(tip.displayTitle)
                                .font(.system(size: 13, weight: .bold, design: .rounded))
                                .monospacedDigit()
                        }
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 10)
                        .background(
                            isSelected ?
                            Color.champagneAccent :
                            Color.cardSurface
                        )
                        .foregroundStyle(
                            isSelected ?
                            Color.obsidianCanvas :
                            Color.textPrimary
                        )
                        .clipShape(RoundedRectangle(cornerRadius: 10))
                        .overlay(
                            RoundedRectangle(cornerRadius: 10)
                                .stroke(
                                    isSelected ?
                                    Color.champagneAccent :
                                    Color.glassBorder,
                                    lineWidth: 1
                                )
                        )
                        .shadow(
                            color: isSelected ? Color.champagneAccent.opacity(0.3) : Color.clear,
                            radius: 8,
                            y: 3
                        )
                    }
                    .buttonStyle(PlainButtonStyle())
                }
            }

            // Tip Descriptor Subtext
            Text(viewModel.selectedTipPercentage.subtitle)
                .font(.system(size: 10.5, weight: .medium, design: .serif))
                .foregroundStyle(Color.textSecondary.opacity(0.85))
                .padding(.leading, 2)
        }
    }

    // MARK: - Subviews: Guest Bill Split

    private var guestSplitSectionView: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                HStack(spacing: 6) {
                    Image(systemName: guestIconName)
                        .font(.system(size: 11))
                        .foregroundStyle(Color.champagneAccent)

                    Text("SPLIT AMONG GUESTS / СПЛИТ СЧЕТА")
                        .font(.luxeMicroLabel)
                        .tracking(1.4)
                        .foregroundStyle(Color.textSecondary)
                }

                Spacer()

                Text("Макс. 8 гостей")
                    .font(.system(size: 10, design: .monospaced))
                    .foregroundStyle(Color.textSecondary)
            }

            HStack(spacing: 16) {
                // Stepper Controls
                HStack(spacing: 12) {
                    Button {
                        withAnimation(.spring(response: 0.25, dampingFraction: 0.7)) {
                            viewModel.decrementGuests()
                        }
                    } label: {
                        Image(systemName: "minus")
                            .font(.system(size: 13, weight: .bold))
                            .frame(width: 36, height: 36)
                            .background(viewModel.guestCount > 1 ? Color.cardSurface : Color.cardSurface.opacity(0.4))
                            .foregroundStyle(viewModel.guestCount > 1 ? Color.champagneAccent : Color.textSecondary.opacity(0.3))
                            .clipShape(Circle())
                            .overlay(
                                Circle()
                                    .stroke(Color.glassBorder, lineWidth: 1)
                            )
                    }
                    .disabled(viewModel.guestCount <= 1)
                    .buttonStyle(PlainButtonStyle())

                    // Center Guest Indicator
                    HStack(spacing: 8) {
                        Image(systemName: guestIconName)
                            .font(.system(size: 16))
                            .foregroundStyle(Color.champagneAccent)

                        Text("\(viewModel.guestCount)")
                            .font(.system(size: 18, weight: .bold, design: .serif))
                            .monospacedDigit()
                            .foregroundStyle(Color.textPrimary)

                        Text(guestPluralWord)
                            .font(.system(size: 12, weight: .medium, design: .serif))
                            .foregroundStyle(Color.textSecondary)
                    }
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 8)
                    .background(Color.obsidianCanvas.opacity(0.6))
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                    .overlay(
                        RoundedRectangle(cornerRadius: 12)
                            .stroke(Color.glassBorder, lineWidth: 1)
                    )

                    Button {
                        withAnimation(.spring(response: 0.25, dampingFraction: 0.7)) {
                            viewModel.incrementGuests()
                        }
                    } label: {
                        Image(systemName: "plus")
                            .font(.system(size: 13, weight: .bold))
                            .frame(width: 36, height: 36)
                            .background(viewModel.guestCount < 8 ? Color.cardSurface : Color.cardSurface.opacity(0.4))
                            .foregroundStyle(viewModel.guestCount < 8 ? Color.champagneAccent : Color.textSecondary.opacity(0.3))
                            .clipShape(Circle())
                            .overlay(
                                Circle()
                                    .stroke(Color.glassBorder, lineWidth: 1)
                            )
                    }
                    .disabled(viewModel.guestCount >= 8)
                    .buttonStyle(PlainButtonStyle())
                }
            }
        }
    }

    private var guestIconName: String {
        switch viewModel.guestCount {
        case 1:
            return "person.fill"
        case 2:
            return "person.2.fill"
        case 3:
            return "person.3.fill"
        default:
            return "person.3.sequence.fill"
        }
    }

    private var guestPluralWord: String {
        switch viewModel.guestCount {
        case 1:
            return "гость (Solo)"
        case 2, 3, 4:
            return "гостя"
        default:
            return "гостей"
        }
    }

    // MARK: - Subviews: Financial Breakdown Ledger

    private var financialLedgerView: some View {
        VStack(spacing: 10) {
            // Subtotal Row
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("СУММА БЛЮД / SUBTOTAL")
                        .font(.system(size: 9.5, weight: .bold, design: .monospaced))
                        .tracking(1)
                        .foregroundStyle(Color.textSecondary)

                    Text("\(viewModel.totalDishesCount) \(viewModel.totalDishesCount == 1 ? "блюдо" : "блюд") в \(viewModel.activeCoursesCount) \(viewModel.activeCoursesCount == 1 ? "подаче" : "подачах")")
                        .font(.system(size: 11))
                        .foregroundStyle(Color.textSecondary.opacity(0.8))
                }

                Spacer()

                Text(viewModel.formattedSubtotal)
                    .font(.system(.subheadline, design: .rounded).weight(.semibold))
                    .monospacedDigit()
                    .foregroundStyle(Color.textPrimary)
            }

            // Gratuity Row
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("СЕРВИСНЫЙ СБОР (\(viewModel.selectedTipPercentage.displayTitle))")
                        .font(.system(size: 9.5, weight: .bold, design: .monospaced))
                        .tracking(1)
                        .foregroundStyle(Color.textSecondary)

                    Text("Gratuity / Service compliment")
                        .font(.system(size: 11))
                        .foregroundStyle(Color.textSecondary.opacity(0.8))
                }

                Spacer()

                Text(viewModel.formattedTipAmount)
                    .font(.system(.subheadline, design: .rounded).weight(.semibold))
                    .monospacedDigit()
                    .foregroundStyle(viewModel.tipAmount > 0 ? Color.champagneAccent : Color.textSecondary)
            }

            Divider()
                .background(Color.glassBorder.opacity(0.7))

            // Total Amount Row
            HStack(alignment: .lastTextBaseline) {
                VStack(alignment: .leading, spacing: 3) {
                    Text("ИТОГОВЫЙ СЧЕТ / TOTAL AMOUNT")
                        .font(.system(size: 10.5, weight: .bold, design: .monospaced))
                        .tracking(1.5)
                        .foregroundStyle(Color.textPrimary)

                    Text("Включая НДС и все сборы")
                        .font(.system(size: 11))
                        .foregroundStyle(Color.textSecondary)
                }

                Spacer()

                Text(viewModel.formattedTotalAmount)
                    .font(.system(size: 26, weight: .bold, design: .serif))
                    .monospacedDigit()
                    .foregroundStyle(Color.textPrimary)
            }
        }
    }

    // MARK: - Subviews: Per-Guest Gold Callout Box

    private var perGuestCalloutBox: some View {
        VStack(spacing: 6) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("PER GUEST / НА ПЕРСОНУ")
                        .font(.system(size: 9, weight: .bold, design: .monospaced))
                        .tracking(1.8)
                        .foregroundStyle(Color.champagneAccent)

                    Text("Сплит на \(viewModel.guestCount) \(guestPluralWord)")
                        .font(.system(size: 11, weight: .medium))
                        .foregroundStyle(Color.textSecondary)
                }

                Spacer()

                Text(viewModel.formattedAmountPerPerson)
                    .font(.system(size: 28, weight: .bold, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(Color.champagneAccent)
            }

            // Calculation footnote
            HStack {
                Text("\(viewModel.formattedAmountPerPerson) × \(viewModel.guestCount) = \(viewModel.formattedTotalAmount)")
                    .font(.system(size: 10.5, design: .monospaced))
                    .foregroundStyle(Color.textSecondary.opacity(0.85))

                Spacer()

                Image(systemName: "checkmark.circle.fill")
                    .font(.system(size: 11))
                    .foregroundStyle(Color.sageGreen)
            }
        }
        .padding(14)
        .background(
            LinearGradient(
                colors: [
                    Color.champagneAccent.opacity(0.12),
                    Color.champagneAccent.opacity(0.04)
                ],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
        )
        .clipShape(RoundedRectangle(cornerRadius: 14))
        .overlay(
            RoundedRectangle(cornerRadius: 14)
                .stroke(Color.champagneAccent.opacity(0.4), lineWidth: 1)
        )
    }
}

// MARK: - Interactive Preview

#Preview("OfflineBillCalculatorView Live Folio") {
    OfflineBillCalculatorPreviewContainer()
}

private struct OfflineBillCalculatorPreviewContainer: View {
    @State private var viewModel: CoursePlannerViewModel = {
        let sampleItems = Array(MockDataService.shared.items.prefix(4))
        return CoursePlannerViewModel(
            tastingItems: sampleItems,
            guestCount: 2,
            selectedTipPercentage: .fifteen
        )
    }()

    var body: some View {
        ZStack {
            Color.obsidianCanvas
                .ignoresSafeArea()

            ScrollView {
                VStack(spacing: 24) {
                    OfflineBillCalculatorView(viewModel: viewModel)
                        .padding(.horizontal, 20)
                }
                .padding(.vertical, 24)
            }
        }
    }
}
