using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using SimpleJSON;
using System.Text.RegularExpressions;

public class Positioner : MVRScript
{
    protected Atom ContainingAtom;
    protected JSONStorableBool IsPositionerHost;
    protected List<string> GroupList = new List<string>();
    protected List<MonitorCoordinates> MonitorCoordinatesList = new List<MonitorCoordinates>();
    protected JSONStorableStringChooser MonitorPositionChooser;
    protected JSONStorableStringChooser GroupChooser;
    protected List<UIDynamic> globalControlsUIs = new List<UIDynamic>();
    protected JSONStorableString CoordsTextUI;
    protected JSONStorableString CameraTextUI;
    protected JSONStorableString HelpText;
    protected InputField CoordsTextInputFieldUI;
    protected InputField CameraTitleInputFieldUI;
    protected JSONStorableString cameraTitle;
    protected UIDynamicTextField UICoordsSectionTitle;
    protected UIDynamicTextField UICameraSectionTitle;
    protected string SelectedMonitorChooserTitle = "";
    protected string SelectedGroupId = "0";
    protected bool isInit = false;

    public override void Init()
    {
        isInit = true;

        ContainingAtom = containingAtom;

        // Create UI elements
        CreateCoordsUIelements();

        // Init variables
        // Initialize GroupList with one item
        if (GroupList.Count == 0)
        {
            GroupList.Add("0");
            SelectedGroupId = "0";
            GroupChooser.val = "0";
        }

        // Register Actions
        RegisterActions();

        // Not sure what this does, but it sounds like it will wait for the end of a frame
        // before changing the camera position
        // StartCoroutine(InitDeferred());

        // This registers the plugin on the containing Atom, sounds like a good idea
        if (enabled)
        {
            OnEnable();
        }
        else
        {
            OnDisable();
        }

        isInit = false;
    }

    protected void RegisterActions()
    {
        // ******* CREATING MAIN TRIGGERS/ACTIONS ********
        // They are here to be displayed on TOP of every other JSONStorables
        JSONStorableAction fakeFuncUseBelow = new JSONStorableAction("- - - - Use these functions below ↓ - - - - -", () => { });
        RegisterAction(fakeFuncUseBelow);

        // Add action to show the selected camera
        JSONStorableStringChooser A_SetMonitorCoords = new JSONStorableStringChooser("Set Camera Position", GetMonitorCoordinatesStringList(MonitorCoordinatesList), "", "Set Camera Position")
        { isStorable = false, isRestorable = false };
        A_SetMonitorCoords.setCallbackFunction += (val) => { OnSetCoordsAction(val); };
        RegisterStringChooser(A_SetMonitorCoords);

        SetupAction(this, "Set Random Camera Position (any group)", OnSetCoordsActionRandomAnyGroup);

        JSONStorableStringChooser A_RandomSpecificGroup = new JSONStorableStringChooser("Set Random Camera Position (specific group)", GroupList, "", "Set Random Camera Position (specific group)")
        { isStorable = false, isRestorable = false };
        A_RandomSpecificGroup.setCallbackFunction += (val) => { OnSetCoordsActionRandomSpecificGroup(val); };
        RegisterStringChooser(A_RandomSpecificGroup);

        SetupAction(this, "Set Next Camera Position", OnSetCoordsActionNext);

        SetupAction(this, "Set Next Camera Position Loop", OnSetCoordsActionNextLoop);

    }

    // Create input action trigger
    public static JSONStorableAction SetupAction(MVRScript script, string name, JSONStorableAction.ActionCallback callback)
    {
        JSONStorableAction action = new JSONStorableAction(name, callback);
        script.RegisterAction(action);
        return action;
    }

    /*
        // Got this from AcidBubbles code, kudos.
        private IEnumerator InitDeferred()
        {
            yield return new WaitForEndOfFrame();
            if (!enabled) yield break;
            if (SetCameraPositionOnEnable.val)
                SetCoords();
            yield return 0;
            if (!enabled) yield break;
            if (SetCameraPositionOnEnable.val)
                SetCoords();
        }
    */

    // We need to override this function, to save a custom array in the savegame file
    public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
    {
        JSONClass jsonObject = base.GetJSON(includePhysical, includeAppearance, forceStore);

        // Store MonitorCoordinatesStringList
        JSONArray monitorCoordsArray = new JSONArray();
        foreach (MonitorCoordinates mc in MonitorCoordinatesList)
        {
            monitorCoordsArray.Add(mc.MonitorCoordsToString());
        }

        jsonObject["monitorCoordinatesList"] = monitorCoordsArray;

        return jsonObject;
    }

    // We also need to override this to restore our custom array from the savegame file
    public override void LateRestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, bool setMissingToDefault = true)
    {
        base.LateRestoreFromJSON(jc, restorePhysical, restoreAppearance, setMissingToDefault);

        // if this object is found in the save file, then it's a save file from version < 4 and requires a different restore method
        if (jc["MonitorPositionCameraTitles"] != null)
        {
            OldVersionLateRestoreFromJSON(jc, restorePhysical, restoreAppearance, setMissingToDefault);
        }
        else
        {
            JSONArray coordsArray = jc["monitorCoordinatesList"].AsArray;

            if (coordsArray.Count > 0)
            {
                MonitorCoordinatesList.Clear();

                // Fill our string list with the saved coordinates
                foreach (JSONNode node in coordsArray)
                {
                    string coordsAsString = node.Value;
                    MonitorCoordinates loadedMonitorCoords = CreateMonitorCoordinatesFromString(coordsAsString, "");
                    MonitorCoordinatesList.Add(loadedMonitorCoords);

                    // Recreate Group List
                    if (!GroupList.Contains(loadedMonitorCoords.GroupId))
                    {
                        GroupList.Add(loadedMonitorCoords.GroupId);
                    }
                }

                // SuperController.LogMessage("GroupList.Count: " + GroupList.Count);

                // Making sure group list contains at least 1 group
                if (GroupList.Count == 0)
                {
                    GroupList.Add("0");
                }

                MonitorPositionChooser.valNoCallback = "";
                MonitorPositionChooser.choices = null; // force UI sync
                MonitorPositionChooser.choices = GetCameraTitlesStringList(SelectedGroupId, MonitorCoordinatesList);
                GroupChooser.choices = null; // force UI sync
                GroupChooser.choices = GroupList;

                // default to first items for list
                // GroupChooser.val = "0";
                MonitorPositionChooser.val = MonitorCoordinatesList[0].MonitorCameraTitle;
                UpdateTextFields("0", MonitorCoordinatesList[0].MonitorCameraTitle);
            }
        }
    }

    // Keep this for compatibiliy with save fields < version 4 of the plugin
    public void OldVersionLateRestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, bool setMissingToDefault = true)
    {
        //SuperController.LogMessage("old version restore started.");

        if (GroupList.Count == 0)
        {
            GroupList.Add("0");
            SelectedGroupId = "0";
            GroupChooser.val = "0";
        }

        JSONArray coordsArray = jc["monitorCoordinatesStringList"].AsArray;
        JSONArray cameraArray = jc["MonitorPositionCameraTitles"].AsArray;

        if (coordsArray.Count > 0)
        {
            MonitorCoordinatesList.Clear();
            int i = 0;
            // Fill our string list with the saved coordinates
            foreach (JSONNode node in coordsArray)
            {
                string coords = node.Value;
                MonitorCoordinatesList.Add(new MonitorCoordinates("0", cameraArray[i], coords));
                //SuperController.LogMessage("adding camera coords: " + cameraArray[i]);
                i++;
            }

            //SuperController.LogMessage("setting choices");

            MonitorPositionChooser.valNoCallback = "";
            MonitorPositionChooser.choices = null; // force UI sync
            MonitorPositionChooser.choices = GetCameraTitlesStringList("0", MonitorCoordinatesList);
            GroupChooser.choices = GroupList;

            //SuperController.LogMessage("setting defaults");

            // default to first items for list
            GroupChooser.val = "0";
            MonitorPositionChooser.val = MonitorCoordinatesList[0].MonitorCameraTitle;
            UpdateTextFields("0", MonitorCoordinatesList[0].MonitorCameraTitle);
        }
    }

    protected void AddOrUpdateCameraList(string groupId, string cameraTitle, string coordsAsString)
    {
        bool titleFoundInList = false;
        for (int i = 0; i < MonitorCoordinatesList.Count; i++)
        {
            MonitorCoordinates currentMonitorCoordinates = MonitorCoordinatesList[i];

            if (currentMonitorCoordinates.GroupId == groupId && currentMonitorCoordinates.MonitorCameraTitle == cameraTitle)
            {
                // title already exists, so we update the value
                currentMonitorCoordinates.CoordinatesAsString = coordsAsString;
                titleFoundInList = true;
                break;
            }
        }

        if (!titleFoundInList)
        {
            MonitorCoordinatesList.Add(new MonitorCoordinates(groupId, cameraTitle, coordsAsString));
        }
    }

    // This happens when the button "add coords" is pressed
    protected void OnAddNewCoords()
    {
        string cameraTitle = CameraTitleInputFieldUI.text;

        var sc = SuperController.singleton;

        // get camera position
        var centerCameraPosition = sc.centerCameraTarget.transform.position;

        // get camera rotation
        var monitorCenterCameraRotation = sc.MonitorCenterCamera.transform;

        // Create Coordinates object
        MonitorCoordinates tmpCoords = new MonitorCoordinates(SelectedGroupId, cameraTitle, centerCameraPosition, monitorCenterCameraRotation.eulerAngles);

        // We add the coordinates to a string list, so at each position in the list (ID), we have a set of coordinates
        // but check if we need to update instead of adding a value
        AddOrUpdateCameraList(SelectedGroupId, cameraTitle, tmpCoords.MonitorCoordsToString());

        RefreshChoosers(cameraTitle);

        // Now that we've added the camera title to the selector, let's suggest the next title
        string nextCameraName = SuggestNextCameraName(cameraTitle);
        CameraTitleInputFieldUI.text = nextCameraName;
        CameraTextUI.val = nextCameraName;

        UpdateTextFields(SelectedGroupId, cameraTitle);
    }

    private void DeleteCoords()
    {
        // get current group
        string currentSelectedGroup = SelectedGroupId;

        // get current camera
        string currentSelectedCamera = SelectedMonitorChooserTitle;

        // get index of current camera and coords
        int currentCameraIndex = -1;
        for (int i = 0; i < MonitorCoordinatesList.Count; i++)
        {
            if (MonitorCoordinatesList[i].GroupId == currentSelectedGroup && MonitorCoordinatesList[i].MonitorCameraTitle == currentSelectedCamera)
            {
                currentCameraIndex = i;
                break;
            }
        }

        if (currentCameraIndex != -1)
        {
            MonitorCoordinatesList.RemoveAt(currentCameraIndex);
        }
        else
        {
            SuperController.LogError($"Tried to remove non-existing camera coordinates from list. You can safely ignore this error.");
        }

        // get next possible previous camera name to set after deleting a camera
        string previousCameraInList = "";
        string matchingGroupId;
        bool updateSelectorAndTextfields = true;
        if (currentCameraIndex - 1 > 0)
        {
            previousCameraInList = MonitorCoordinatesList[currentCameraIndex - 1].MonitorCameraTitle;
            matchingGroupId = MonitorCoordinatesList[currentCameraIndex - 1].GroupId;
        }
        else if (MonitorCoordinatesList.Count > 0)
        {
            previousCameraInList = MonitorCoordinatesList[0].MonitorCameraTitle;
            matchingGroupId = MonitorCoordinatesList[0].GroupId;
        }
        else
        {
            updateSelectorAndTextfields = false;
        }

        if (updateSelectorAndTextfields)
        {
            // refresh 
            RefreshChoosers(previousCameraInList);

            // update fields
            UpdateTextFields(SelectedGroupId, previousCameraInList);
        }
    }

    // Read the coordinates from the UI text field and set the camera to that position
    protected void SetCoords()
    {
        string coordsAsString = CoordsTextInputFieldUI.text;
        CameraVectors cameraVectors = CreateCameraVectorsFromCoordinatesString(coordsAsString);
        SetCoords(cameraVectors.MonitorPosition, cameraVectors.MonitorRotation);
    }

    // Lookup the coordinates from the list (by title) and set the camera to that position
    protected void OnSetCoordsAction(string cameraTitle)
    {
        // get string from list by id
        string coordsString = "";
        for (int i = 0; i < MonitorCoordinatesList.Count; i++)
        {
            if (MonitorCoordinatesList[i].GroupId == SelectedGroupId && MonitorCoordinatesList[i].MonitorCameraTitle == cameraTitle)
            {
                coordsString = MonitorCoordinatesList[i].CoordinatesAsString;
                break;
            }
        }

        string[] coordsStringArray = coordsString.Split('_');

        // sometimes this array has only length 1 (empty string), can't figure out why, it works anyway, no need to bother the user about it.
        if (coordsStringArray.Length > 1)
        {

            if (coordsStringArray.Length == 8)
            {
                try
                {
                    Vector3 newCenterCameraPosition = new Vector3(float.Parse(coordsStringArray[2]), float.Parse(coordsStringArray[3]), float.Parse(coordsStringArray[4]));
                    Vector3 newMonitorCenterCameraRotation = new Vector3(float.Parse(coordsStringArray[5]), float.Parse(coordsStringArray[6]), float.Parse(coordsStringArray[7]));

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

        SelectedMonitorChooserTitle = cameraTitle;
    }

    // Here we get the coordinates via parameters set the camera to that position
    protected void SetCoords(Vector3 newCenterCameraPosition, Vector3 newMonitorCenterCameraRotation)
    {
        // //SuperController.LogMessage($"SetCoords called");

        var sc = SuperController.singleton;

        // This part is copied from the SpawnPoint script from AcidBubbles -> Kudos man!
        // I would have NEVER figured out how to calculate this stuff
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
    }

    protected void OnSetCoordsActionRandomAnyGroup()
    {
        string coordsAsString;

        // get a random camera from the list
        int randomListIndex = UnityEngine.Random.Range(0, MonitorCoordinatesList.Count);
        {
            coordsAsString = MonitorCoordinatesList[randomListIndex].CoordinatesAsString;
        }

        MonitorCoordinates tmpMonitorCoordinates = CreateMonitorCoordinatesFromString(coordsAsString, "");

        SetCoords(tmpMonitorCoordinates.MonitorPosition, tmpMonitorCoordinates.MonitorRotation);
        SelectedMonitorChooserTitle = MonitorCoordinatesList[randomListIndex].MonitorCameraTitle;
    }

    protected void OnSetCoordsActionRandomSpecificGroup(string groupId)
    {
        string coordsAsString = "";

        // count max random number
        int maxRandomNumber = 0;

        for (int i = 0; i < MonitorCoordinatesList.Count; i++)
        {
            if (MonitorCoordinatesList[i].GroupId == groupId)
            {
                maxRandomNumber++;
            }
        }

        // get a random camera from the list within the group
        int randomListIndex = UnityEngine.Random.Range(0, maxRandomNumber);
        int counter = 0;
        for (int i = 0; i < MonitorCoordinatesList.Count; i++)
        {
            if (MonitorCoordinatesList[i].GroupId == groupId)
            {
                if (counter == randomListIndex)
                {
                    coordsAsString = MonitorCoordinatesList[randomListIndex].CoordinatesAsString;
                    break;
                }
                counter++;
            }
        }

        MonitorCoordinates tmpMonitorCoordinates = CreateMonitorCoordinatesFromString(coordsAsString, "");

        SetCoords(tmpMonitorCoordinates.MonitorPosition, tmpMonitorCoordinates.MonitorRotation);
        SelectedMonitorChooserTitle = MonitorCoordinatesList[randomListIndex].MonitorCameraTitle;
    }

    // Lookup the coordinates from the list (by title) and get the next list item. Stops at the last item in the list.
    protected void OnSetCoordsActionNext()
    {
        // get current selected camera
        string cameraTitle = SelectedMonitorChooserTitle;

        if (!string.IsNullOrEmpty(cameraTitle))
        {
            // get string from list by id
            string coordsAsString = "";
            for (int i = 0; i < MonitorCoordinatesList.Count; i++)
            {
                if (MonitorCoordinatesList[i].MonitorCameraTitle == cameraTitle)
                {
                    if (i < MonitorCoordinatesList.Count)
                    {
                        // Get the next camera
                        i++;
                    }
                    else
                    {
                        // if this is the last camera in the list, give back last item
                    }

                    coordsAsString = MonitorCoordinatesList[i].CoordinatesAsString;
                    SelectedGroupId = MonitorCoordinatesList[i].GroupId;
                    SelectedMonitorChooserTitle = MonitorCoordinatesList[i].MonitorCameraTitle;
                    break;
                }
            }

            MonitorCoordinates tmpMonitorCoordinates = CreateMonitorCoordinatesFromString(coordsAsString, "");
            SetCoords(tmpMonitorCoordinates.MonitorPosition, tmpMonitorCoordinates.MonitorRotation);
        }
        else
        {
            //SuperController.LogMessage($"MonitorPositionChooser.val was empty or null.");
        }
    }

    // Lookup the coordinates from the list (by title) and get the next list item. Loop from beginning if end is reached
    protected void OnSetCoordsActionNextLoop()
    {
        // get current selected camera
        string cameraTitle = SelectedMonitorChooserTitle;

        if (!string.IsNullOrEmpty(cameraTitle))
        {
            // get string from list by id
            string coordsAsString = "";
            for (int i = 0; i < MonitorCoordinatesList.Count; i++)
            {
                if (MonitorCoordinatesList[i].MonitorCameraTitle == cameraTitle)
                {
                    if (i < MonitorCoordinatesList.Count)
                    {
                        // Get the next camera
                        i++;
                    }
                    else
                    {
                        // if this is the last camera in the list, give back the last camera
                        i = 0;
                    }

                    coordsAsString = MonitorCoordinatesList[i].CoordinatesAsString;
                    SelectedGroupId = MonitorCoordinatesList[i].GroupId;
                    SelectedMonitorChooserTitle = MonitorCoordinatesList[i].MonitorCameraTitle;
                    break;
                }
            }

            MonitorCoordinates tmpMonitorCoordinates = CreateMonitorCoordinatesFromString(coordsAsString, "");
            SetCoords(tmpMonitorCoordinates.MonitorPosition, tmpMonitorCoordinates.MonitorRotation);
        }
        else
        {
            //SuperController.LogMessage($"MonitorPositionChooser.val was empty or null.");
        }
    }

    public void UpdateTextFields(string groupId, string cameraTitle)
    {
        bool requestedCameraFound = false;
        // check if the titles list contains the cameraTitle, if so, update the coord textfield
        for (int i = 0; i < MonitorCoordinatesList.Count; i++)
        {
            if (MonitorCoordinatesList[i].GroupId == groupId && MonitorCoordinatesList[i].MonitorCameraTitle == cameraTitle)
            {
                CoordsTextInputFieldUI.text = MonitorCoordinatesList[i].CoordinatesAsString;
                CoordsTextUI.val = CoordsTextInputFieldUI.text;
                requestedCameraFound = true;
                break;
            }
        }

        if (!requestedCameraFound)
        {
            // Select the first camera from the current group
            foreach (MonitorCoordinates mc in MonitorCoordinatesList)
            {
                if (mc.GroupId == groupId)
                {
                    CoordsTextInputFieldUI.text = mc.CoordinatesAsString;
                    CoordsTextUI.val = mc.CoordinatesAsString;
                    cameraTitle = mc.MonitorCameraTitle;
                }
            }
        }

        SelectedGroupId = groupId;
        SelectedMonitorChooserTitle = cameraTitle;
    }

    protected void RefreshChoosers(string cameraTitle)
    {
        // Update group chooser
        GroupChooser.choices = null; // force UI sync
        GroupChooser.choices = GroupList;

        //SuperController.LogMessage("GroupList count after refresh: " + GroupList.Count);

        // Update camera title chooser
        MonitorPositionChooser.valNoCallback = "";
        MonitorPositionChooser.choices = null; // force UI sync
        MonitorPositionChooser.choices = GetCameraTitlesStringList(SelectedGroupId, MonitorCoordinatesList);

        if (!string.IsNullOrEmpty(cameraTitle))
        {
            MonitorPositionChooser.val = cameraTitle;
        }
    }

    private void OnChangeSelectedGroupChoice(string groupId)
    {
        SelectedGroupId = groupId;
        RefreshChoosers("");
        UpdateTextFields(groupId, SelectedMonitorChooserTitle);
    }

    protected void OnChangeSelectedCameraTitle(string cameraTitle)
    {
        // here we get the selected camera title and want to update the text field
        UpdateTextFields(SelectedGroupId, cameraTitle);
    }

    private void OnCameraTitleTextChanged()
    {
        CoordsTextUI.val = CameraTitleInputFieldUI.text;
    }

    protected void OnCoordsTextChanged()
    {
        CoordsTextUI.val = CoordsTextInputFieldUI.text;
    }

    protected string SuggestNextCameraName(string cameraTitle)
    {
        // Match the last number in the string
        Match match = Regex.Match(cameraTitle, @"\d+$");

        if (match.Success)
        {
            string lastNumber = match.Value;
            int lastNumberInt = Int32.Parse(lastNumber);
            lastNumberInt++;
            return cameraTitle.Replace(lastNumber, lastNumberInt.ToString());
        }
        else
        {
            return cameraTitle + "1";
        }
    }

    protected void CreateCoordsUIelements()
    {
        CoordsTextUI = new JSONStorableString("CoordsTextUI", "_default_") { isStorable = false, isRestorable = false };
        CameraTextUI = new JSONStorableString("CameraTextUI", "_default_") { isStorable = false, isRestorable = false };

        // Temporary vars
        UIDynamicTextField tmpTextfield;
        UIDynamicTextField tmp2Textfield;

        // Creating components

        // ******* CAMERA CHOOSER  ***********
        MonitorPositionChooser = new JSONStorableStringChooser("Camera ID", GetCameraTitlesStringList(SelectedGroupId, MonitorCoordinatesList), "", "Camera ID")
        {
            isRestorable = true,
            isStorable = true,
            storeType = JSONStorableParam.StoreType.Full
        };
        RegisterStringChooser(MonitorPositionChooser);
        MonitorPositionChooser.setCallbackFunction += (val) =>
        {
            OnChangeSelectedCameraTitle(val);
        };
        UIDynamicPopup DSelsp = CreateScrollablePopup(MonitorPositionChooser, true);
        DSelsp.labelWidth = 250f;
        globalControlsUIs.Add((UIDynamic)DSelsp);

        // ******* BUTTONS FOR CAMERA CHOOSER ***********
        // add button
        UIDynamicButton addCoordsBtn = CreateButton("Add camera", true);
        addCoordsBtn.button.onClick.AddListener(() => { OnAddNewCoords(); });
        globalControlsUIs.Add((UIDynamic)addCoordsBtn);

        // test button
        UIDynamicButton setCoordsBtn = CreateButton("Test camera", true);
        setCoordsBtn.button.onClick.AddListener(() => { SetCoords(); });
        globalControlsUIs.Add((UIDynamic)setCoordsBtn);

        // delete button
        UIDynamicButton deleteCoordsBtn = CreateButton("Delete camera", true);
        deleteCoordsBtn.button.onClick.AddListener(() => { DeleteCoords(); });
        globalControlsUIs.Add((UIDynamic)deleteCoordsBtn);

        /*

        // DEBUG button
        UIDynamicButton debugCoordsBtn = CreateButton("DEBUG", true);
        debugCoordsBtn.button.onClick.AddListener(() => { DebugButton(); });
        globalControlsUIs.Add((UIDynamic)debugCoordsBtn);

        // DEBUG button
        UIDynamicButton debugGroupList = CreateButton("Recreate GroupList", true);
        debugGroupList.button.onClick.AddListener(() => { ReCreateGroupListDEBUG(); });
        globalControlsUIs.Add((UIDynamic)debugGroupList);

        */

        // ******* GROUP CHOOSER  ***********
        GroupChooser = new JSONStorableStringChooser("Group ID", GroupList, "", "Group ID")
        {
            isRestorable = true,
            isStorable = true,
            storeType = JSONStorableParam.StoreType.Full
        };
        RegisterStringChooser(GroupChooser);
        GroupChooser.setCallbackFunction += (val) => { OnChangeSelectedGroupChoice(val); };
        UIDynamicPopup DSelsp1 = CreateScrollablePopup(GroupChooser, true);
        DSelsp1.labelWidth = 250f;
        globalControlsUIs.Add((UIDynamic)DSelsp1);

        // ******* BUTTONS FOR CAMERA CHOOSER ***********
        // add button
        UIDynamicButton addGroupBtn = CreateButton("Add group", true);
        addGroupBtn.button.onClick.AddListener(() => { OnAddNewGroup(); });
        globalControlsUIs.Add((UIDynamic)addGroupBtn);

        // delete button
        UIDynamicButton deleteGroup = CreateButton("Delete group", true);
        deleteGroup.button.onClick.AddListener(() => { DeleteGroup(); });
        globalControlsUIs.Add((UIDynamic)deleteGroup);

        // ******* SECTION TITLE ***********
        UICameraSectionTitle = CreateStaticDescriptionText("UICameraSectionTitle", "<color=#000><size=35><b>Next camera name</b></size></color>", false, 55, TextAnchor.MiddleLeft);
        globalControlsUIs.Add((UIDynamic)UICameraSectionTitle);

        // ******* CAMERA TITLE TEXTFIELD ***********
        cameraTitle = new JSONStorableString("CameraTitle", "0");
        tmp2Textfield = CreateTextField(cameraTitle);
        SetupTextField(tmp2Textfield, 50f, false, false);
        CameraTitleInputFieldUI = tmp2Textfield.UItext.gameObject.AddComponent<InputField>();
        CameraTitleInputFieldUI.textComponent = tmp2Textfield.UItext;
        CameraTitleInputFieldUI.lineType = InputField.LineType.SingleLine;
        globalControlsUIs.Add((UIDynamic)tmp2Textfield);
        CoordsTextUI.valNoCallback = "0";
        CameraTitleInputFieldUI.text = "0";
        CameraTitleInputFieldUI.onValueChanged.AddListener(delegate { OnCameraTitleTextChanged(); });

        // ******* SECTION TITLE ***********
        UICoordsSectionTitle = CreateStaticDescriptionText("UICoordsSectionTitle", "<color=#000><size=35><b>Camera Coordinates</b></size></color>", false, 55, TextAnchor.MiddleLeft);
        globalControlsUIs.Add((UIDynamic)UICoordsSectionTitle);

        // ******* COORDS TEXTFIELD  ***********
        string newDefaultText = "Treat this as a read-only field, don't type in it.";
        CoordsTextUI = new JSONStorableString("CoordsTextUI", "_default_");
        tmpTextfield = CreateTextField(CoordsTextUI);
        SetupTextField(tmpTextfield, 100f, false, false);
        CoordsTextInputFieldUI = tmpTextfield.UItext.gameObject.AddComponent<InputField>();
        CoordsTextInputFieldUI.textComponent = tmpTextfield.UItext;
        CoordsTextInputFieldUI.lineType = InputField.LineType.MultiLineNewline;
        globalControlsUIs.Add((UIDynamic)tmpTextfield);
        CoordsTextUI.valNoCallback = newDefaultText;
        CoordsTextInputFieldUI.text = newDefaultText;
        CoordsTextInputFieldUI.onValueChanged.AddListener(delegate { OnCoordsTextChanged(); });

        HelpText = new JSONStorableString("Help",
                    "WTF is this?\n" +
                    "-----------------\n\n" +
                    "Ok, the idea of this plugin is that you can move your monitor camera to a certain place, store the coordinates of your screen camera in a list and then later on set your camera back to the saved coordinates.\n\n" +
                    "This gets cool, if you store many camera locations and you are using another plugin (like VAMStory) to tell a story and want the user to see the exact camera angles that you had planned.\n\n" +
                    "So it's mainly great to use with another plugin, by calling an action called 'Set Camera Position' .\n\n" +
                    "You can give the camera coordinates any kind of name, like Cam1. It helps to have a number at the end, the plugin will increment the number for you after adding another camera coordinates.\n\n"
                );
        UIDynamicTextField helpWindow = CreateTextField(HelpText, true);
        helpWindow.height = 850.0f;
        globalControlsUIs.Add((UIDynamic)helpWindow);
    }

    private void DebugButton()
    {
        SuperController.LogMessage("-------------------------");
        foreach (MonitorCoordinates mc in MonitorCoordinatesList)
        {
            SuperController.LogMessage(mc.CoordinatesAsString);
        }
        SuperController.LogMessage("-------------------------");
    }

    private void DeleteGroup()
    {
        // if it is last group, don't delete it
        if (GroupList.Count == 1)
        {
            SuperController.LogMessage("You cannot delete the last remaining group.");
        }
        else
        {
            // get current selected group and the group to change
            string deletedGroupId = SelectedGroupId;
            int deletedGroupIdInt = Int32.Parse(deletedGroupId);

            // delete all cameras with that group id
            List<MonitorCoordinates> toDeleteList = new List<MonitorCoordinates>();

            foreach (MonitorCoordinates mc in MonitorCoordinatesList)
            {
                if (mc.GroupId == SelectedGroupId)
                {
                    toDeleteList.Add(mc);
                }
            }

            foreach (MonitorCoordinates mcDel in toDeleteList)
            {
                MonitorCoordinatesList.Remove(mcDel);
            }

            // remove group from grouplist
            GroupList.Remove(deletedGroupId);

            // shift all cameras, that have a higher higher groupId than the deleted group to a new group
            foreach (MonitorCoordinates mc in MonitorCoordinatesList)
            {
                //SuperController.LogMessage("Del Group ID: '" +deletedGroupId+"' Current Group ID: '"+mc.GroupId+"'");
                if (Int32.Parse(mc.GroupId) > deletedGroupIdInt)
                {
                    mc.GroupId = Math.Max(Int32.Parse(mc.GroupId) - 1, 0).ToString();
                    // SuperController.LogMessage("Changed Group ID to: " + mc.GroupId);
                }
                else
                {
                    // SuperController.LogMessage("No change.");
                }
            }

            // Recreate Group List to solve issues when GroupID's aren't in the natural sequence
            GroupList = ReCreateGroupList();

            // RefreshChoosers
            RefreshChoosers("");
        }
    }

    private void ReCreateGroupListDEBUG()
    {
        GroupList = ReCreateGroupList();

        // Update group chooser
        GroupChooser.choices = null; // force UI sync
        GroupChooser.choices = GroupList;
        GroupChooser.valNoCallback = GroupList[0];
    }

    private List<string> ReCreateGroupList()
    {
        List<string> newGroupList = new List<string>();

        // find the highest group ID in the MonitorList
        int highestnumber = 0;
        foreach (MonitorCoordinates mc in MonitorCoordinatesList)
        {
            int groupIdInt = Int32.Parse(mc.GroupId);

            if (groupIdInt > highestnumber)
            {
                highestnumber = groupIdInt;
            }
        }

        // doing it this way, we're sure to have the correct sequence of group ID's
        for (int i = 0; i <= highestnumber; i++)
        {
            newGroupList.Add(i.ToString());
        }

        return newGroupList;
    }

    private void OnAddNewGroup()
    {
        // add new group
        string nextGroupName = GroupList.Count.ToString();
        GroupList.Add(nextGroupName);

        // select group
        SelectedGroupId = nextGroupName;
        GroupChooser.val = nextGroupName;

        // unselect camera title
        SelectedMonitorChooserTitle = "";
        MonitorPositionChooser.val = "";

        // set fields to empty
        CoordsTextInputFieldUI.text = "";
        CoordsTextUI.val = "";

        // update chooser field to only show items that belong to the new group (which should be no items)
        RefreshChoosers("");
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
        JSONStorableString staticDescString = new JSONStorableString(DescTitle, DescText)
        {
            isStorable = false,
            isRestorable = false,
            hidden = true
        };
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
        if (ContainingAtom == null) return;
        if (ContainingAtom.IsBoolJSONParam("IsPositionerHost")) return;
        IsPositionerHost = new JSONStorableBool("IsPositionerHost", true);
        ContainingAtom.RegisterBool(IsPositionerHost);
    }

    private void OnDisable()
    {
        if (IsPositionerHost == null) return;
        ContainingAtom.DeregisterBool(IsPositionerHost);
        IsPositionerHost = null;
    }

    public void OnDestroy()
    {
        SuperController.singleton.BroadcastMessage("OnActionsProviderDestroyed", this, SendMessageOptions.DontRequireReceiver);
    }

    protected class CameraVectors
    {
        public Vector3 MonitorPosition;
        public Vector3 MonitorRotation;
    }

    public class MonitorCoordinates
    {
        public string GroupId;
        public string MonitorCameraTitle;
        public Vector3 MonitorPosition;
        public Vector3 MonitorRotation;
        private string _coordinatesAsString;
        public string CoordinatesAsString
        {
            get
            {
                // this makes sure, we return the latest value
                return MonitorCoordsToString();
            }

            set
            {
                _coordinatesAsString = value;
                MonitorCoordinates tmpMonitorCoordinates = CreateMonitorCoordinatesFromString(_coordinatesAsString, "");
                MonitorPosition = tmpMonitorCoordinates.MonitorPosition;
                MonitorRotation = tmpMonitorCoordinates.MonitorRotation;
            }
        }

        public MonitorCoordinates(string groupId, string monitorCameraTitle, string coordsAsString)
        {
            GroupId = groupId;
            MonitorCameraTitle = monitorCameraTitle;
            CoordinatesAsString = coordsAsString;
        }

        public MonitorCoordinates(string groupId, string monitorCameraTitle, Vector3 monitorPosition, Vector3 monitorRotation)
        {
            MonitorPosition = monitorPosition;
            MonitorRotation = monitorRotation;
            GroupId = groupId;
            MonitorCameraTitle = monitorCameraTitle;
            _coordinatesAsString = MonitorCoordsToString();
        }

        // This is the string representation of the monitor coordinates, that we can parse again later
        public string MonitorCoordsToString()
        {
            string coordsAsString = GroupId + "_" + MonitorCameraTitle + "_" + MonitorPosition.x + "_" + MonitorPosition.y + "_" + MonitorPosition.z + "_" + MonitorRotation.x + "_" + MonitorRotation.y + "_" + MonitorRotation.z;
            return coordsAsString;
        }
    }
    public List<string> GetMonitorCoordinatesStringList(List<MonitorCoordinates> monitorCoordinatesList)
    {
        List<string> stringList = new List<string>();

        foreach (MonitorCoordinates mc in monitorCoordinatesList)
        {
            stringList.Add(mc.MonitorCoordsToString());
        }

        return stringList;
    }

    public List<string> GetCameraTitlesStringList(string groupId, List<MonitorCoordinates> monitorCoordinatesList)
    {
        List<string> stringList = new List<string>();

        foreach (MonitorCoordinates mc in monitorCoordinatesList)
        {
            if (mc.GroupId == groupId)
            {
                stringList.Add(mc.MonitorCameraTitle);
            }
        }

        return stringList;
    }

    private static CameraVectors CreateCameraVectorsFromCoordinatesString(string coordsAsString)
    {

        CameraVectors newCV = new CameraVectors();

        if (!string.IsNullOrEmpty(coordsAsString))
        {
            var sc = SuperController.singleton;

            string[] coordsStringArray = coordsAsString.Split('_');

            if (coordsStringArray.Length == 8)
            {
                try
                {
                    newCV.MonitorPosition = new Vector3(float.Parse(coordsStringArray[2]), float.Parse(coordsStringArray[3]), float.Parse(coordsStringArray[4]));
                    newCV.MonitorRotation = new Vector3(float.Parse(coordsStringArray[5]), float.Parse(coordsStringArray[6]), float.Parse(coordsStringArray[7]));
                }
                catch (Exception e)
                {
                    SuperController.LogError($"Could not parse coordinates from the text field.");
                    SuperController.LogError(e.Message);
                }
            }
            // This is for restoring camera positions from save files with version < 4.
            else if (coordsStringArray.Length == 6)
            {
                try
                {
                    newCV.MonitorPosition = new Vector3(float.Parse(coordsStringArray[0]), float.Parse(coordsStringArray[1]), float.Parse(coordsStringArray[2]));
                    newCV.MonitorRotation = new Vector3(float.Parse(coordsStringArray[3]), float.Parse(coordsStringArray[4]), float.Parse(coordsStringArray[5]));
                }
                catch (Exception e)
                {
                    SuperController.LogError($"Could not parse coordinates from the text field.");
                    SuperController.LogError(e.Message);
                }
            }
            else
            {
                SuperController.LogError($"Could not parse coordinates from the text field, need exactly 8 fields: groupId, cameraTitle and 6 coordinates (position x,y,z and rotation x,y,z).");
            }
        }

        return newCV;
    }

    private static MonitorCoordinates CreateMonitorCoordinatesFromString(string coordsAsString, string cameraTitleForOldSaveFileVersion)
    {
        if (!string.IsNullOrEmpty(coordsAsString))
        {
            string groupId = "0";
            string cameraTitle = "";
            Vector3 monitorPosition = new Vector3();
            Vector3 monitorRotation = new Vector3();

            string[] coordsStringArray = coordsAsString.Split('_');

            MonitorCoordinates newMonitorCoords = null;

            if (coordsStringArray.Length == 8)
            {
                try
                {
                    groupId = coordsStringArray[0].ToString();
                    cameraTitle = coordsStringArray[1].ToString();
                    monitorPosition = new Vector3(float.Parse(coordsStringArray[2]), float.Parse(coordsStringArray[3]), float.Parse(coordsStringArray[4]));
                    monitorRotation = new Vector3(float.Parse(coordsStringArray[5]), float.Parse(coordsStringArray[6]), float.Parse(coordsStringArray[7]));

                    newMonitorCoords = new MonitorCoordinates(groupId, cameraTitle, monitorPosition, monitorRotation);
                }
                catch (Exception e)
                {
                    SuperController.LogError($"Could not parse coordinates from the text field.");
                    SuperController.LogError(e.Message);
                }
            }
            // This is for restoring camera positions from save files with version < 4.
            else if (coordsStringArray.Length == 6)
            {
                try
                {
                    groupId = "0";
                    cameraTitle = cameraTitleForOldSaveFileVersion;
                    monitorPosition = new Vector3(float.Parse(coordsStringArray[0]), float.Parse(coordsStringArray[1]), float.Parse(coordsStringArray[2]));
                    monitorRotation = new Vector3(float.Parse(coordsStringArray[3]), float.Parse(coordsStringArray[4]), float.Parse(coordsStringArray[5]));

                    newMonitorCoords = new MonitorCoordinates(groupId, cameraTitle, monitorPosition, monitorRotation);
                }
                catch (Exception e)
                {
                    SuperController.LogError($"Could not parse coordinates from the text field.");
                    SuperController.LogError(e.Message);
                }
            }
            else
            {
                SuperController.LogError($"Could not parse coordinates from the text field, need exactly 8 fields: groupId, cameraTitle and 6 coordinates (position x,y,z and rotation x,y,z).");
            }

            return newMonitorCoords;
        }
        else
        {
            SuperController.LogError($"Got an empty string coordinates to parse, returning null.");
            return null;
        }
    }
}