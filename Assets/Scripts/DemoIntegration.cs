using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using System.Collections;

public class DemoIntegration : MonoBehaviour
{
    [Header("Realtime API Wrapper Bindings")]
    [SerializeField] private RealtimeAPIWrapper realtimeAPIWrapper;

    [Header("VR Canvas Popup")]
    [SerializeField] private Canvas demoCanvas;          // drag your DemoUI canvas here
    [SerializeField] private float canvasDistance = 1f;
    [SerializeField] private Canvas  conversationLogCanvas;  // drag in your ConversationLog canvas here
    [SerializeField] private Toggle  conversationLogToggle;  // drag in the new Toggle here
    [SerializeField] private Vector3 conversationLogOffset = new Vector3(0.5f, 0f, 0f);

    [Header("Avatar Scale")]
    [SerializeField] public Slider avatarScaleSlider; 

    [Header("EffectMesh Toggle")]
    [SerializeField] private EffectMesh effectMesh;                    // drag the GameObject that has EffectMesh
    [SerializeField] private Button    hideMeshButton;                 // your new button in the Settings UI
    [SerializeField] private TextMeshProUGUI hideMeshButtonText;       // the label inside that button

    [Header("Voice‑Buttons (Alloy/Ash/Ballad/Coral/Verse)")]
    [SerializeField] private Button btnAlloy;
    [SerializeField] private Button btnAsh;
    [SerializeField] private Button btnBallad;
    [SerializeField] private Button btnCoral;
    [SerializeField] private Button btnVerse;

    [Header("Button Colors")]
    [SerializeField] private Color inactiveColor = Color.white;
    [SerializeField] private Color activeColor   = new Color(0f, 0.5f, 1f); // e.g. a blue tint

    [SerializeField] private KeyCode pushToTalkKey = KeyCode.Space;
    [SerializeField] private AudioRecorder audioRecorder;
    [SerializeField] private AudioPlayer audioPlayer;
    [SerializeField] private TextMeshProUGUI eventsText;
    [SerializeField] private TextMeshProUGUI conversationText;
    [SerializeField] private TextMeshProUGUI vadEnergyText;
    [SerializeField] private Button pushToTalkButton;
    [SerializeField] private Button connectButton;
    [SerializeField] private TextMeshProUGUI pushToTalkButtonText;
    [SerializeField] private TextMeshProUGUI connectButtonText;
    [SerializeField] private Button manualListeningButton;
    [SerializeField] private Button vadListeningButton;
    [SerializeField] private TextMeshProUGUI manualListeningButtonText;
    [SerializeField] private TextMeshProUGUI vadListeningButtonText;

    [SerializeField] private Image[] frequencyBars;
    [SerializeField] private Image[] aiFrequencyBars;

    int logCountLimit = 14;

    float maxFrequencyAmplitude = 4f;
    float aiMaxFrequencyAmplitude = 0.1f;

    bool isRecording = false;
    List<string> logMessages = new List<string>();
    List<string> conversationMessages = new List<string>();
    string currentConversationLine = "";

    float[] userBarAmplitudes;
    float[] aiBarAmplitudes;
    float barSmoothingSpeed = 5f;

    public static DemoIntegration Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (realtimeAPIWrapper == null)
            realtimeAPIWrapper = FindObjectOfType<RealtimeAPIWrapper>();
        if (demoCanvas == null)
            demoCanvas = FindObjectOfType<Canvas>();
        if (effectMesh == null)
            effectMesh = FindObjectOfType<EffectMesh>();
        if (conversationLogToggle != null && conversationLogCanvas != null)
        {
            conversationLogToggle.onValueChanged.AddListener(OnToggleConversationLog);
            // start hidden (or sync with the toggle’s initial state)
            conversationLogCanvas.gameObject.SetActive(conversationLogToggle.isOn);
        }
        if (avatarScaleSlider != null)
        {
            // initialize slider to 1.0 (no scaling)
            avatarScaleSlider.value = 1f;
            avatarScaleSlider.onValueChanged.AddListener(OnScaleSliderValueChanged);
        }
        RealtimeAPIWrapper.OnResponseCreated += OnStartTalking;
        // When the API says “audio done” that means we’re done streaming audio:
        RealtimeAPIWrapper.OnResponseAudioDone += OnStopTalking;

        btnAlloy.onClick.AddListener(() => SelectVoice("alloy", btnAlloy));
        btnAsh.onClick.AddListener(() => SelectVoice("ash", btnAsh));
        btnBallad.onClick.AddListener(() => SelectVoice("ballad", btnBallad));
        btnCoral.onClick.AddListener(() => SelectVoice("coral", btnCoral));
        btnVerse.onClick.AddListener(() => SelectVoice("verse", btnVerse));

        // 3) Initialize button‑colors based on the default voice (if any)
        //    If realtimeAPIWrapper.voiceId was set already in Inspector, highlight that.
        UpdateVoiceButtonColors();

        hideMeshButton.onClick.AddListener(OnToggleHideMesh);
        UpdateHideMeshButtonLabel();

        pushToTalkButton.onClick.AddListener(OnRecordButtonPressed);
        RealtimeAPIWrapper.OnWebSocketConnected += OnWebSocketConnected;
        RealtimeAPIWrapper.OnWebSocketClosed += OnWebSocketClosed;
        RealtimeAPIWrapper.OnSessionCreated += OnSessionCreated;
        RealtimeAPIWrapper.OnConversationItemCreated += OnConversationItemCreated;
        RealtimeAPIWrapper.OnResponseDone += OnResponseDone;
        RealtimeAPIWrapper.OnTranscriptReceived += OnTranscriptReceived;
        RealtimeAPIWrapper.OnResponseCreated += OnResponseCreated;

        AudioRecorder.OnVADRecordingStarted += OnVADRecordingStarted;
        AudioRecorder.OnVADRecordingEnded += OnVADRecordingEnded;

        manualListeningButton.onClick.AddListener(OnManualListeningMode);
        vadListeningButton.onClick.AddListener(OnVADListeningMode);

        UpdateListeningModeButtons();
        UpdateRecordButton();

        userBarAmplitudes = new float[frequencyBars.Length];
        aiBarAmplitudes = new float[aiFrequencyBars.Length];
    }

    private void OnDestroy()
    {
        // clean up to avoid memory leaks
        RealtimeAPIWrapper.OnResponseCreated   -= OnStartTalking;
        RealtimeAPIWrapper.OnResponseAudioDone -= OnStopTalking;
    }

    private void OnStartTalking()
    {
        SetAvatarTalking(true);
    }

    private void OnStopTalking()
    {
        StartCoroutine(WaitForAudioToEndThenStopTalking());
    }

    private IEnumerator WaitForAudioToEndThenStopTalking()
    {
        var player = realtimeAPIWrapper.audioPlayer;  // assume you’ve wired this in Inspector
        // Wait until the wrapper has stopped streaming _and_ nothing is left in the player’s buffer
        while (realtimeAPIWrapper.IsAudioStreaming || player.IsAudioPlaying())
            yield return null;

        SetAvatarTalking(false);
    }

    private void SetAvatarTalking(bool talking)
    {
        var avatar = AvatarSettingsManager.Instance.currentInstance;
        if (avatar == null) return;

        // ← change this line:
        var animator = avatar.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("SetAvatarTalking: no Animator on the root of currentInstance");
            return;
        }

        Debug.Log($"SetBool bool set to {talking}");
        animator.SetBool("talking", talking);
    }
    /// <summary>
    /// Called whenever one of the five voice‑buttons is clicked.
    /// Sets the new voice ID and repaints button colors.
    /// </summary>
    private void SelectVoice(string voiceId, Button chosenButton)
    {
        realtimeAPIWrapper.voiceId = voiceId;
        UpdateVoiceButtonColors();
    }

    /// <summary>
    /// Tints all five buttons. The currently active voice gets activeColor; the others get inactiveColor.
    /// </summary>
    private void UpdateVoiceButtonColors()
    {
        // 1) Figure out which voice is currently active
        string current = realtimeAPIWrapper.voiceId;

        // 2) For each button, compare its corresponding voice string against current
        btnAlloy.image.color  = (current == "alloy")  ? activeColor : inactiveColor;
        btnAsh.image.color    = (current == "ash")    ? activeColor : inactiveColor;
        btnBallad.image.color = (current == "ballad") ? activeColor : inactiveColor;
        btnCoral.image.color  = (current == "coral")  ? activeColor : inactiveColor;
        btnVerse.image.color  = (current == "verse")  ? activeColor : inactiveColor;
    }
    
    /// <summary>
    /// Called when your “Hide Mesh” button is clicked.
    /// </summary>
    private void OnToggleHideMesh()
    {
        // Flip the boolean on EffectMesh:
        effectMesh.HideMesh = !effectMesh.HideMesh;
        // Update the button label so it reads “Show Mesh” if mesh is now hidden, etc.
        UpdateHideMeshButtonLabel();
    }

    /// <summary>
    /// Writes the correct label based on effectMesh.HideMesh
    /// </summary>
    private void UpdateHideMeshButtonLabel()
    {
        if (hideMeshButtonText == null || effectMesh == null) 
            return;

        // If hideMesh == true, the mesh is hidden, so the button should say “Show Mesh”
        hideMeshButtonText.text = effectMesh.HideMesh
            ? "Show Mesh"
            : "Hide Mesh";
    }
    private void Update()
    {
        // - When X (OVRInput.Button.Three) on the left controller goes down…
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch))
        {
            bool nowActive = !demoCanvas.gameObject.activeSelf;
            demoCanvas.gameObject.SetActive(nowActive);

            Debug.Log("[demoCanvas] demoCanvas activated");
            if (nowActive)
            {
                // grab your HMD camera
                Transform cam = Camera.main.transform;
                // place the canvas a fixed distance in front
                demoCanvas.transform.position = cam.position + cam.forward * canvasDistance;
                // face it toward the camera
                demoCanvas.transform.rotation = Quaternion.LookRotation(cam.forward, cam.up);
            }
        }

        // — A button toggles connect/disconnect —
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            realtimeAPIWrapper.ConnectWebSocketButton();
        }

        // — now your existing Push‑to‑Talk logic, augmented for B —
        if (audioRecorder.listeningMode == ListeningMode.PushToTalk)
        {
            // press B (Button.Two) OR Space to start
            if ((Input.GetKeyDown(pushToTalkKey) ||
                OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
                && !isRecording)
            {
                StartRecording();
            }

            // release B OR Space to stop
            if ((Input.GetKeyUp(pushToTalkKey) ||
                OVRInput.GetUp(OVRInput.Button.Two, OVRInput.Controller.RTouch))
                && isRecording)
            {
                StopRecording();
            }
        }
        // UpdateFrequencyBars();
        // UpdateAIFrequencyBars();
    }

     /// <summary>
    /// Called by the UI Toggle whenever the user flips “Show Conversation Log.”
    /// </summary>
    public void OnToggleConversationLog(bool isOn)
    {
        if (conversationLogCanvas == null) return;
        conversationLogCanvas.gameObject.SetActive(isOn);

        if (isOn)
            PositionConversationLog();
    }

    /// <summary>
    /// Positions the conversationLogCanvas next to demoCanvas using the given offset.
    /// </summary>
    private void PositionConversationLog()
    {
        if (demoCanvas == null || conversationLogCanvas == null) return;

        // world‐space: start at your settings canvas
        var origin = demoCanvas.transform;
        // offset in the canvas’s local space
        var worldOffset = origin.right * conversationLogOffset.x
                        + origin.up    * conversationLogOffset.y
                        + origin.forward * conversationLogOffset.z;

        conversationLogCanvas.transform.position = origin.position + worldOffset;
        conversationLogCanvas.transform.rotation = origin.rotation;
    }

    /// <summary>
    /// Called when the user drags the Avatar Scale slider.
    /// </summary>
    private void OnScaleSliderValueChanged(float newScale)
    {
        AvatarSettingsManager.Instance.ScaleCurrentAvatar(newScale);
    }

    /// <summary>
    /// updates frequency bars for user audio visualization
    /// </summary>
    // private void UpdateFrequencyBars()
    // {
    //     if (frequencyBars == null || frequencyBars.Length == 0)
    //         return;

    //     if (!isRecording && audioRecorder.listeningMode == ListeningMode.PushToTalk)
    //     {
    //         for (int i = 0; i < frequencyBars.Length; i++)
    //         {
    //             userBarAmplitudes[i] = Mathf.Lerp(userBarAmplitudes[i], 0f, Time.deltaTime * barSmoothingSpeed);
    //             frequencyBars[i].fillAmount = userBarAmplitudes[i];
    //         }
    //         return;
    //     }

    //     float[] spectrum = audioRecorder.frequencyData;
    //     if (spectrum == null || spectrum.Length == 0)
    //     {
    //         for (int i = 0; i < frequencyBars.Length; i++)
    //         {
    //             userBarAmplitudes[i] = Mathf.Lerp(userBarAmplitudes[i], 0f, Time.deltaTime * barSmoothingSpeed);
    //             frequencyBars[i].fillAmount = userBarAmplitudes[i];
    //         }
    //         return;
    //     }

    //     float sampleRate = audioRecorder.sampleRate;
    //     int fftSize = audioRecorder.fftSampleSize;
    //     float nyquist = sampleRate / 2f;
    //     float freqPerBin = nyquist / fftSize;
    //     float[] freqBands = new float[] { 85f, 160f, 255f, 350f, 500f, 1000f, 2000f, 3000f, 4000f, nyquist };

    //     for (int i = 0; i < frequencyBars.Length; i++)
    //     {
    //         int startIndex = i == 0 ? 0 : Mathf.FloorToInt(freqBands[i - 1] / freqPerBin);
    //         int endIndex = Mathf.FloorToInt(freqBands[i] / freqPerBin);
    //         float sum = 0f;
    //         for (int j = startIndex; j < endIndex; j++)
    //         {
    //             sum += spectrum[j];
    //         }
    //         int sampleCount = endIndex - startIndex;
    //         float average = sampleCount > 0 ? sum / sampleCount : 0f;
    //         float amplitude = average * Mathf.Pow(2f, i);
    //         amplitude = Mathf.Clamp01(amplitude / maxFrequencyAmplitude);
    //         userBarAmplitudes[i] = Mathf.Lerp(userBarAmplitudes[i], amplitude, Time.deltaTime * barSmoothingSpeed);
    //         frequencyBars[i].fillAmount = userBarAmplitudes[i];
    //     }


    //     if (audioRecorder.listeningMode == ListeningMode.VAD)
    //         vadEnergyText.text = "nrg: " + AudioProcessingUtils.energyLast.ToString("0.0000E+0");
    // }

    // /// <summary>
    // /// updates frequency bars for ai audio visualization
    // /// </summary>
    // private void UpdateAIFrequencyBars()
    // {
    //     if (aiFrequencyBars == null || aiFrequencyBars.Length == 0)
    //         return;
    //     float[] spectrum = audioPlayer.aiFrequencyData;
    //     if (spectrum == null || spectrum.Length == 0)
    //     {
    //         for (int i = 0; i < aiFrequencyBars.Length; i++)
    //         {
    //             aiBarAmplitudes[i] = Mathf.Lerp(aiBarAmplitudes[i], 0f, Time.deltaTime * barSmoothingSpeed);
    //             aiFrequencyBars[i].fillAmount = aiBarAmplitudes[i];
    //         }
    //         return;
    //     }

    //     float sampleRate = audioPlayer.sampleRate;
    //     int fftSize = audioPlayer.fftSampleSize;
    //     float nyquist = sampleRate / 2f;
    //     float freqPerBin = nyquist / fftSize;
    //     float[] freqBands = new float[] { 85f, 160f, 255f, 350f, 500f, 1000f, 2000f, 3000f, 4000f, nyquist };

    //     for (int i = 0; i < aiFrequencyBars.Length; i++)
    //     {
    //         int startIndex = i == 0 ? 0 : Mathf.FloorToInt(freqBands[i - 1] / freqPerBin);
    //         int endIndex = Mathf.FloorToInt(freqBands[i] / freqPerBin);
    //         float sum = 0f;
    //         for (int j = startIndex; j < endIndex; j++)
    //         {
    //             sum += spectrum[j];
    //         }
    //         int sampleCount = endIndex - startIndex;
    //         float average = sampleCount > 0 ? sum / sampleCount : 0f;
    //         float amplitude = average * Mathf.Pow(2f, i);
    //         amplitude = Mathf.Clamp01(amplitude / aiMaxFrequencyAmplitude);
    //         aiBarAmplitudes[i] = Mathf.Lerp(aiBarAmplitudes[i], amplitude, Time.deltaTime * barSmoothingSpeed);
    //         aiFrequencyBars[i].fillAmount = aiBarAmplitudes[i];
    //     }
    // }

    /// <summary>
    /// handles push-to-talk button press
    /// </summary>
    private void OnRecordButtonPressed()
    {
        if (audioRecorder.listeningMode == ListeningMode.PushToTalk)
        {
            if (isRecording) StopRecording();
            else StartRecording();
        }
    }

    /// <summary>
    /// starts audio recording
    /// </summary>
    private void StartRecording()
    {
        audioRecorder.StartRecording();
        isRecording = true;
        AddLogMessage("recording...");
        UpdateRecordButton();
    }

    /// <summary>
    /// stops audio recording
    /// </summary>
    private void StopRecording()
    {
        audioRecorder.StopRecording();
        isRecording = false;
        AddLogMessage("recording stopped. sending audio...");
        UpdateRecordButton();
    }



    /// <summary>
    /// updates the record button UI
    /// </summary>
    private void UpdateRecordButton()
    {
        if (audioRecorder.listeningMode == ListeningMode.PushToTalk)
        {
            pushToTalkButton.interactable = true;
            if (isRecording)
            {
                pushToTalkButton.image.color = Color.red;
                pushToTalkButtonText.text = "release to send";
                pushToTalkButtonText.color = Color.white;
            }
            else
            {
                pushToTalkButton.image.color = new Color(236f / 255f, 236f / 255f, 241f / 255f);
                pushToTalkButtonText.text = "push to talk";
                pushToTalkButtonText.color = new Color(50f / 255f, 50f / 255f, 50f / 255f);
            }
        }
        else
        {
            pushToTalkButton.interactable = false;
            pushToTalkButton.image.color = Color.clear;
            pushToTalkButtonText.text = "";
        }
    }

    /// <summary>
    /// activates manual listening mode
    /// </summary>
    private void OnManualListeningMode()
    {
        AddLogMessage("manual listening mode activated (push to talk / spacebar).");

        audioRecorder.listeningMode = ListeningMode.PushToTalk;
        audioRecorder.StopMicrophone();

        UpdateListeningModeButtons();
        UpdateRecordButton();

        vadEnergyText.text = "";
    }

    /// <summary>
    /// activates VAD listening mode
    /// </summary>
    private void OnVADListeningMode()
    {
        AddLogMessage("VAD listening mode activated (super basic client-side vad, threshold-based).");

        audioRecorder.listeningMode = ListeningMode.VAD;
        audioRecorder.StartMicrophone();
        if (isRecording) StopRecording();

        UpdateListeningModeButtons();
        UpdateRecordButton();
    }

    /// <summary>
    /// updates listening mode buttons UI
    /// </summary>
    private void UpdateListeningModeButtons()
    {
        if (audioRecorder.listeningMode == ListeningMode.PushToTalk)
        {
            SetButtonActive(manualListeningButton, manualListeningButtonText);
            SetButtonInactive(vadListeningButton, vadListeningButtonText);
        }
        else if (audioRecorder.listeningMode == ListeningMode.VAD)
        {
            SetButtonActive(vadListeningButton, vadListeningButtonText);
            SetButtonInactive(manualListeningButton, manualListeningButtonText);
        }
    }

    /// <summary>
    /// sets a button to active state
    /// </summary>
    private void SetButtonActive(Button button, TextMeshProUGUI buttonText)
    {
        buttonText.color = Color.white;

        ColorBlock cb = button.colors;
        cb.normalColor = cb.selectedColor = new Color(15f / 255f, 15f / 255f, 15f / 255f);
        cb.highlightedColor = cb.pressedColor = new Color(64f / 255f, 64f / 255f, 64f / 255f);
        button.colors = cb;
    }

    /// <summary>
    /// sets a button to inactive state
    /// </summary>
    private void SetButtonInactive(Button button, TextMeshProUGUI buttonText)
    {
        buttonText.color = new Color(50f / 255f, 50f / 255f, 50f / 255f);

        ColorBlock cb = button.colors;
        cb.normalColor = cb.selectedColor = Color.clear;
        cb.highlightedColor = cb.pressedColor = new Color(216f / 255f, 216f / 255f, 216f / 255f);
        button.colors = cb;
    }

    /// <summary>
    /// adds a message to the log
    /// </summary>
    private void AddLogMessage(string message)
    {
        if (logMessages.Count >= logCountLimit) logMessages.RemoveAt(0);

        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");

        logMessages.Add($"{timestamp}\t{message}");
        UpdateEventsText();
    }

    /// <summary>
    /// updates the events text UI (line-idx based color-fade)
    /// </summary>
    private void UpdateEventsText()
    {
        eventsText.text = "";
        for (int i = 0; i < logMessages.Count; i++)
        {
            float alpha = Mathf.Lerp(0.2f, 1.0f, (float)(i + 1) / logMessages.Count);
            string logWithAlpha = $"<color=#{ColorUtility.ToHtmlStringRGBA(new Color(0, 0, 0, alpha))}>{logMessages[i]}</color>";
            eventsText.text += logWithAlpha + "\n";
        }
    }

    /// <summary>
    /// called when new websocket is connected - changes UI button states
    /// </summary>
    private void OnWebSocketConnected()
    {
        AddLogMessage("connection established.");
        connectButtonText.text = "disconnect";
        connectButtonText.color = new Color(50f / 255f, 50f / 255f, 50f / 255f);

        ColorBlock cb = connectButton.colors;
        cb.normalColor = cb.selectedColor = new Color(236f / 255f, 236f / 255f, 241f / 255f);
        cb.highlightedColor = cb.pressedColor = new Color(216f / 255f, 216f / 255f, 216f / 255f);
        connectButton.colors = cb;
    }

    /// <summary>
    /// called when new websocket is closed - changes UI button states
    /// </summary>
    private void OnWebSocketClosed()
    {
        AddLogMessage("connection closed.");
        connectButtonText.text = "connect";
        connectButtonText.color = Color.white;

        ColorBlock cb = connectButton.colors;
        cb.normalColor = cb.selectedColor = new Color(15f / 255f, 15f / 255f, 15f / 255f);
        cb.highlightedColor = cb.pressedColor = new Color(64f / 255f, 64f / 255f, 64f / 255f);
        if (connectButton) connectButton.colors = cb;
    }



    /// <summary>
    /// called when new conversation item is created - cleans current transcript line for new chunks
    /// </summary>
    private void OnConversationItemCreated()
    {
        AddLogMessage("conversation item created.");

        if (!string.IsNullOrEmpty(currentConversationLine))
        {
            if (conversationMessages.Count >= logCountLimit) conversationMessages.RemoveAt(0);
            conversationMessages.Add(currentConversationLine);
        }

        currentConversationLine = "";
        UpdateConversationText();
    }



    /// <summary>
    /// called when new transcript chunk is received
    /// </summary>
    private void OnTranscriptReceived(string transcriptPart)
    {
        if (string.IsNullOrEmpty(currentConversationLine))
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            currentConversationLine = $"{timestamp}\t";
        }

        currentConversationLine += transcriptPart;

        UpdateConversationTextInPlace();
    }

    /// <summary>
    /// updates the conversation text in place
    /// </summary>
    private void UpdateConversationTextInPlace()
    {
        conversationText.text = "";

        for (int i = 0; i < conversationMessages.Count; i++)
        {
            float alpha = Mathf.Lerp(0.2f, 1.0f, (float)(i + 1) / conversationMessages.Count);
            string messageWithAlpha = $"<color=#{ColorUtility.ToHtmlStringRGBA(new Color(0, 0, 0, alpha))}>{conversationMessages[i]}</color>";
            conversationText.text += messageWithAlpha + "\n";
        }

        conversationText.text += $"<color=#{ColorUtility.ToHtmlStringRGBA(new Color(0, 0, 0, 1.0f))}>{currentConversationLine}</color>";
    }

    /// <summary>
    /// updates the conversation text UI
    /// </summary>
    private void UpdateConversationText()
    {
        conversationText.text = "";

        for (int i = 0; i < conversationMessages.Count; i++)
        {
            float alpha = Mathf.Lerp(0.2f, 1.0f, (float)(i + 1) / conversationMessages.Count);
            string messageWithAlpha = $"<color=#{ColorUtility.ToHtmlStringRGBA(new Color(0, 0, 0, alpha))}>{conversationMessages[i]}</color>";
            conversationText.text += messageWithAlpha + "\n";
        }
    }

    private void OnSessionCreated() => AddLogMessage("session created.");
    private void OnResponseCreated() => AddLogMessage("response created.");
    private void OnResponseDone() => AddLogMessage("response done.");
    private void OnVADRecordingStarted() => AddLogMessage("VAD recording started...");
    private void OnVADRecordingEnded() => AddLogMessage("VAD recording ended.");
}
