using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif
using static CarViewer.UiFactory;

namespace CarViewer
{
    /// <summary>
    /// App entry point: starts the AR session (permission + ARCore checks), wires the
    /// placement controller, car model and voice tour, and builds the whole UI from code.
    /// </summary>
    public class ARCarApp : MonoBehaviour
    {
        [Header("AR")]
        public ARSession session;
        public ARCameraManager cameraManager;
        public ARPlacementController placement;

        [Header("Content")]
        public CarModel car;
        public GameObject carPrefab;
        public CarPaint[] paints;
        public VoiceTour voice;
        public TMP_FontAsset font;
        public Sprite logo;

        // Texts kept in one place for easy editing.
        const string AppTitle = "AR Car Viewer";
        const string Tagline = "Augmented reality car showroom";
        const string Author = "Made by Maheshwaran";
        const string GitHubUrl = "github.com/MAHESH21723";

        // UI references
        GameObject splash, hud, paintPanel, captionsPanel, infoModal, aboutModal, dialogModal;
        TextMeshProUGUI hintText, captionTime, dialogTitle, dialogBody, dialogButtonLabel;
        Image captionFill, hintBg;
        DockButton paintBtn, explodeBtn, spinBtn, voiceBtn, planesBtn, resetBtn;
        Image[] swatchRings;
        Sprite playSprite, pauseSprite;
        Action dialogAction;
        float hintHideAt;
        bool arStarted;

        void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            UiFactory.Font = font;

            // Show the real camera image behind the 3D content.
            if (cameraManager != null)
                cameraManager.requestedBackgroundRenderingMode = CameraBackgroundRenderingMode.BeforeOpaques;

            car.Build(carPrefab, paints);
            car.Hide();
            car.ApplyPaint(paints, 0);

            BuildUi();
            placement.StateChanged += OnPlacementState;
            placement.enabled = false;
            if (session != null) session.enabled = false;
            ShowHint("Move your phone slowly to scan the floor or a table", 0f);
        }

        void Update()
        {
            if (hintText != null && hintHideAt > 0f && Time.unscaledTime > hintHideAt)
            {
                hintBg.gameObject.SetActive(false);
                hintHideAt = 0f;
            }
            UpdateVoiceUi();
        }

        // ------------------------------------------------------------------
        // AR start-up
        // ------------------------------------------------------------------

        void OnStartPressed()
        {
            splash.SetActive(false);
            hud.SetActive(true);
            StartCoroutine(StartAr());
        }

        IEnumerator StartAr()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                bool answered = false, granted = false;
                var cb = new PermissionCallbacks();
                cb.PermissionGranted += _ => { answered = true; granted = true; };
                cb.PermissionDenied += _ => { answered = true; };
                cb.PermissionDeniedAndDontAskAgain += _ => { answered = true; };
                Permission.RequestUserPermission(Permission.Camera, cb);
                yield return new WaitUntil(() => answered);
                if (!granted)
                {
                    ShowDialog("Camera access needed",
                        "AR Car Viewer needs the camera to show the car in your room.\nPlease allow camera access in the app settings, then try again.",
                        "Try again", () => StartCoroutine(StartAr()));
                    yield break;
                }
            }
#endif
            yield return ARSession.CheckAvailability();
            if (ARSession.state == ARSessionState.NeedsInstall)
                yield return ARSession.Install();

            if (ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.NeedsInstall)
            {
                ShowDialog("AR not supported",
                    "This device does not support ARCore, or Google Play Services for AR is missing.\nYou can still read about the app, but the AR view needs a supported phone.",
                    "OK", null);
                yield break;
            }

            session.enabled = true;
            placement.enabled = true;
            arStarted = true;
        }

        void OnPlacementState(PlacementState s)
        {
            switch (s)
            {
                case PlacementState.Scanning:
                    ShowHint("Move your phone slowly to scan the floor or a table", 0f); break;
                case PlacementState.Ready:
                    ShowHint("Surface found. Aim the ring and tap to place the car", 0f); break;
                case PlacementState.Placed:
                    ShowHint("Pinch to scale  -  Drag to rotate  -  Tap to move", 6f); break;
            }
        }

        void ShowHint(string text, float seconds)
        {
            if (hintText == null) return;
            hintText.text = text;
            hintBg.gameObject.SetActive(true);
            hintHideAt = seconds > 0f ? Time.unscaledTime + seconds : 0f;
        }

        bool RequirePlaced()
        {
            if (placement.State == PlacementState.Placed) return true;
            ShowHint("Place the car on a surface first", 3f);
            return false;
        }

        // ------------------------------------------------------------------
        // Button handlers
        // ------------------------------------------------------------------

        void OnPaint()
        {
            if (!RequirePlaced()) return;
            paintPanel.SetActive(!paintPanel.activeSelf);
            paintBtn.SetActive(paintPanel.activeSelf);
        }

        void OnSelectPaint(int index)
        {
            car.ApplyPaint(paints, index);
            for (int i = 0; i < swatchRings.Length; i++) swatchRings[i].enabled = i == index;
        }

        void OnExplode()
        {
            if (!RequirePlaced()) return;
            car.SetExploded(!car.IsExploded);
            explodeBtn.SetActive(car.IsExploded);
            explodeBtn.label.text = car.IsExploded ? "Assemble" : "Explode";
        }

        void OnSpin()
        {
            if (!RequirePlaced()) return;
            car.AutoRotate = !car.AutoRotate;
            spinBtn.SetActive(car.AutoRotate);
        }

        void OnVoice()
        {
            voice.Toggle();
        }

        void OnPlanes()
        {
            placement.SetPlanesVisible(!placement.PlanesVisible);
            planesBtn.SetActive(placement.PlanesVisible);
        }

        void OnReset()
        {
            placement.ResetPlacement();
            voice.Stop();
            paintPanel.SetActive(false);
            paintBtn.SetActive(false);
            explodeBtn.SetActive(false);
            explodeBtn.label.text = "Explode";
            spinBtn.SetActive(false);
            ShowHint("Aim the ring at a surface and tap to place the car", 0f);
        }

        void UpdateVoiceUi()
        {
            if (voiceBtn == null) return;
            bool playing = voice.IsPlaying;
            voiceBtn.icon.sprite = playing ? pauseSprite : playSprite;
            voiceBtn.SetActive(playing);
            bool show = voice.IsActive;
            if (captionsPanel.activeSelf != show) captionsPanel.SetActive(show);
            if (show)
            {
                captionFill.fillAmount = voice.Time01;
                captionTime.text = $"{FormatTime(voice.TimeSeconds)} / {FormatTime(voice.Length)}";
            }
        }

        static string FormatTime(float s) => $"{(int)(s / 60)}:{(int)(s % 60):00}";

        // ------------------------------------------------------------------
        // UI construction
        // ------------------------------------------------------------------

        void BuildUi()
        {
            var canvasGo = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            playSprite = UiIcons.Play();
            pauseSprite = UiIcons.Pause();

            var safe = NewRect(canvasGo.transform, "SafeArea");
            safe.gameObject.AddComponent<SafeArea>();
            Stretch(safe);

            BuildHud(safe);
            hud.SetActive(false);
            BuildSplash(canvasGo.transform, safe);
            BuildInfoModal(canvasGo.transform);
            BuildAboutModal(canvasGo.transform);
            BuildDialog(canvasGo.transform);
        }

        void BuildHud(RectTransform safe)
        {
            var root = NewRect(safe, "HUD");
            Stretch(root);
            hud = root.gameObject;

            // Title pill
            var title = Panel(root, "TitlePill", Palette.Midnight);
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(560, 110));
            if (logo != null)
            {
                var lg = Icon(title.transform, logo, Color.white, "Logo");
                Anchor(lg.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(80, 80));
            }
            var tt = Label(title.transform, AppTitle.ToUpperInvariant(), 44, Palette.Text, TextAlignmentOptions.MidlineLeft);
            tt.fontStyle = FontStyles.Bold;
            Stretch(tt.rectTransform, 112, 0, 12, 0);

            // Info + About
            var info = MakeRoundButton(root, "InfoButton", null, "i", Palette.Midnight, Palette.Cyan);
            Anchor((RectTransform)info.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(110, 110));
            info.onClick.AddListener(() => infoModal.SetActive(true));
            var about = MakeRoundButton(root, "AboutButton", null, "?", Palette.Midnight, Palette.Amber);
            Anchor((RectTransform)about.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-150, -24), new Vector2(110, 110));
            about.onClick.AddListener(() => aboutModal.SetActive(true));

            // Hint
            hintBg = Panel(root, "Hint", Palette.Graphite);
            Anchor(hintBg.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(980, 100));
            hintText = Label(hintBg.transform, "", 36, Palette.Cyan);
            Stretch(hintText.rectTransform, 24, 0, 24, 0);

            // Captions / voice progress
            var cap = Panel(root, "Captions", Palette.Midnight);
            captionsPanel = cap.gameObject;
            Anchor(cap.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -280), new Vector2(980, 150));
            var ct = Label(cap.transform, "Voice tour", 38, Palette.Amber, TextAlignmentOptions.MidlineLeft);
            Anchor(ct.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -14), new Vector2(500, 56));
            captionTime = Label(cap.transform, "0:00 / 0:00", 34, Palette.Muted, TextAlignmentOptions.MidlineRight);
            Anchor(captionTime.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -14), new Vector2(360, 56));
            var barBg = Panel(cap.transform, "BarBg", Palette.GraphiteLight);
            Anchor(barBg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(920, 22));
            var fill = Panel(barBg.transform, "BarFill", Palette.Cyan, false);
            fill.sprite = UiIcons.Circle();
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            captionFill = fill;
            captionsPanel.SetActive(false);

            BuildDock(root);
        }

        void BuildDock(RectTransform root)
        {
            // Paint popup
            var paint = Panel(root, "PaintPanel", Palette.Midnight);
            paintPanel = paint.gameObject;
            Anchor(paint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 224), new Vector2(1032, 200));
            var pl = Label(paint.transform, "PAINT", 34, Palette.Muted, TextAlignmentOptions.MidlineLeft);
            Anchor(pl.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -12), new Vector2(500, 50));
            var row = NewRect(paint.transform, "Swatches");
            Anchor(row, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(980, 120));
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.spacing = 28;
            hl.childControlWidth = hl.childControlHeight = false;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            swatchRings = new Image[paints.Length];
            for (int i = 0; i < paints.Length; i++)
            {
                int idx = i;
                var sw = MakeRoundButton(row, "Swatch" + paints[i].name, null, "", paints[i].swatch, Color.white);
                ((RectTransform)sw.transform).sizeDelta = new Vector2(112, 112);
                var ring = Icon(sw.transform, UiIcons.Ring(), Palette.Amber, "Selected");
                Stretch(ring.rectTransform, -10, -10, -10, -10);
                ring.enabled = i == 0;
                swatchRings[i] = ring;
                sw.onClick.AddListener(() => OnSelectPaint(idx));
            }
            paintPanel.SetActive(false);

            // Dock
            var dock = Panel(root, "Dock", Palette.Midnight);
            Anchor(dock.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(1032, 180));
            var h = dock.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 10, 10);
            h.spacing = 4;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;

            paintBtn = MakeDockButton(dock.transform, UiIcons.Paint(), "Paint");
            explodeBtn = MakeDockButton(dock.transform, UiIcons.Explode(), "Explode");
            spinBtn = MakeDockButton(dock.transform, UiIcons.Rotate(), "Spin");
            voiceBtn = MakeDockButton(dock.transform, playSprite, "Voice");
            planesBtn = MakeDockButton(dock.transform, UiIcons.Planes(), "Planes");
            resetBtn = MakeDockButton(dock.transform, UiIcons.Reset(), "Reset");
            planesBtn.SetActive(true);

            paintBtn.button.onClick.AddListener(OnPaint);
            explodeBtn.button.onClick.AddListener(OnExplode);
            spinBtn.button.onClick.AddListener(OnSpin);
            voiceBtn.button.onClick.AddListener(OnVoice);
            planesBtn.button.onClick.AddListener(OnPlanes);
            resetBtn.button.onClick.AddListener(OnReset);
        }

        void BuildSplash(Transform canvas, RectTransform safe)
        {
            var bg = Panel(canvas, "Splash", Palette.MidnightSolid, false);
            Stretch(bg.rectTransform);
            splash = bg.gameObject;
            var content = NewRect(bg.transform, "Content");
            Stretch(content);
            content.gameObject.AddComponent<SafeArea>();

            if (logo != null)
            {
                var lg = Icon(content, logo, Color.white, "Logo");
                Anchor(lg.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(340, 340));
            }
            var title = Label(content, AppTitle.ToUpperInvariant(), 92, Palette.Text, TextAlignmentOptions.Center, "Title");
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -510), new Vector2(1000, 120));
            var sub = Label(content, Tagline, 44, Palette.Cyan, TextAlignmentOptions.Center, "Tagline");
            Anchor(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -630), new Vector2(1000, 70));

            string[] steps =
            {
                "Point your phone at the floor or a table",
                "Tap to place the car at real-life size",
                "Pinch to scale, drag to rotate, explore"
            };
            for (int i = 0; i < steps.Length; i++)
            {
                var card = Panel(content, "Step" + (i + 1), Palette.Graphite);
                Anchor(card.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -760 - i * 130), new Vector2(940, 110));
                var num = Panel(card.transform, "Num", Palette.Amber, false);
                num.sprite = CircleSprite;
                Anchor(num.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(70, 70));
                var nt = Label(num.transform, (i + 1).ToString(), 44, Palette.MidnightSolid);
                nt.fontStyle = FontStyles.Bold;
                Stretch(nt.rectTransform);
                var st = Label(card.transform, steps[i], 38, Palette.Text, TextAlignmentOptions.MidlineLeft);
                Stretch(st.rectTransform, 120, 0, 20, 0);
            }

            var start = MakeTextButton(content, "START", Palette.Amber, Palette.MidnightSolid, new Vector2(620, 150));
            Anchor((RectTransform)start.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(620, 150));
            start.onClick.AddListener(OnStartPressed);

            var made = Label(content, Author, 40, Palette.Amber, TextAlignmentOptions.Center, "Author");
            Anchor(made.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(900, 60));
            var gh = Label(content, GitHubUrl, 32, Palette.Muted, TextAlignmentOptions.Center, "GitHub");
            Anchor(gh.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(900, 50));
        }

        /// <summary>Creates a dimmed full-screen backdrop with a centred card. Tapping the backdrop closes it.</summary>
        GameObject MakeModal(Transform canvas, string name, string title, float height, out RectTransform card)
        {
            var scrim = Panel(canvas, name, Palette.Scrim, false);
            Stretch(scrim.rectTransform);
            var sb = scrim.gameObject.AddComponent<Button>();
            sb.targetGraphic = scrim;
            sb.transition = Selectable.Transition.None;
            GameObject modal = scrim.gameObject;
            sb.onClick.AddListener(() => modal.SetActive(false));

            var safeInner = NewRect(scrim.transform, "Safe");
            Stretch(safeInner);
            safeInner.gameObject.AddComponent<SafeArea>();

            var c = Panel(safeInner, "Card", Palette.MidnightSolid);
            Anchor(c.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, height));
            var cardBtn = c.gameObject.AddComponent<Button>(); // swallow taps so the scrim does not close
            cardBtn.transition = Selectable.Transition.None;
            var tl = Label(c.transform, title, 58, Palette.Amber, TextAlignmentOptions.MidlineLeft);
            tl.fontStyle = FontStyles.Bold;
            Anchor(tl.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -30), new Vector2(700, 90));
            var close = MakeRoundButton(c.transform, "Close", UiIcons.Close(), null, Palette.GraphiteLight, Palette.Text);
            Anchor((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(90, 90));
            close.onClick.AddListener(() => modal.SetActive(false));
            card = c.rectTransform;
            modal.SetActive(false);
            return modal;
        }

        void BuildInfoModal(Transform canvas)
        {
            infoModal = MakeModal(canvas, "InfoModal", "Car info", 900, out var card);
            var rows = new[]
            {
                ("Model", "ARCADE Racer"),
                ("Engine", "2.0L Turbo, 4-cyl"),
                ("Top speed", "280 km/h"),
                ("0 - 100 km/h", "3.9 s"),
                ("Price", "$49,900")
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var line = Panel(card, "Row" + i, i % 2 == 0 ? Palette.Graphite : new Color(0, 0, 0, 0));
                Anchor(line.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150 - i * 110), new Vector2(880, 96));
                var l = Label(line.transform, rows[i].Item1, 40, Palette.Muted, TextAlignmentOptions.MidlineLeft);
                Stretch(l.rectTransform, 30, 0, 0, 0);
                var v = Label(line.transform, rows[i].Item2, 44, Palette.Text, TextAlignmentOptions.MidlineRight);
                v.fontStyle = FontStyles.Bold;
                Stretch(v.rectTransform, 0, 0, 30, 0);
            }
            var note = Label(card, "Sample showroom values for demonstration only. Not real specifications.", 32, Palette.Cyan,
                TextAlignmentOptions.Center);
            Anchor(note.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(860, 110));
        }

        void BuildAboutModal(Transform canvas)
        {
            aboutModal = MakeModal(canvas, "AboutModal", "About", 1240, out var card);
            var name = Label(card, AppTitle, 60, Palette.Text, TextAlignmentOptions.Center);
            name.fontStyle = FontStyles.Bold;
            Anchor(name.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(880, 80));
            var ver = Label(card, "Version " + Application.version, 38, Palette.Muted);
            Anchor(ver.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -225), new Vector2(880, 55));
            var made = Label(card, Author, 46, Palette.Amber);
            Anchor(made.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(880, 70));
            var gh = Label(card, GitHubUrl, 40, Palette.Cyan);
            Anchor(gh.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -365), new Vector2(880, 60));

            var credits = Label(card,
                "<color=#8FA0B8>CREDITS</color>\n" +
                "Car model: \"ARCADE - FREE Racing Car\" from the Unity Asset Store, by its author\n" +
                "Voice tour: generated with ElevenLabs\n" +
                "Font: Rajdhani, SIL Open Font License\n" +
                "Built with Unity, AR Foundation and Google ARCore",
                34, Palette.Text);
            Anchor(credits.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(860, 650));
            credits.alignment = TextAlignmentOptions.Top;
            credits.lineSpacing = 18f;
        }

        void BuildDialog(Transform canvas)
        {
            dialogModal = MakeModal(canvas, "Dialog", "", 700, out var card);
            // Dialog should not close on backdrop tap.
            dialogModal.GetComponent<Button>().onClick.RemoveAllListeners();
            foreach (var b in card.GetComponentsInChildren<Button>(true))
                if (b.name == "Close") b.gameObject.SetActive(false);

            dialogTitle = card.Find("Text") != null ? card.Find("Text").GetComponent<TextMeshProUGUI>() : null;
            dialogBody = Label(card, "", 40, Palette.Text);
            Anchor(dialogBody.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(860, 340));
            dialogBody.alignment = TextAlignmentOptions.Top;
            var btn = MakeTextButton(card, "OK", Palette.Amber, Palette.MidnightSolid, new Vector2(520, 130));
            Anchor((RectTransform)btn.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(520, 130));
            dialogButtonLabel = btn.GetComponentInChildren<TextMeshProUGUI>();
            btn.onClick.AddListener(() =>
            {
                dialogModal.SetActive(false);
                var a = dialogAction;
                dialogAction = null;
                a?.Invoke();
            });
        }

        void ShowDialog(string title, string body, string button, Action onButton)
        {
            if (dialogTitle != null) dialogTitle.text = title;
            dialogBody.text = body;
            dialogButtonLabel.text = button;
            dialogAction = onButton;
            dialogModal.SetActive(true);
        }
    }
}

