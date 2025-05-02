using System;
using System.Collections;
using System.Runtime.InteropServices;
using AOT;
using TMPro;
using UnityEngine;

public class VisionProEnterpriseAPIManager : MonoBehaviour
{
    public static VisionProEnterpriseAPIManager Instance { get; private set; }
    public Material PreviewMaterial;

    public TextMeshPro previewText;
    private Texture2D tmpTexture = null;
    private string tempBase64String = null;
    private string lastQRCodePayload = null;
    private float skipSeconds = 0.1f;

    public delegate void CameraCallbackDelegate(string base64String);
    public delegate void QRCodeCallbackDelegate(string qrPayload);

    [MonoPInvokeCallback(typeof(CameraCallbackDelegate))]
    public static void CallbackFromCamera(string base64)
    {
        Instance.tempBase64String = base64;
    }

    [MonoPInvokeCallback(typeof(QRCodeCallbackDelegate))]
    public static void CallbackFromQRCode(string payload)
    {
        Instance.lastQRCodePayload = payload;
        Debug.Log("QR Code Detected: " + payload);
        Instance.previewText.text = payload;
        // You can invoke Unity event or notification here
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }

    void Start()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        SetCameraCallback(CallbackFromCamera);
        SetQRCodeCallback(CallbackFromQRCode);
        StartCameraFeed();
        StartQRCodeDetection();

        tmpTexture = new Texture2D(256, 256);
        PreviewMaterial.mainTexture = tmpTexture;
        StartCoroutine(ApplyCameraFeedToMaterial());
#endif
    }

    void OnDisable()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        SetCameraCallback(null);
        SetQRCodeCallback(null);
#endif
    }

    IEnumerator ApplyCameraFeedToMaterial()
    {
        while (true)
        {
            yield return new WaitForSeconds(skipSeconds);
            if (PreviewMaterial != null && tempBase64String != null)
            {
                Base64ToTexture2D(tmpTexture, tempBase64String);
                PreviewMaterial.mainTexture = tmpTexture;
            }
        }
    }

    void Base64ToTexture2D(Texture2D tex, string base64)
    {
        if (string.IsNullOrEmpty(base64)) return;

        try
        {
            byte[] imageBytes = Convert.FromBase64String(base64);
            if (!tex.LoadImage(imageBytes))
            {
                Debug.LogError("Failed to load image from byte array.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Base64 decode error: " + e.Message);
        }
    }

#if UNITY_VISIONOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SetCameraCallback(CameraCallbackDelegate callback);

    [DllImport("__Internal")]
    private static extern void StartCameraFeed();

    [DllImport("__Internal")]
    private static extern void SetQRCodeCallback(QRCodeCallbackDelegate callback);

    [DllImport("__Internal")]
    private static extern void StartQRCodeDetection();
#else
    private static void SetCameraCallback(CameraCallbackDelegate callback) { }
    private static void StartCameraFeed() { }
    private static void SetQRCodeCallback(QRCodeCallbackDelegate callback) { }
    private static void StartQRCodeDetection() { }
#endif
}

