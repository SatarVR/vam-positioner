using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;

public class Positioner : MVRScript
{
    private Atom _containingAtom;
    private JSONStorableBool _isPositionerHost;
    protected List<string> MonitorCoordinatesStringList = new List<string>();
    protected List<string> monitorPositionChoices;
    protected JSONStorableStringChooser MonitorPositionSelector;
    protected List<UIDynamic> globalControlsUIs = new List<UIDynamic>();
    public JSONStorableString CoordsTextUI;
    protected InputField CoordsTextInputFieldUI;
    protected UIDynamicTextField coordsTextTitle;
    protected List<UIDynamic> coordsComponentsUI = new List<UIDynamic>();

    protected Dictionary<string, string> testDict = new Dictionary<string, string>();

    protected bool isInit = false;

    private JSONStorableBool _spawnOnEnable;

    public override void Init()
    {
        isInit = true;

        _containingAtom = containingAtom;

        // add button
        UIDynamicButton addCoordsBtn = CreateButton("Add camera coords", true);
        addCoordsBtn.button.onClick.AddListener(() => { OnAddNewCoords(); });
        globalControlsUIs.Add((UIDynamic)addCoordsBtn);

        // test button
        UIDynamicButton setCoordsBtn = CreateButton("Set camera to coords", true);
        setCoordsBtn.button.onClick.AddListener(() => { SetCoords(); });
        globalControlsUIs.Add((UIDynamic)setCoordsBtn);


        // Create UI elements
        CreateCoordsUIelements();

        // MonitorPosition choices
        monitorPositionChoices = new List<string>();
        MonitorPositionSelector = new JSONStorableStringChooser("Monitor position ID", monitorPositionChoices, "", "Monitor position ID");
        MonitorPositionSelector.isRestorable = true;
        MonitorPositionSelector.isStorable = true;
        MonitorPositionSelector.storeType = JSONStorableParam.StoreType.Full;
        RegisterStringChooser(MonitorPositionSelector);
        MonitorPositionSelector.setCallbackFunction += (val) => { OnTogglecoordsId(val); };
        UIDynamicPopup DSelsp = CreateScrollablePopup(MonitorPositionSelector, true);
        DSelsp.labelWidth = 250f;
        globalControlsUIs.Add((UIDynamic)DSelsp);

        // SuperController.singleton.BroadcastMessage("OnActionsProviderAvailable", this, SendMessageOptions.DontRequireReceiver);

        // ******* CREATING MAIN TRIGGERS/ACTIONS ********
        // They are here to be displayed on TOP of every other JSONStorables
        JSONStorableAction fakeFuncUseBelow = new JSONStorableAction("- - - - Use these functions below ↓ - - - - -", () => {});
        RegisterAction(fakeFuncUseBelow);

        // This should show an action so that the monitor ID can be selected and called from another plugin
        JSONStorableStringChooser A_SetMonitorCoords = new JSONStorableStringChooser("Set Camera Position", monitorPositionChoices, "", "Set Camera Position") 
        {isStorable=false,isRestorable=false};
        A_SetMonitorCoords.setCallbackFunction += (val) => { OnSetCoordsAction( val ); };
        RegisterStringChooser(A_SetMonitorCoords);


        StartCoroutine(InitDeferred());


        if (enabled)
        {
            OnEnable();
        }

        isInit = false;
    }

    // Got this from AcidBubbles code, kudos.
    private IEnumerator InitDeferred()
    {
        yield return new WaitForEndOfFrame();
        if (!enabled) yield break;
        if (_spawnOnEnable.val)
            SetCoords();
        yield return 0;
        if (!enabled) yield break;
        if (_spawnOnEnable.val)
            SetCoords();
    }

    // We need to override this function, to save a custom array in the savegame file
    public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
    {
        //SuperController.LogMessage($"GETJSON called");

        JSONClass jsonObject = base.GetJSON(includePhysical, includeAppearance, forceStore);

        // Store MonitorCoordinatesStringList
        JSONArray monitorCoordsArray = new JSONArray();
        foreach (string coord in MonitorCoordinatesStringList)
        {
            monitorCoordsArray.Add(coord);
        }
        jsonObject["monitorCoordinatesStringList"] = monitorCoordsArray;

        return jsonObject;
    }

    // We also need to override this to restore our custom array from the savegame file
    public override void LateRestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, bool setMissingToDefault = true)
    {
        base.LateRestoreFromJSON(jc, restorePhysical, restoreAppearance, setMissingToDefault);

        JSONArray coordsList = jc["monitorCoordinatesStringList"].AsArray;
        MonitorCoordinatesStringList.Clear();

        // Fill our string list with the saved coordinates
        foreach (JSONNode node in coordsList)
        {
            string coords = node.Value;
            MonitorCoordinatesStringList.Add(coords);
        }

        // SuperController.LogMessage($"AFTER Load stringlist count: " + MonitorCoordinatesStringList.Count);

        // Fill the slider with the ID's, so we can select the coordinates from the string list
        monitorPositionChoices.Clear();
        for (int i = 0; i < MonitorCoordinatesStringList.Count; i++)
        {
            monitorPositionChoices.Add(i.ToString());
        }

        MonitorPositionSelector.valNoCallback = "";
        MonitorPositionSelector.choices = null; // force UI sync
        MonitorPositionSelector.choices = monitorPositionChoices;

        // default to 0
        MonitorPositionSelector.val = "0";
        UpdateTextField("0");
    }

    // This happens when the button add coords is pressed
    protected void OnAddNewCoords()
    {
        string coordsId = MonitorCoordinatesStringList.Count.ToString();

        var sc = SuperController.singleton;

        // get camera position
        var centerCameraPosition = sc.centerCameraTarget.transform.position;

        // get camera rotation
        var monitorCenterCameraRotation = sc.MonitorCenterCamera.transform;

        // Here we add coordinates to our coordinates list
        MonitorCoordinates tmpCoords = new MonitorCoordinates(centerCameraPosition, monitorCenterCameraRotation);

        // We add the coordinates to a string list, so at each position in the list (ID), we have a set of coordinates
        MonitorCoordinatesStringList.Add(tmpCoords.MonitorCoordsToString());

        RefreshSelectors(coordsId);

        UpdateTextField(coordsId);
    }

    // Read the coordinates from the UI text field and set the camera to that position
    protected void SetCoords()
    {
        string[] coordsStringArray = CoordsTextInputFieldUI.text.Split('_');

        if (coordsStringArray.Length == 6)
        {
            try
            {
                Vector3 newCenterCameraPosition = new Vector3(float.Parse(coordsStringArray[0]), float.Parse(coordsStringArray[1]), float.Parse(coordsStringArray[2]));
                Vector3 newMonitorCenterCameraRotation = new Vector3(float.Parse(coordsStringArray[3]), float.Parse(coordsStringArray[4]), float.Parse(coordsStringArray[5]));

                SetCoords(newCenterCameraPosition, newMonitorCenterCameraRotation);
            }
            catch (Exception e)
            {
                SuperController.LogError($"Could not parse coordinates from the text field.");
                SuperController.LogError(e.Message);
            }
        }
        else
        {
            SuperController.LogError($"Could not parse coordinates from the text field, need exactly 6 coordinates (position x,y,z and rotation x,y,z).");
        }
    }

        // Read the coordinates from the UI text field and set the camera to that position
    protected void OnSetCoordsAction(string CoordsId)
    {
        SuperController.LogMessage("Trying to set coords for id requested by foreign action: '" + CoordsId + "'");

        // get string from list by id
        string coordsString = MonitorCoordinatesStringList[Int32.Parse(CoordsId)];

        string[] coordsStringArray = coordsString.Split('_');

        if (coordsStringArray.Length == 6)
        {
            try
            {
                Vector3 newCenterCameraPosition = new Vector3(float.Parse(coordsStringArray[0]), float.Parse(coordsStringArray[1]), float.Parse(coordsStringArray[2]));
                Vector3 newMonitorCenterCameraRotation = new Vector3(float.Parse(coordsStringArray[3]), float.Parse(coordsStringArray[4]), float.Parse(coordsStringArray[5]));

                SetCoords(newCenterCameraPosition, newMonitorCenterCameraRotation);
            }
            catch (Exception e)
            {
                SuperController.LogError($"Could not parse coordinates from the text field.");
                SuperController.LogError(e.Message);
            }
        }
        else
        {
            SuperController.LogError($"Could not parse coordinates from the text field, need exactly 6 coordinates (position x,y,z and rotation x,y,z).");
        }
    }


    // Here we read the coordinates from the UI text field and set the camera to that position
    protected void SetCoords(Vector3 newCenterCameraPosition, Vector3 newMonitorCenterCameraRotation)
    {
        // SuperController.LogMessage($"SetCoords called");

        var sc = SuperController.singleton;
        
        // This part is copied from the SpawnPoint script from AcidBubbles -> Kudos man!
        // I would have NEVER figured out, how to calculate this stuff
        sc.ResetNavigationRigPositionRotation();

        var navigationRigTransform = sc.navigationRig.transform;

        navigationRigTransform.eulerAngles = newMonitorCenterCameraRotation;

        var targetPosition = newCenterCameraPosition;
        navigationRigTransform.eulerAngles = new Vector3(0, newMonitorCenterCameraRotation.y, 0);

        var centerCameraPosition = sc.centerCameraTarget.transform.position;
        var teleportPosition = targetPosition + (navigationRigTransform.position - centerCameraPosition);

        navigationRigTransform.position = new Vector3(teleportPosition.x, 0, teleportPosition.z);
        sc.playerHeightAdjust += (targetPosition.y - centerCameraPosition.y);

        var monitorCenterCameraTransform = sc.MonitorCenterCamera.transform;
        monitorCenterCameraTransform.eulerAngles = newMonitorCenterCameraRotation;
        monitorCenterCameraTransform.localEulerAngles = new Vector3(monitorCenterCameraTransform.localEulerAngles.x, 0, 0);

        //SuperController.LogMessage($"SetCoords end");
    }

    public void UpdateTextField(string coordsId)
    {
        // SuperController.LogMessage("updating textfield");

        int coordsIdInt = Int32.Parse(coordsId);
        if (coordsIdInt >= 0 && coordsIdInt < MonitorCoordinatesStringList.Count)
        {
            CoordsTextInputFieldUI.text = MonitorCoordinatesStringList[coordsIdInt];
            CoordsTextUI.val = CoordsTextInputFieldUI.text;
        }
        else
        {
            SuperController.LogError("Invalid coordsId: " + coordsId);
        }
    }

    protected void RefreshSelectors(string coordsId)
    {
        // we need to add our monitor id to the choice selector
        monitorPositionChoices.Add(coordsId);

        MonitorPositionSelector.valNoCallback = "";
        MonitorPositionSelector.choices = null; // force UI sync
        MonitorPositionSelector.choices = monitorPositionChoices;
        MonitorPositionSelector.val = coordsId;
    }

    protected void OnTogglecoordsId(string coordsId)
    {
        // here we get the selected ID and want to update the text field
        UpdateTextField(coordsId);
    }

    protected void CreateCoordsUIelements()
    {
        CoordsTextUI = new JSONStorableString("CoordsTextUI", "_default_") { isStorable = false, isRestorable = false };

        // Temporary vars
        UIDynamicTextField tmpTextfield;

        // Creating components
        // ******* COORDS TEXTFIELD TITLE ***********
        coordsTextTitle = CreateStaticDescriptionText("coordsTextTitle", "<color=#000><size=35><b>Coords</b></size></color>", false, 55, TextAnchor.MiddleLeft);
        coordsComponentsUI.Add((UIDynamic)coordsTextTitle);

        // ******* COORDS TEXTFIELD TEXT ***********
        string newDefaultText = "Treat this as a read-only field, don't type in it.";
        CoordsTextUI = new JSONStorableString("CoordsTextUI", "_default_");
        tmpTextfield = CreateTextField(CoordsTextUI);
        SetupTextField(tmpTextfield, 550f, false, false);
        CoordsTextInputFieldUI = tmpTextfield.UItext.gameObject.AddComponent<InputField>();
        CoordsTextInputFieldUI.textComponent = tmpTextfield.UItext;
        CoordsTextInputFieldUI.lineType = InputField.LineType.MultiLineNewline;
        coordsComponentsUI.Add((UIDynamic)tmpTextfield);
        CoordsTextUI.valNoCallback = newDefaultText;
        CoordsTextInputFieldUI.text = newDefaultText;
        CoordsTextInputFieldUI.onValueChanged.AddListener(delegate { OnCoordsTextChanged(); });
    }

    protected void OnCoordsTextChanged()
    {
        CoordsTextUI.val = CoordsTextInputFieldUI.text;
    }

    private void SetupTextField(UIDynamicTextField target, float fieldHeight, bool disableBackground = true, bool disableScroll = true)
    {
        if (disableBackground) target.backgroundColor = new Color(1f, 1f, 1f, 0f);
        LayoutElement tfLayout = target.GetComponent<LayoutElement>();
        tfLayout.preferredHeight = tfLayout.minHeight = fieldHeight;
        target.height = fieldHeight;
        if (disableScroll) disableScrollOnText(target);
    }

    public UIDynamicTextField CreateStaticDescriptionText(string DescTitle, string DescText, bool rightSide, int fieldHeight, TextAnchor textAlignment = TextAnchor.UpperLeft)
    {
        JSONStorableString staticDescString = new JSONStorableString(DescTitle, DescText) { isStorable = false, isRestorable = false };
        staticDescString.hidden = true;
        UIDynamicTextField staticDescStringField = CreateTextField(staticDescString, rightSide);
        staticDescStringField.backgroundColor = new Color(1f, 1f, 1f, 0f);
        staticDescStringField.UItext.alignment = textAlignment;
        LayoutElement sdsfLayout = staticDescStringField.GetComponent<LayoutElement>();
        sdsfLayout.preferredHeight = sdsfLayout.minHeight = fieldHeight;
        staticDescStringField.height = fieldHeight;
        disableScrollOnText(staticDescStringField);

        return staticDescStringField;
    }

    private static void disableScrollOnText(UIDynamicTextField target)
    {
        ScrollRect targetSR = target.UItext.transform.parent.transform.parent.transform.parent.GetComponent<ScrollRect>();
        if (targetSR != null)
        {
            targetSR.horizontal = false;
            targetSR.vertical = false;
        }
    }

    private void OnEnable()
    {
        if (_containingAtom == null) return;
        if (_containingAtom.IsBoolJSONParam("IsPositionerHost")) return;
        _isPositionerHost = new JSONStorableBool("IsPositionerHost", true);
        _containingAtom.RegisterBool(_isPositionerHost);
    }

    private void OnDisable()
    {
        if (_isPositionerHost == null) return;
        _containingAtom.DeregisterBool(_isPositionerHost);
        _isPositionerHost = null;
    }

    public void OnBindingsListRequested(List<object> bindings)
    {
        // bindings.Add(new JSONStorableAction("addCoords", AddNewCoords));

        // TODO: will want to register an action, so that I can set the monitor position to a certain ID from another plugin

    }

    public void OnDestroy()
    {
        SuperController.singleton.BroadcastMessage("OnActionsProviderDestroyed", this, SendMessageOptions.DontRequireReceiver);
    }

    public class MonitorCoordinates
    {
        protected JSONClass coordsData { get; set; }
        public int MonitorCoordsID;
        public Vector3 MonitorPosition;
        public Transform MonitorRotation;

        public MonitorCoordinates(Vector3 monitorPosition, Transform monitorRotation)
        {
            MonitorPosition = monitorPosition;
            MonitorRotation = monitorRotation;
        }

        // This is the string representation of the monitor coordinates, that we can parse again later
        // I'm sure there's a fancy way of doing this in JSON
        public string MonitorCoordsToString()
        {
            string coordsAsString = MonitorPosition.x + "_" + MonitorPosition.y + "_" + MonitorPosition.z + "_" + MonitorRotation.eulerAngles.x + "_" + MonitorRotation.eulerAngles.y + "_" + MonitorRotation.eulerAngles.z;
            return coordsAsString;
        }
    }
}
