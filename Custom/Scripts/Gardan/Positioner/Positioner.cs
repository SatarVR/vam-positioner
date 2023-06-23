using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using SimpleJSON;

public class Positioner : MVRScript
{
    private static string SAVE_PREFIX = "Positioner_";

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
    private bool isInit = false;

    public override void Init()
    {
        _containingAtom = containingAtom;

        // add button
        UIDynamicButton addCoordsBtn = CreateButton("Add coords", true);
        addCoordsBtn.button.onClick.AddListener(() => { OnAddNewCoords(); });
        globalControlsUIs.Add((UIDynamic)addCoordsBtn);

        // Create UI elements
        CreateCoordsUIelements();

        // MonitorPosition choices
        monitorPositionChoices = new List<string>();
        MonitorPositionSelector = new JSONStorableStringChooser("Monitor position ID", monitorPositionChoices, "", "Monitor position ID");
        MonitorPositionSelector.setCallbackFunction += (val) => { OnTogglecoordsId(val); };
        UIDynamicPopup DSelsp = CreateScrollablePopup(MonitorPositionSelector, true);
        DSelsp.labelWidth = 250f;
        globalControlsUIs.Add((UIDynamic)DSelsp);

        SuperController.singleton.BroadcastMessage("OnActionsProviderAvailable", this, SendMessageOptions.DontRequireReceiver);

        if (enabled)
            OnEnable();
    }

    protected void OnAddNewCoords()
    {
        string coordsId = MonitorCoordinatesStringList.Count.ToString();

        var sc = SuperController.singleton;

        // get camera position
        var centerCameraPosition = sc.centerCameraTarget.transform.position;

        // get camera rotation?
        var monitorCenterCameraRotation = sc.MonitorCenterCamera.transform;

        // Here we add coordinates to our coordinates list
        MonitorCoordinates tmpCoords = new MonitorCoordinates(centerCameraPosition, monitorCenterCameraRotation);

        // We add the coordinates to a string list, so at each position in the list (ID), we have a set of coordinates
        MonitorCoordinatesStringList.Add(tmpCoords.MonitorCoordsToString());

        RefreshSelectors(coordsId);

        UpdateTextField(coordsId);
    }

    public void UpdateTextField(string coordsId)
    {
        SuperController.LogMessage($"updating textfield");

        int coordsIdInt = Int32.Parse(coordsId);
        CoordsTextInputFieldUI.text = MonitorCoordinatesStringList[coordsIdInt];
        CoordsTextUI.val = CoordsTextInputFieldUI.text;
    }

    protected void RefreshSelectors(string coordsId)
    {
        /*
        SuperController.LogMessage($"refreshing selector");
        SuperController.LogMessage($"MonitorPositionSelector.val1: " + MonitorPositionSelector.val);
        SuperController.LogMessage($"MonitorPositionSelector.valNoCallback1: " + MonitorPositionSelector.valNoCallback);
        */

        // we need to add our monitor id to the choice selector
        monitorPositionChoices.Add(coordsId);

        MonitorPositionSelector.valNoCallback = "";
        MonitorPositionSelector.choices = null; // force UI sync
        MonitorPositionSelector.choices = monitorPositionChoices;
        MonitorPositionSelector.val = coordsId;

        /*
        SuperController.LogMessage($"monitor position choices: " + MonitorPositionSelector.choices.Count);
        SuperController.LogMessage($"first monitor position choice: " + MonitorPositionSelector.choices[0]);
        SuperController.LogMessage($"last monitor position choice: " + MonitorPositionSelector.choices[MonitorPositionSelector.choices.Count - 1]);
        SuperController.LogMessage($"MonitorPositionSelector.val2: " + MonitorPositionSelector.val);
        */
    }

    protected void OnTogglecoordsId(string coordsId)
    {
        //SuperController.LogMessage($"Coords id was toggled, isinit? " + isInit);

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
        string newDefaultText = "Type some text...";
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

        public string MonitorCoordsToString()
        {
            string coordsAsString = MonitorPosition.x + "_" + MonitorPosition.y + "_" + MonitorPosition.z + "_" + MonitorRotation.eulerAngles.x + "_" + MonitorRotation.eulerAngles.y + "_" + MonitorRotation.eulerAngles.z;
            return coordsAsString;
        }

        public void Delete()
        {
            // Clearing data from the loaded data of the scene (to prevent re-restore after deleting)
            // This only happens when you load a scene and have data loaded from the json
            if (coordsData != null)
            {
                coordsData.Remove(SAVE_PREFIX + "_" + MonitorCoordsID + "-Text");
            }
        }
    }
}
