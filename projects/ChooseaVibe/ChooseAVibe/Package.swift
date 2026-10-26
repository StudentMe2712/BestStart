// swift-tools-version: 5.9
// The swift-tools-version declares the minimum version of Swift required to build this package.

import PackageDescription

let package = Package(
    name: "ChooseAVibe",
    defaultLocalization: "ru",
    platforms: [
        .iOS(.v17),
        .macOS(.v14)
    ],
    products: [
        .library(
            name: "ChooseAVibeCore",
            targets: ["ChooseAVibeCore"]
        )
    ],
    targets: [
        .target(
            name: "ChooseAVibeCore",
            path: "Domain",
            resources: [
                .process("../Resources")
            ]
        ),
        .testTarget(
            name: "ChooseAVibeTests",
            dependencies: [
                "ChooseAVibeCore"
            ],
            path: "Tests/ChooseAVibeTests"
        )
    ]
)
