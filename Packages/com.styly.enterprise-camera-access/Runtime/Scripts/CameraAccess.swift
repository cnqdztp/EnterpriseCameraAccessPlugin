import Foundation
import ARKit
import AVFoundation
import UIKit
import RealityKit

// MARK: - C# Callbacks
typealias CameraCallback = @convention(c) (UnsafePointer<CChar>) -> Void
typealias QRCodeCallback = @convention(c) (UnsafePointer<CChar>) -> Void

var cameraCallback: CameraCallback?
var qrCodeCallback: QRCodeCallback?

@_cdecl("SetCameraCallback")
func setCameraCallback(_ callback: @escaping CameraCallback) {
    cameraCallback = callback
}

@_cdecl("SetQRCodeCallback")
func setQRCodeCallback(_ callback: @escaping QRCodeCallback) {
    qrCodeCallback = callback
}

// MARK: - Camera Feed
@_cdecl("StartCameraFeed")
func startCameraFeed() {
    Task {
        await startCameraCapture()
    }
}

var lastCameraTime: Date?

func startCameraCapture() async {
    let formats = CameraVideoFormat.supportedVideoFormats(for: .main, cameraPositions: [.left])
    let session = ARKitSession()
    _ = await session.queryAuthorization(for: [.cameraAccess])
    
    let cameraProvider = CameraFrameProvider()
    try? await session.run([cameraProvider])

    guard let format = formats.first else { return }

    for await update in cameraProvider.cameraFrameUpdates(for: format)! {
        let pixelBuffer = update.primarySample.pixelBuffer
        let now = Date()
        if lastCameraTime == nil || now.timeIntervalSince(lastCameraTime!) > 0.1 {
            sendCameraBufferToUnity(pixelBuffer)
            lastCameraTime = now
        }
    }
}

func sendCameraBufferToUnity(_ buffer: CVPixelBuffer) {
    CVPixelBufferLockBaseAddress(buffer, .readOnly)
    defer { CVPixelBufferUnlockBaseAddress(buffer, .readOnly) }

    let ciImage = CIImage(cvPixelBuffer: buffer)
    let context = CIContext()
    guard let cgImage = context.createCGImage(ciImage, from: ciImage.extent) else { return }

    let uiImage = UIImage(cgImage: cgImage)
    guard let imageData = uiImage.jpegData(compressionQuality: 1.0) else { return }

    let base64 = imageData.base64EncodedString()
    base64.withCString {
        cameraCallback?($0)
    }
}

// MARK: - QR Code Detection
@_cdecl("StartQRCodeDetection")
func startQRCodeDetection() {
    Task {
        await detectBarcodes()
    }
}

func detectBarcodes() async {
    guard BarcodeDetectionProvider.isSupported else { return }

    let barcodeProvider = BarcodeDetectionProvider(symbologies: [.qr])
    let session = ARKitSession()
    try? await session.run([barcodeProvider])

    for await update in barcodeProvider.anchorUpdates where update.event == .added {
        if let payload = update.anchor.payloadString {
            payload.withCString {
                qrCodeCallback?($0)
            }
        }
    }
}
