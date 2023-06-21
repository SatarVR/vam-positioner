using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using System.Linq;
using System.IO;
using MeshVR;
using MVR.FileManagementSecure;
using Request = MeshVR.AssetLoader.AssetBundleFromFileRequest;
using AssetBundles;
using MVR;

public class Positioner : MVRScript
{
    private static string SAVE_PREFIX = "Positioner_";

    private Atom _containingAtom;
    private JSONStorableBool _isPositionerHost;
    protected List<string> MonitorCoordinatesStringList = new List<string>();
    protected List<string> monitorPositionChoices;
    protected JSONStorableStringChooser MonitorPositionSelector;
    protected List<UIDynamic> globalControlsUIs = new List<UIDynamic>();
    public JSONStorableString DialogTextUI;
    protected InputField DialogTextInputFieldUI;
    protected UIDynamicTextField dialogTitle;
    protected List<UIDynamic> dialogsComponentsUI = new List<UIDynamic>();
    private bool isInit = false;

    public override void Init()
    {
        _containingAtom = containingAtom;

        // add button
        UIDynamicButton newDialogBtn = CreateButton("New coords", true);
        newDialogBtn.button.onClick.AddListener(() => { OnAddNewDialog(); });
        globalControlsUIs.Add((UIDynamic)newDialogBtn);

        // Create UI elements
        CreateDialogUI();

        // MonitorPosition choices
        monitorPositionChoices = new List<string>();
        MonitorPositionSelector = new JSONStorableStringChooser("Monitor position ID", monitorPositionChoices, "", "Monitor position ID");
        MonitorPositionSelector.setCallbackFunction += (val) => { OnToggleDialogId(val); };
        UIDynamicPopup DSelsp = CreateScrollablePopup(MonitorPositionSelector, true);
        DSelsp.labelWidth = 250f;
        globalControlsUIs.Add((UIDynamic)DSelsp);

        SuperController.singleton.BroadcastMessage("OnActionsProviderAvailable", this, SendMessageOptions.DontRequireReceiver);

        if (enabled)
            OnEnable();
    }

    protected void OnAddNewDialog()
    {
        string dialogId = MonitorCoordinatesStringList.Count.ToString();

        var sc = SuperController.singleton;

        // get camera position
        var centerCameraPosition = sc.centerCameraTarget.transform.position;

        // get camera rotation?
        var monitorCenterCameraRotation = sc.MonitorCenterCamera.transform;

        // Here we add coordinates to our coordinates list
        //MonitorCoordinatesList.Add(new MonitorCoordinates(this, dialogId, centerCameraPosition, monitorCenterCameraRotation));

        MonitorCoordinates tmpCoords = new MonitorCoordinates(centerCameraPosition, monitorCenterCameraRotation);

        // We add the coordinates to a string list, so at each position in the list (ID), we have a set of coordinates
        MonitorCoordinatesStringList.Add(tmpCoords.MonitorCoordsToString());

        RefreshSelectors(dialogId);

        UpdateTextField(dialogId);
    }

    public void UpdateTextField(string dialogId)
    {
        SuperController.LogMessage($"updating textfield");

        int dialogIdInt = Int32.Parse(dialogId);
        DialogTextInputFieldUI.text = MonitorCoordinatesStringList[dialogIdInt];
    }

    protected void RefreshSelectors(string dialogId)
    {
        SuperController.LogMessage($"refreshing selector");

        // we need to add our monitor id to the choice selector
        monitorPositionChoices.Add(dialogId);

        MonitorPositionSelector.valNoCallback = "";
        MonitorPositionSelector.choices = monitorPositionChoices;
        MonitorPositionSelector.val = "" + dialogId;

        SuperController.LogMessage($"monitor position choices: " + MonitorPositionSelector.choices.Count);
        SuperController.LogMessage($"first monitor position choice: " + MonitorPositionSelector.choices[0]);
        SuperController.LogMessage($"last monitor position choice: " + MonitorPositionSelector.choices[MonitorPositionSelector.choices.Count - 1]);
    }

    protected void OnToggleDialogId(string dialogId)
    {
        SuperController.LogMessage($"Dialog id was toggled, isinit? " + isInit);

        // TODO: maybe remove this?
        if (isInit == false) return;

        // here we get the selected dialog ID and want to update the text field
        UpdateTextField(dialogId);
    }

    protected void CreateDialogUI()
    {
        DialogTextUI = new JSONStorableString("DialogTextUI", "_default_") { isStorable = false, isRestorable = false };

        // Temporary vars
        UIDynamicTextField tmpTextfield;

        // Creating Dialog components
        // ******* DIALOG TITLE (ID) ***********
        dialogTitle = createStaticDescriptionText("DialogTitle", "<color=#000><size=35><b>Dialog</b></size></color>", false, 55, TextAnchor.MiddleLeft);
        dialogsComponentsUI.Add((UIDynamic)dialogTitle);

        // ******* DIALOG TEXT ***********
        string newDefaultText = "Type some text...";
        DialogTextUI = new JSONStorableString("DialogTextUI", "_default_");
        tmpTextfield = CreateTextField(DialogTextUI);
        setupTextField(tmpTextfield, 550f, false, false);
        DialogTextInputFieldUI = tmpTextfield.UItext.gameObject.AddComponent<InputField>();
        DialogTextInputFieldUI.textComponent = tmpTextfield.UItext;
        DialogTextInputFieldUI.lineType = InputField.LineType.MultiLineNewline;
        dialogsComponentsUI.Add((UIDynamic)tmpTextfield);
        DialogTextUI.valNoCallback = newDefaultText;
        DialogTextInputFieldUI.text = newDefaultText;
        DialogTextInputFieldUI.onValueChanged.AddListener(delegate { OnDialogTextChanged(); });
    }

    protected void OnDialogTextChanged()
    {
        DialogTextUI.val = DialogTextInputFieldUI.text;

        // TODO: set the dialog text?
        //activeDialog.DialogText.val = DialogTextInputFieldUI.text;
    }

    private void setupTextField(UIDynamicTextField target, float fieldHeight, bool disableBackground = true, bool disableScroll = true)
    {
        if (disableBackground) target.backgroundColor = new Color(1f, 1f, 1f, 0f);
        LayoutElement tfLayout = target.GetComponent<LayoutElement>();
        tfLayout.preferredHeight = tfLayout.minHeight = fieldHeight;
        target.height = fieldHeight;
        if (disableScroll) disableScrollOnText(target);
    }

    public UIDynamicTextField createStaticDescriptionText(string DescTitle, string DescText, bool rightSide, int fieldHeight, TextAnchor textAlignment = TextAnchor.UpperLeft)
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
        protected JSONClass dialogsDatas { get; set; }
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
            if (dialogsDatas != null)
            {
                dialogsDatas.Remove(SAVE_PREFIX + "_" + MonitorCoordsID + "-Text");
            }
        }
    }
}
