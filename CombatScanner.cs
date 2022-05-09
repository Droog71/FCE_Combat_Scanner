using UnityEngine;
using System;
using System.IO;
using System.Reflection;
using System.Collections;

public class CombatScanner : FortressCraftMod
{
    private bool usingScanner;
    private static AudioClip scannerSound;
    private AudioSource scannerAudio;
    private Camera mCam;
    private GameObject scanner;
    private Mesh scannerMesh;
    private float messageTimer;
    private Texture2D scannerTexture;
    private Coroutine audioLoadingCoroutine;
    private ParticleSystem scannerEffect;
    private string playerLocations;
    private static readonly string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    private static readonly string scannerModelPath = Path.Combine(assemblyFolder, "Models/scanner.obj");
    private static readonly string scannerTexturePath = Path.Combine(assemblyFolder, "Images/scanner.png");
    private static readonly string scannerAudioPath = Path.Combine(assemblyFolder, "Sounds/scanner.wav");
    private UriBuilder scannerTextureUriBuilder = new UriBuilder(scannerTexturePath);
    private UriBuilder scannerAudioUribuilder = new UriBuilder(scannerAudioPath);

    public override ModRegistrationData Register()
    {
        ModRegistrationData modRegistrationData = new ModRegistrationData();
        return modRegistrationData;
    }

    public IEnumerator Start()
    {
        scannerTextureUriBuilder.Scheme = "file";
        scannerTexture = new Texture2D(512, 512, TextureFormat.DXT5, false);

        using (WWW www = new WWW(scannerTextureUriBuilder.ToString()))
        {
            yield return www;
            www.LoadImageIntoTexture(scannerTexture);
        }

        ObjImporter importer = new ObjImporter();
        scannerMesh = importer.ImportFile(scannerModelPath);
    }

    private IEnumerator LoadScannerAudio()
    {
        scannerAudioUribuilder.Scheme = "file";
        scannerAudio = scanner.AddComponent<AudioSource>();
        using (WWW www = new WWW(scannerAudioUribuilder.ToString()))
        {
            yield return www;
            scannerSound = www.GetAudioClip();
            scannerAudio.clip = scannerSound;
        }
    }

    private void CreateScanner()
    {
        scanner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        scanner.transform.position = mCam.gameObject.transform.position + (mCam.gameObject.transform.forward * 1);
        scanner.transform.forward = mCam.transform.forward;
        scanner.GetComponent<MeshFilter>().mesh = scannerMesh;
        scanner.GetComponent<Renderer>().material.mainTexture = scannerTexture;
        audioLoadingCoroutine = StartCoroutine(LoadScannerAudio());
    }

    private void ManageScanner()
    {
        usingScanner &= WeaponSelectScript.meActiveWeapon == eWeaponType.eNone;
        scanner.GetComponent<Renderer>().enabled = usingScanner;
        scanner.transform.position = mCam.gameObject.transform.position + (mCam.gameObject.transform.forward * 1);
        scanner.transform.forward = mCam.transform.forward;
        if (usingScanner)
        {
            UIManager.UpdateUIRules("Weapon", UIRules.HideHotBar | UIRules.ShowSuitPanels | UIRules.HideCrossHair);
        }
    }

    private void Scan()
    {
        if (!scannerAudio.isPlaying && !UIManager.CursorShown)
        {
            if (SurvivalPowerPanel.mrSuitPower >= 50)
            {
                if (NetworkManager.instance != null)
                {
                    if (NetworkManager.instance.mClientThread != null)
                    {
                        Player player = NetworkManager.instance.mClientThread.mPlayer;
                        if (player != null)
                        {
                            float playerX = player.mnWorldX - 4611686017890516992L;
                            float playerY = player.mnWorldY - 4611686017890516992L;
                            float playerZ = player.mnWorldZ - 4611686017890516992L;
                            Vector3 playerPos = new Vector3(playerX, playerY, playerZ);

                            if (NetworkManager.instance.mClientThread.mOtherPlayers != null)
                            {
                                string playersFound = "";
                                foreach (Player otherPlayer in NetworkManager.instance.mClientThread.mOtherPlayers.Values)
                                {
                                    float otherPlayerX = otherPlayer.mnWorldX - 4611686017890516992L;
                                    float otherPlayerY = otherPlayer.mnWorldY - 4611686017890516992L;
                                    float otherPlayerZ = otherPlayer.mnWorldZ - 4611686017890516992L;
                                    Vector3 otherPlayerPos = new Vector3(otherPlayerX, otherPlayerY, otherPlayerZ);
                                    float distance = Vector3.Distance(playerPos, otherPlayerPos);
                                    if (distance <= 1000)
                                    {
                                        playersFound += otherPlayer.mUserName + ": (" + otherPlayerX + ", " + otherPlayerY + ", " + otherPlayerZ + ")\n";
                                    }
                                }
                                playerLocations = "[Combat Scanner]\n";
                                if (playersFound == "")
                                {
                                    playerLocations += "No targets in range.";
                                }
                                else
                                {
                                    playerLocations += playersFound;
                                }
                            }
                        }
                    }
                }
                if (scannerEffect == null)
                {
                    scannerEffect = SurvivalParticleManager.instance.PingLocationResponse;
                }
                scannerEffect.transform.position = scanner.transform.position;
                scannerEffect.Emit(15);
                SurvivalPowerPanel.mrSuitPower -= 50;
                scannerAudio.Play();
            }
        }
    }

    public void Update()
    {
        if (mCam == null)
        {
            Camera[] allCams = Camera.allCameras;
            foreach (Camera c in allCams)
            {
                if (c != null)
                {
                    if (c.gameObject.name.Equals("Camera"))
                    {
                        mCam = c;
                    }
                }
            }
        }
        else
        {
            if (scanner == null)
            {
                CreateScanner();
            }
            else
            {
                ManageScanner();
            }
        }

        if (GameState.PlayerSpawned)
        {
            UpdateGame();
        }
    }

    private void UpdateGame()
    {
        if (Input.GetKeyDown(KeyCode.Period))
        {
            if (usingScanner == false)
            {
                WeaponSelectScript.meNextWeapon = eWeaponType.eNone;
                usingScanner = true;
            }
            else
            {
                WeaponSelectScript.meNextWeapon = eWeaponType.eLaserDrill;
                usingScanner = false;
            }
        }

        if (usingScanner == true)
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                Scan();
            }

            if (ModManager.mModConfigurations.ModsById.ContainsKey("Maverick.FCE_PVP"))
            {
                if (Input.GetKeyDown(KeyCode.Comma))
                {
                    usingScanner = false;
                }
            }

            if (playerLocations != "")
            {
                messageTimer += 1 * Time.deltaTime;
                if (messageTimer >= 30)
                {
                    playerLocations = "";
                    messageTimer = 0;
                }
            }
        }
        else
        {
            playerLocations = "";
            messageTimer = 0;
        }
    }

    public void OnGUI()
    {
        if (playerLocations != "")
        {
            Rect infoRect = new Rect(Screen.width * 0.4f, Screen.height * 0.4f, 500, 250);
            int fontSize = GUI.skin.label.fontSize;
            FontStyle fontStyle = GUI.skin.label.fontStyle;
            GUI.skin.label.fontSize = 12;
            GUI.skin.label.fontStyle = FontStyle.Bold;
            GUI.Label(infoRect, playerLocations);
            GUI.skin.label.fontSize = fontSize;
            GUI.skin.label.fontStyle = fontStyle;
        }
    }
}
