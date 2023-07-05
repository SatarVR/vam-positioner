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
    protected JSONStorableBool ApplyToAtomUI;
    protected List<string> GroupList = new List<string>();
    protected List<string> FlatGroupAndPositionCoordinatesStringList = new List<string>();
    protected List<PositionCoordinates> PositionCoordinatesList = new List<PositionCoordinates>();
    protected JSONStorableStringChooser PositionChooser;
    protected JSONStorableStringChooser GroupChooser;
    protected List<UIDynamic> globalControlsUIs = new List<UIDynamic>();
    protected JSONStorableString CoordsTextUI;
    protected JSONStorableString PositionTitleUI;
    protected JSONStorableString HelpText;
    protected InputField CoordsTextInputFieldUI;
    protected InputField PositionTitleInputFieldUI;
    protected JSONStorableString positionTitle;
    protected UIDynamicTextField UICoordsSectionTitle;
    protected UIDynamicTextField UIPositionSectionTitle;
    protected string SelectedPositionChooserTitle = "";
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

        // Initialize CameraGroupList with one item
        if (FlatGroupAndPositionCoordinatesStringList.Count == 0)
        {
            FlatGroupAndPositionCoordinatesStringList.Add("0");
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
        JSONStorableAction fakeFuncUseBelow = new JSONStorableAction("- - - - Use these actions below ↓ - - - - -", () => { });
        RegisterAction(fakeFuncUseBelow);

        // Add action to show the selected camera
        JSONStorableStringChooser A_SetPositionCoords = new JSONStorableStringChooser("Set Position", FlatGroupAndPositionCoordinatesStringList, "", "Set Position")
        { isStorable = false, isRestorable = false };
        A_SetPositionCoords.setCallbackFunction += (val) => { OnSetCoordsAction(val); };
        RegisterStringChooser(A_SetPositionCoords);

        SetupAction(this, "Set Random Position (any group)", OnSetCoordsActionRandomAnyGroup);

        JSONStorableStringChooser A_RandomSpecificGroup = new JSONStorableStringChooser("Set Position (specific group)", GroupList, "", "Set Random Position (specific group)")
        { isStorable = false, isRestorable = false };
        A_RandomSpecificGroup.setCallbackFunction += (val) => { OnSetCoordsActionRandomSpecificGroup(val); };
        RegisterStringChooser(A_RandomSpecificGroup);

        SetupAction(this, "Set Next Position (until end)", OnSetCoordsActionNext);

        SetupAction(this, "Set Next Position (Loop)", OnSetCoordsActionNextLoop);

    }

    // Create input action trigger
    public static JSONStorableAction SetupAction(MVRScript script, string name, JSONStorableAction.ActionCallback callback)
    {
        JSONStorableAction action = new JSONStorableAction(name, callback);
        script.RegisterAction(action);
        return action;
    }

    // We need to override this function, to save a custom array in the savegame file
    public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
    {
        JSONClass jsonObject = base.GetJSON(includePhysical, includeAppearance, forceStore);

        // Store MonitorCoordinatesStringList
        JSONArray positionCoordsArray = new JSONArray();
        foreach (PositionCoordinates mc in PositionCoordinatesList)
        {
            positionCoordsArray.Add(mc.PositionCoordsToString());
        }

        jsonObject["monitorCoordinatesList"] = positionCoordsArray;

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
                PositionCoordinatesList.Clear();

                // Fill our string list with the saved coordinates
                foreach (JSONNode node in coordsArray)
                {
                    string coordsAsString = node.Value;
                    PositionCoordinates loadedPositionCoords = CreatePositionCoordinatesFromString(coordsAsString, "");
                    PositionCoordinatesList.Add(loadedPositionCoords);

                    // Recreate Group List
                    if (!GroupList.Contains(loadedPositionCoords.GroupId))
                    {
                        GroupList.Add(loadedPositionCoords.GroupId);
                    }
                }

                // SuperController.LogMessage("GroupList.Count: " + GroupList.Count);

                // Making sure group list contains at least 1 group
                if (GroupList.Count == 0)
                {
                    GroupList.Add("0");
                }

                PositionChooser.valNoCallback = "";
                PositionChooser.choices = null; // force UI sync
                PositionChooser.choices = GetCameraTitlesStringList(SelectedGroupId, PositionCoordinatesList);
                GroupChooser.choices = null; // force UI sync
                GroupChooser.choices = GroupList;

                // default to first items for list
                // GroupChooser.val = "0";
                PositionChooser.val = PositionCoordinatesList[0].PositionTitle;
                UpdateTextFields("0", PositionCoordinatesList[0].PositionTitle);
            }
        }

        UpdateFlatGroupAndPositionCoordinatesStringList();
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
            PositionCoordinatesList.Clear();
            int i = 0;
            // Fill our string list with the saved coordinates
            foreach (JSONNode node in coordsArray)
            {
                string coords = node.Value;
                PositionCoordinatesList.Add(new PositionCoordinates("0", cameraArray[i], coords));
                //SuperController.LogMessage("adding camera coords: " + cameraArray[i]);
                i++;
            }

            //SuperController.LogMessage("setting choices");

            PositionChooser.valNoCallback = "";
            PositionChooser.choices = null; // force UI sync
            PositionChooser.choices = GetCameraTitlesStringList("0", PositionCoordinatesList);
            GroupChooser.choices = GroupList;

            //SuperController.LogMessage("setting defaults");

            // default to first items for list
            GroupChooser.val = "0";
            PositionChooser.val = PositionCoordinatesList[0].PositionTitle;
            UpdateTextFields("0", PositionCoordinatesList[0].PositionTitle);
        }
    }

    protected void AddOrUpdateCameraList(string groupId, string cameraTitle, string coordsAsString)
    {
        bool titleFoundInList = false;
        for (int i = 0; i < PositionCoordinatesList.Count; i++)
        {
            PositionCoordinates currentPositionCoordinates = PositionCoordinatesList[i];

            if (currentPositionCoordinates.GroupId == groupId && currentPositionCoordinates.PositionTitle == cameraTitle)
            {
                // title already exists, so we update the value
                currentPositionCoordinates.CoordinatesAsString = coordsAsString;
                titleFoundInList = true;
                break;
            }
        }

        if (!titleFoundInList)
        {
            PositionCoordinatesList.Add(new PositionCoordinates(groupId, cameraTitle, coordsAsString));
        }
    }

    // This happens when the button "add coords" is pressed
    protected void OnAddNewCoords()
    {
        string positionTitle = PositionTitleInputFieldUI.text;
        Vector3 newPosition;
        Transform newRotation;

        // This means we add the coordinates for the containing atom, not the main monitor camera
        if (ApplyToAtomUI.val)
        {
            // get atom position
            newPosition = ContainingAtom.mainController.transform.position;

            // get atom rotation
            newRotation = ContainingAtom.mainController.transform;
        }
        else
        {
            var sc = SuperController.singleton;

            // get camera position
            newPosition = sc.centerCameraTarget.transform.position;

            // get camera rotation
            newRotation = sc.MonitorCenterCamera.transform;
        }

        // Create Coordinates object
        PositionCoordinates tmpCoords = new PositionCoordinates(SelectedGroupId, positionTitle, newPosition, newRotation.eulerAngles);

        // We add the coordinates to a string list, so at each position in the list (ID), we have a set of coordinates
        // but check if we need to update instead of adding a value
        AddOrUpdateCameraList(SelectedGroupId, positionTitle, tmpCoords.PositionCoordsToString());

        RefreshChoosers(positionTitle);

        // Now that we've added the camera title to the selector, let's suggest the next title
        string nextCameraName = SuggestNextCameraName(positionTitle);
        PositionTitleInputFieldUI.text = nextCameraName;
        PositionTitleUI.val = nextCameraName;

        UpdateTextFields(SelectedGroupId, positionTitle);
    }

    private void DeleteCoords()
    {
        // get current group
        string currentSelectedGroup = SelectedGroupId;

        // get current camera
        string currentSelectedCamera = SelectedPositionChooserTitle;

        // get index of current camera and coords
        int currentCameraIndex = -1;
        for (int i = 0; i < PositionCoordinatesList.Count; i++)
        {
            if (PositionCoordinatesList[i].GroupId == currentSelectedGroup && PositionCoordinatesList[i].PositionTitle == currentSelectedCamera)
            {
                currentCameraIndex = i;
                break;
            }
        }

        if (currentCameraIndex != -1)
        {
            PositionCoordinatesList.RemoveAt(currentCameraIndex);
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
            previousCameraInList = PositionCoordinatesList[currentCameraIndex - 1].PositionTitle;
            matchingGroupId = PositionCoordinatesList[currentCameraIndex - 1].GroupId;
        }
        else if (PositionCoordinatesList.Count > 0)
        {
            previousCameraInList = PositionCoordinatesList[0].PositionTitle;
            matchingGroupId = PositionCoordinatesList[0].GroupId;
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
        SetCoords(cameraVectors.Position, cameraVectors.Rotation);
    }

    // Lookup the coordinates from the list and set the camera to that position
    // The value has the format of: Group ID: '<GroupId>', Camera: '<CameraTitle>'
    protected void OnSetCoordsAction(string value)
    {
        string[] groupAndCameraStringArray = value.Split('\'');
        string groupId = groupAndCameraStringArray[1];
        string positionTitle = groupAndCameraStringArray[3];

        // get string from list by id
        string coordsString = "";
        for (int i = 0; i < PositionCoordinatesList.Count; i++)
        {
            if (PositionCoordinatesList[i].GroupId == groupId && PositionCoordinatesList[i].PositionTitle == positionTitle)
            {
                coordsString = PositionCoordinatesList[i].CoordinatesAsString;
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
                    Vector3 newPosition = new Vector3(float.Parse(coordsStringArray[2]), float.Parse(coordsStringArray[3]), float.Parse(coordsStringArray[4]));
                    Vector3 newRotation = new Vector3(float.Parse(coordsStringArray[5]), float.Parse(coordsStringArray[6]), float.Parse(coordsStringArray[7]));

                    SetCoords(newPosition, newRotation);
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

        SelectedPositionChooserTitle = positionTitle;
    }

    // Here we get the coordinates via parameters set the camera to that position
    protected void SetCoords(Vector3 newPosition, Vector3 newRotation)
    {
        // This means we apply the coordinates to the containing atom, not the main monitor camera
        if (ApplyToAtomUI.val)
        {
            // SuperController.LogMessage($"Applying to main containing atom");
            ContainingAtom.mainController.transform.position = newPosition;
            ContainingAtom.mainController.transform.eulerAngles = newRotation;
        }
        else
        {
            // SuperController.LogMessage($"Applying to main camera");
            var sc = SuperController.singleton;

            // This part is copied from the SpawnPoint script from AcidBubbles -> Kudos man!
            // I would have NEVER figured out how to calculate this stuff
            sc.ResetNavigationRigPositionRotation();

            var navigationRigTransform = sc.navigationRig.transform;

            navigationRigTransform.eulerAngles = newRotation;

            var targetPosition = newPosition;
            navigationRigTransform.eulerAngles = new Vector3(0, newRotation.y, 0);

            var centerCameraPosition = sc.centerCameraTarget.transform.position;
            var teleportPosition = targetPosition + (navigationRigTransform.position - centerCameraPosition);

            navigationRigTransform.position = new Vector3(teleportPosition.x, 0, teleportPosition.z);
            sc.playerHeightAdjust += (targetPosition.y - centerCameraPosition.y);

            var monitorCenterCameraTransform = sc.MonitorCenterCamera.transform;
            monitorCenterCameraTransform.eulerAngles = newRotation;
            monitorCenterCameraTransform.localEulerAngles = new Vector3(monitorCenterCameraTransform.localEulerAngles.x, 0, 0);
        }
    }

    protected void OnSetCoordsActionRandomAnyGroup()
    {
        string coordsAsString;

        // get a random camera from the list
        int randomListIndex = UnityEngine.Random.Range(0, PositionCoordinatesList.Count);
        {
            coordsAsString = PositionCoordinatesList[randomListIndex].CoordinatesAsString;
        }

        PositionCoordinates tmpPositionCoordinates = CreatePositionCoordinatesFromString(coordsAsString, "");

        SetCoords(tmpPositionCoordinates.Position, tmpPositionCoordinates.Rotation);
        SelectedPositionChooserTitle = PositionCoordinatesList[randomListIndex].PositionTitle;
    }

    protected void OnSetCoordsActionRandomSpecificGroup(string groupId)
    {
        string coordsAsString = "";

        // count max random number
        int maxRandomNumber = -1;

        for (int i = 0; i < PositionCoordinatesList.Count; i++)
        {
            if (PositionCoordinatesList[i].GroupId == groupId)
            {
                maxRandomNumber++;
            }
        }

        if (maxRandomNumber > -1)
        {
            // get a random camera from the list within the group
            int randomListIndex = UnityEngine.Random.Range(0, maxRandomNumber);
            int counter = 0;
            int indexFound = -1;
            for (int i = 0; i < PositionCoordinatesList.Count; i++)
            {
                if (PositionCoordinatesList[i].GroupId == groupId)
                {
                    if (counter == randomListIndex)
                    {
                        coordsAsString = PositionCoordinatesList[i].CoordinatesAsString;
                        indexFound = i;
                        break;
                    }
                    counter++;
                }
            }

            PositionCoordinates tmpPositionCoordinates = CreatePositionCoordinatesFromString(coordsAsString, "");

            SetCoords(tmpPositionCoordinates.Position, tmpPositionCoordinates.Rotation);
            SelectedPositionChooserTitle = PositionCoordinatesList[indexFound].PositionTitle;
        }
    }

    // Lookup the coordinates from the list (by title) and get the next list item. Stops at the last item in the list.
    protected void OnSetCoordsActionNext()
    {
        // get current selected camera
        string cameraTitle = SelectedPositionChooserTitle;

        if (!string.IsNullOrEmpty(cameraTitle))
        {
            // get string from list by id
            string coordsAsString = "";
            for (int i = 0; i < PositionCoordinatesList.Count; i++)
            {
                if (PositionCoordinatesList[i].PositionTitle == cameraTitle)
                {
                    if (i < PositionCoordinatesList.Count)
                    {
                        // Get the next camera
                        i++;
                    }
                    else
                    {
                        // if this is the last camera in the list, give back last item
                    }

                    coordsAsString = PositionCoordinatesList[i].CoordinatesAsString;
                    SelectedGroupId = PositionCoordinatesList[i].GroupId;
                    SelectedPositionChooserTitle = PositionCoordinatesList[i].PositionTitle;
                    break;
                }
            }

            PositionCoordinates tmpPositionCoordinates = CreatePositionCoordinatesFromString(coordsAsString, "");
            SetCoords(tmpPositionCoordinates.Position, tmpPositionCoordinates.Rotation);
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
        string cameraTitle = SelectedPositionChooserTitle;

        if (!string.IsNullOrEmpty(cameraTitle))
        {
            // get string from list by id
            string coordsAsString = "";
            int nextCamera = -1;
            for (int i = 0; i < PositionCoordinatesList.Count; i++)
            {
                if (PositionCoordinatesList[i].PositionTitle == cameraTitle)
                {
                    if (i == PositionCoordinatesList.Count - 1)
                    {
                        // if this is the last camera in the list, give back the first camera
                        nextCamera = 0;
                    }
                    else
                    {
                        nextCamera = i + 1;
                    }

                    coordsAsString = PositionCoordinatesList[nextCamera].CoordinatesAsString;
                    SelectedGroupId = PositionCoordinatesList[nextCamera].GroupId;
                    SelectedPositionChooserTitle = PositionCoordinatesList[nextCamera].PositionTitle;
                    break;
                }
            }

            PositionCoordinates tmpPositionCoordinates = CreatePositionCoordinatesFromString(coordsAsString, "");
            SetCoords(tmpPositionCoordinates.Position, tmpPositionCoordinates.Rotation);
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
        for (int i = 0; i < PositionCoordinatesList.Count; i++)
        {
            if (PositionCoordinatesList[i].GroupId == groupId && PositionCoordinatesList[i].PositionTitle == cameraTitle)
            {
                CoordsTextInputFieldUI.text = PositionCoordinatesList[i].CoordinatesAsString;
                CoordsTextUI.val = CoordsTextInputFieldUI.text;
                requestedCameraFound = true;
                break;
            }
        }

        if (!requestedCameraFound)
        {
            // Select the first camera from the current group
            foreach (PositionCoordinates mc in PositionCoordinatesList)
            {
                if (mc.GroupId == groupId)
                {
                    CoordsTextInputFieldUI.text = mc.CoordinatesAsString;
                    CoordsTextUI.val = mc.CoordinatesAsString;
                    cameraTitle = mc.PositionTitle;
                }
            }
        }

        SelectedGroupId = groupId;
        SelectedPositionChooserTitle = cameraTitle;
    }

    protected void RefreshChoosers(string cameraTitle)
    {
        // Update chooser for action
        UpdateFlatGroupAndPositionCoordinatesStringList();

        // Update group chooser
        GroupChooser.choices = null; // force UI sync
        GroupChooser.choices = GroupList;

        // Update camera title chooser
        PositionChooser.valNoCallback = "";
        PositionChooser.choices = null; // force UI sync
        PositionChooser.choices = GetCameraTitlesStringList(SelectedGroupId, PositionCoordinatesList);

        if (!string.IsNullOrEmpty(cameraTitle))
        {
            PositionChooser.val = cameraTitle;
        }
    }

    private void OnChangeSelectedGroupChoice(string groupId)
    {
        SelectedGroupId = groupId;
        RefreshChoosers("");
        UpdateTextFields(groupId, SelectedPositionChooserTitle);
    }

    protected void OnChangeSelectedCameraTitle(string cameraTitle)
    {
        // here we get the selected camera title and want to update the text field
        UpdateTextFields(SelectedGroupId, cameraTitle);
    }

    private void OnCameraTitleTextChanged()
    {
        CoordsTextUI.val = PositionTitleInputFieldUI.text;
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
        PositionTitleUI = new JSONStorableString("CameraTextUI", "_default_") { isStorable = false, isRestorable = false };

        // Temporary vars
        UIDynamicTextField tmpTextfield;
        UIDynamicTextField tmp2Textfield;
        UIDynamicToggle tmpToggle;

        // Creating components

        // ******** Camera or Containing Atom *******
        ApplyToAtomUI = new JSONStorableBool("ApplyToAtomUI", false);
        ApplyToAtomUI.setCallbackFunction += (val) => { OnApplyToAtomChanged(val); };
        tmpToggle = CreateToggle(ApplyToAtomUI, true);
        tmpToggle.label = "Target containing Atom?";
        RegisterBool(ApplyToAtomUI);
        globalControlsUIs.Add((UIDynamic)tmpToggle);

        // ******* CAMERA CHOOSER  ***********
        PositionChooser = new JSONStorableStringChooser("Position ID", GetCameraTitlesStringList(SelectedGroupId, PositionCoordinatesList), "", "Position ID")
        {
            isRestorable = true,
            isStorable = true,
            storeType = JSONStorableParam.StoreType.Full
        };
        RegisterStringChooser(PositionChooser);
        PositionChooser.setCallbackFunction += (val) =>
        {
            OnChangeSelectedCameraTitle(val);
        };
        UIDynamicPopup DSelsp = CreateScrollablePopup(PositionChooser, true);
        DSelsp.labelWidth = 250f;
        globalControlsUIs.Add((UIDynamic)DSelsp);

        // ******* BUTTONS FOR CAMERA CHOOSER ***********
        // add button
        UIDynamicButton addCoordsBtn = CreateButton("Add Position", true);
        addCoordsBtn.button.onClick.AddListener(() => { OnAddNewCoords(); });
        globalControlsUIs.Add((UIDynamic)addCoordsBtn);
        setButtonColor(addCoordsBtn, new Color(0.3f, 0.6f, 0.3f, 1f));
        setButtonTextColor(addCoordsBtn, new Color(1f, 1f, 1f, 1f));
        setupButtonWithLayout(addCoordsBtn, 190f);

        // delete button
        UIDynamicButton deleteCoordsBtn = CreateButton("Delete Position", true);
        deleteCoordsBtn.button.onClick.AddListener(() => { DeleteCoords(); });
        setButtonColor(deleteCoordsBtn, new Color(0.6f, 0.3f, 0.3f, 1f));
        setButtonTextColor(deleteCoordsBtn, new Color(1f, 1f, 1f, 1f));
        globalControlsUIs.Add((UIDynamic)deleteCoordsBtn);
        setupButtonWithoutLayout(deleteCoordsBtn, 190f, new Vector2(210, -215));

        UIDynamicButton moveUpDialogBtn = CreateButton("↑", true);
        moveUpDialogBtn.button.onClick.AddListener(() => { OnMoveCurrentDialog(false); });
        setButtonColor(moveUpDialogBtn, new Color(0.1f, 0.5f, 0.6f, 1f));
        setButtonTextColor(moveUpDialogBtn, new Color(1f, 1f, 1f, 1f));
        globalControlsUIs.Add((UIDynamic)moveUpDialogBtn);
        setupButtonWithoutLayout(moveUpDialogBtn, 50f, new Vector2(410, -215));

        UIDynamicButton moveDownDialogBtn = CreateButton("↓", true);
        moveDownDialogBtn.button.onClick.AddListener(() => { OnMoveCurrentDialog(true); });
        setButtonColor(moveDownDialogBtn, new Color(0.1f, 0.5f, 0.6f, 1f));
        setButtonTextColor(moveDownDialogBtn, new Color(1f, 1f, 1f, 1f));
        globalControlsUIs.Add((UIDynamic)moveDownDialogBtn);
        setupButtonWithoutLayout(moveDownDialogBtn, 50f, new Vector2(470, -215));


        // test button
        UIDynamicButton setCoordsBtn = CreateButton("Test Position", true);
        setCoordsBtn.button.onClick.AddListener(() => { SetCoords(); });
        globalControlsUIs.Add((UIDynamic)setCoordsBtn);



        // DEBUG button
        UIDynamicButton debugCoordsBtn = CreateButton("DEBUG", true);
        debugCoordsBtn.button.onClick.AddListener(() => { DebugButton(); });
        globalControlsUIs.Add((UIDynamic)debugCoordsBtn);

        // DEBUG button
        UIDynamicButton debugGroupList = CreateButton("Recreate GroupList", true);
        debugGroupList.button.onClick.AddListener(() => { ReCreateGroupListDEBUG(); });
        globalControlsUIs.Add((UIDynamic)debugGroupList);


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
        setButtonColor(addGroupBtn, new Color(0.3f, 0.6f, 0.3f, 1f));
        setButtonTextColor(addGroupBtn, new Color(1f, 1f, 1f, 1f));
        setupButtonWithLayout(addGroupBtn, 190f);
        globalControlsUIs.Add((UIDynamic)addGroupBtn);

        // delete button
        UIDynamicButton deleteGroup = CreateButton("Delete group", true);
        deleteGroup.button.onClick.AddListener(() => { DeleteGroup(); });
        setButtonColor(deleteGroup, new Color(0.6f, 0.3f, 0.3f, 1f));
        setButtonTextColor(deleteGroup, new Color(1f, 1f, 1f, 1f));
        globalControlsUIs.Add((UIDynamic)deleteGroup);
        setupButtonWithoutLayout(deleteGroup, 190f, new Vector2(210, -460));

        // ******* SECTION TITLE ***********
        UIPositionSectionTitle = CreateStaticDescriptionText("UICameraSectionTitle", "<color=#000><size=35><b>Next position name</b></size></color>", false, 55, TextAnchor.MiddleLeft);
        globalControlsUIs.Add((UIDynamic)UIPositionSectionTitle);

        // ******* CAMERA TITLE TEXTFIELD ***********
        positionTitle = new JSONStorableString("CameraTitle", "0");
        tmp2Textfield = CreateTextField(positionTitle);
        SetupTextField(tmp2Textfield, 50f, false, false);
        PositionTitleInputFieldUI = tmp2Textfield.UItext.gameObject.AddComponent<InputField>();
        PositionTitleInputFieldUI.textComponent = tmp2Textfield.UItext;
        PositionTitleInputFieldUI.lineType = InputField.LineType.SingleLine;
        globalControlsUIs.Add((UIDynamic)tmp2Textfield);
        CoordsTextUI.valNoCallback = "0";
        PositionTitleInputFieldUI.text = "0";
        PositionTitleInputFieldUI.onValueChanged.AddListener(delegate { OnCameraTitleTextChanged(); });

        // ******* SECTION TITLE ***********
        UICoordsSectionTitle = CreateStaticDescriptionText("UICoordsSectionTitle", "<color=#000><size=35><b>Position Coordinates</b></size></color>", false, 55, TextAnchor.MiddleLeft);
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
                    "Note: See new FAQ at the end.\n\n" +
                    "Ok, the idea of this plugin is that you can move your MONITOR CAMERA or ANY ATOM to a certain place, store the coordinates of your monitor camera or the Atom in a list and then later on set your camera or the Atom back to the saved coordinates.\n\n" +
                    "This gets cool, if you store many coordinates and you are using another plugin (like VAMStory) to tell a story and want the user to see the exact camera angles or place an Atom at a certain spot (or both) that you had planned.\n\n" +
                    "So it's mainly great to use with another plugin, by calling an action called 'Set Camera Position' or any of the other actions of this plugin.\n\n" +
                    "You can give the coordinates any kind of title, like Cam1. It helps to have a number at the end, the plugin will increment the number for you after adding, so you can keep going.\n\n" +
                    "FAQ\n" +
                    "------\n\n" +
                    "What does 'Target containing Atom' mean?\n" +
                    "If the toggle is on then all buttons and actions are using the Atom that contains this plugin for getting or setting coordinates. That way you can move an Atom around. If the toggle is off, then the plugin gets or sets the coordinates to the main monitor camera.\n\n"
                );
        UIDynamicTextField helpWindow = CreateTextField(HelpText, false);
        helpWindow.height = 850.0f;
        globalControlsUIs.Add((UIDynamic)helpWindow);
    }

    private void OnMoveCurrentDialog(bool moveItemDown)
    {
        //false = move position up +1
        //true = move position down -1

        // get the current item
        string cameraTitle = "";
        if (!string.IsNullOrEmpty(SelectedGroupId) && !string.IsNullOrEmpty(SelectedPositionChooserTitle))
        {
            int index = 0;

            foreach (PositionCoordinates coordinate in PositionCoordinatesList)
            {
                if (coordinate.PositionTitle == SelectedPositionChooserTitle)
                {
                    cameraTitle = coordinate.PositionTitle;
                    break;
                }
                index++;
            }

            if (moveItemDown == true)
            {
                if (index > 0 && index < PositionCoordinatesList.Count)
                {
                    PositionCoordinates itemToMove = PositionCoordinatesList[index]; // Get the item to be moved
                    PositionCoordinatesList.RemoveAt(index); // Remove the item from the current position
                    PositionCoordinatesList.Insert(index - 1, itemToMove); // Insert the item at the new position
                }
            }
            else
            {
                if (index >= 0 && index < PositionCoordinatesList.Count)
                {
                    PositionCoordinates itemToMove = PositionCoordinatesList[index]; // Get the item to be moved
                    PositionCoordinatesList.RemoveAt(index); // Remove the item from the current position
                    PositionCoordinatesList.Insert(index + 1, itemToMove); // Insert the item at the new position
                }
            }

            RefreshChoosers(cameraTitle);
        }
    }

    private void OnApplyToAtomChanged(bool value)
    {
        // Set global value to true or false
        ApplyToAtomUI.valNoCallback = value;
    }

    private void OnChangeSelectedGroupAndCameraChoice(string val)
    {
        RefreshChoosers("");
    }

    private void DebugButton()
    {
        SuperController.LogMessage("-------------------------");
        foreach (PositionCoordinates mc in PositionCoordinatesList)
        {
            SuperController.LogMessage(mc.CoordinatesAsString);
        }
        SuperController.LogMessage("-------------------------");
    }




    private void DeleteGroup()
    {
        // if it is the last group, don't delete it
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
            List<PositionCoordinates> toDeleteList = new List<PositionCoordinates>();

            foreach (PositionCoordinates mc in PositionCoordinatesList)
            {
                if (mc.GroupId == SelectedGroupId)
                {
                    toDeleteList.Add(mc);
                }
            }

            foreach (PositionCoordinates mcDel in toDeleteList)
            {
                PositionCoordinatesList.Remove(mcDel);
            }

            // remove group from grouplist
            GroupList.Remove(deletedGroupId);

            // shift all cameras, that have a higher higher groupId than the deleted group to a new group
            foreach (PositionCoordinates mc in PositionCoordinatesList)
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
        foreach (PositionCoordinates mc in PositionCoordinatesList)
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
        SelectedPositionChooserTitle = "";
        PositionChooser.val = "";

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

    private void setupButtonWithoutLayout(UIDynamicButton target, float newSize, Vector2 newPosition)
    {
        ContentSizeFitter tmpCSF = target.button.transform.gameObject.AddComponent<ContentSizeFitter>();
        tmpCSF.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.MinSize;
        LayoutElement tmpLE = target.button.transform.gameObject.GetComponent<LayoutElement>();
        RectTransform tmpRectT = target.button.transform.GetComponent<RectTransform>();
        Rect tmpRect = tmpRectT.rect;
        tmpRectT.pivot = new Vector2(0f, 0.5f);
        tmpLE.minWidth = newSize;
        tmpLE.preferredWidth = newSize;
        tmpLE.ignoreLayout = true;
        tmpRectT.anchoredPosition = newPosition;
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

    private void setButtonColor(UIDynamicButton targetBtn, Color newColor)
    {
        targetBtn.buttonColor = newColor;
    }

    private void setButtonTextColor(UIDynamicButton targetBtn, Color newColor)
    {
        targetBtn.textColor = newColor;
    }

    private void setupButtonWithLayout(UIDynamicButton target, float newSize)
    {
        ContentSizeFitter tmpCSF = target.button.transform.gameObject.AddComponent<ContentSizeFitter>();
        tmpCSF.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.MinSize;
        LayoutElement tmpLE = target.button.transform.gameObject.GetComponent<LayoutElement>();
        RectTransform tmpRectT = target.button.transform.GetComponent<RectTransform>();
        Rect tmpRect = tmpRectT.rect;
        tmpRectT.pivot = new Vector2(0f, 0.5f);
        tmpLE.minWidth = newSize;
        tmpLE.preferredWidth = newSize;
    }

    protected class CameraVectors
    {
        public Vector3 Position;
        public Vector3 Rotation;
    }

    public class PositionCoordinates
    {
        public string GroupId;
        public string PositionTitle;
        public Vector3 Position;
        public Vector3 Rotation;
        private string _coordinatesAsString;
        public string CoordinatesAsString
        {
            get
            {
                // this makes sure, we return the latest value
                return PositionCoordsToString();
            }

            set
            {
                _coordinatesAsString = value;
                PositionCoordinates tmpPositionCoordinates = CreatePositionCoordinatesFromString(_coordinatesAsString, "");
                Position = tmpPositionCoordinates.Position;
                Rotation = tmpPositionCoordinates.Rotation;
            }
        }

        public PositionCoordinates(string groupId, string positionTitle, string coordsAsString)
        {
            GroupId = groupId;
            PositionTitle = positionTitle;
            CoordinatesAsString = coordsAsString;
        }

        public PositionCoordinates(string groupId, string positionTitle, Vector3 position, Vector3 rotation)
        {
            Position = position;
            Rotation = rotation;
            GroupId = groupId;
            PositionTitle = positionTitle;
            _coordinatesAsString = PositionCoordsToString();
        }

        // This is the string representation of the monitor coordinates, that we can parse again later
        public string PositionCoordsToString()
        {
            string coordsAsString = GroupId + "_" + PositionTitle + "_" + Position.x + "_" + Position.y + "_" + Position.z + "_" + Rotation.x + "_" + Rotation.y + "_" + Rotation.z;
            return coordsAsString;
        }
    }
    public void UpdateFlatGroupAndPositionCoordinatesStringList()
    {
        if (PositionCoordinatesList != null)
        {
            if (FlatGroupAndPositionCoordinatesStringList != null)
            {
                FlatGroupAndPositionCoordinatesStringList.Clear();
            }

            foreach (PositionCoordinates mc in PositionCoordinatesList)
            {
                string groupIdAndCamera = "Group ID: '" + mc.GroupId + "', Camera: '" + mc.PositionTitle + "'";
                // string groupIdAndCamera = mc.MonitorCameraTitle;
                FlatGroupAndPositionCoordinatesStringList.Add(groupIdAndCamera);
            }
        }
    }

    public List<string> GetCameraTitlesStringList(string groupId, List<PositionCoordinates> positionCoordinatesList)
    {
        List<string> stringList = new List<string>();

        foreach (PositionCoordinates mc in positionCoordinatesList)
        {
            if (mc.GroupId == groupId)
            {
                stringList.Add(mc.PositionTitle);
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
                    newCV.Position = new Vector3(float.Parse(coordsStringArray[2]), float.Parse(coordsStringArray[3]), float.Parse(coordsStringArray[4]));
                    newCV.Rotation = new Vector3(float.Parse(coordsStringArray[5]), float.Parse(coordsStringArray[6]), float.Parse(coordsStringArray[7]));
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
                    newCV.Position = new Vector3(float.Parse(coordsStringArray[0]), float.Parse(coordsStringArray[1]), float.Parse(coordsStringArray[2]));
                    newCV.Rotation = new Vector3(float.Parse(coordsStringArray[3]), float.Parse(coordsStringArray[4]), float.Parse(coordsStringArray[5]));
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

    private static PositionCoordinates CreatePositionCoordinatesFromString(string coordsAsString, string cameraTitleForOldSaveFileVersion)
    {
        if (!string.IsNullOrEmpty(coordsAsString))
        {
            string groupId = "0";
            string cameraTitle = "";
            Vector3 position = new Vector3();
            Vector3 rotation = new Vector3();

            string[] coordsStringArray = coordsAsString.Split('_');

            PositionCoordinates newPositionCoords = null;

            if (coordsStringArray.Length == 8)
            {
                try
                {
                    groupId = coordsStringArray[0].ToString();
                    cameraTitle = coordsStringArray[1].ToString();
                    position = new Vector3(float.Parse(coordsStringArray[2]), float.Parse(coordsStringArray[3]), float.Parse(coordsStringArray[4]));
                    rotation = new Vector3(float.Parse(coordsStringArray[5]), float.Parse(coordsStringArray[6]), float.Parse(coordsStringArray[7]));

                    newPositionCoords = new PositionCoordinates(groupId, cameraTitle, position, rotation);
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
                    position = new Vector3(float.Parse(coordsStringArray[0]), float.Parse(coordsStringArray[1]), float.Parse(coordsStringArray[2]));
                    rotation = new Vector3(float.Parse(coordsStringArray[3]), float.Parse(coordsStringArray[4]), float.Parse(coordsStringArray[5]));

                    newPositionCoords = new PositionCoordinates(groupId, cameraTitle, position, rotation);
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

            return newPositionCoords;
        }
        else
        {
            SuperController.LogError($"Got an empty string coordinates to parse, returning null.");
            return null;
        }
    }
}