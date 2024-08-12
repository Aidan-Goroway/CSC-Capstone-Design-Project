using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;
using System.Text;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{

//-----###INITIALIZATION------------------------------------------------------------------------------------

    [SerializeField]
    private Rigidbody2D playerBody;
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private SpriteRenderer sRenderer;
    [SerializeField]
    private Text feedbackText;
    [SerializeField]
    private Image textBox;
    [SerializeField]
    private Image stickUI;
    [SerializeField]
    private List<Sprite> stickSprites = new List<Sprite>();
    [SerializeField]
    private Text trialText;
    [SerializeField]
    private Image trialRepresentation;
    [SerializeField]
    private List<Sprite> trialReps = new List<Sprite>();
    [SerializeField]
    private Image NextTextBox;
    [SerializeField]
    private Text NextText;

    private float Xinput;
    private float Yinput;

    private string walkAnimation = "Walk";
    private string crouchAnimation = "Crouch";

    private string punchAnimation = "Punch";
    private string kickAnimation = "Kick";

    private string DPanimation = "DragonPunch";
    private string TatsuAnimation = "SpinnyKick";
    private bool animationLockout = false;

    private bool isGrounded;
    private string groundTag = "Ground";

    private bool inputLock = false; //used to prevent unneccessary calls
    private bool nextBasedInputLock = false; // similar to inputLock, but tied to the NEXT button
    private bool practiceMode = true;
    /* Pratice mode:
        - Only available at the start of a trial
        - inputs will act as if they are reall, but arent charted on the .txt file
        - Practice Mode is quit using the Next button */

    private int neutralFrameCount = 0; //the num of frames (at 60 fps) we are in neutral for
    private int chargeFrameCount = 0; // the number of frames we are charging an input for
    private bool chargeEnough = false; // fully charged at 30 frames of hold

    private string unparsedPlayerInput = "5"; //may need to be changed, "" also causes issues
    private string passdownInput;
    private string generatedFeedback;

    private string IDEAL_INPUT = "5P"; // default value

    private int trialIndex = 0; // STARTS AT 0, goes to 9 (in accordance with the following Array)
    // DEBUG: manipulate from 0-9 to skip to a specific trial
    private string[] trialInputList =  {"5P", //Standard Punch (1)
                                        "5K", //Standard Kick  (2)
                                        "[4]6P", //Charge Punch (3)
                                        "22K", //"Double tap down" Kick (4)
                                        "236P", // Quiarter Circle Forwards Punch (5)
                                        "41236K", // Half Circle Forwards Kick (6)
                                        "236236P", // Double Quarter Circle Forwards Punch (7)
                                        "2141236K", // Q.Circle back, H.Circle Forwards Kick (8)
                                        "632147896K", // 360-Input Kick (9)
                                        "632146P"}; // H.Circle back, then forwards Punch (10)
                                        
                                                                                                  
    private int attemptIndex = 1; // STARTS AT 1, goes to 5
    private string defaultTrialText = "Current Goal:           Attempt 1/5";

    private bool killSwitch = false;

//========= Non-parameter passdown info for DFP =========================

    // These ints are pieces of info about an input that cannot be parsed from the state of the input alone.
    // They must be passsed down as continuously updated ints, or at least instantiated here.
    private int numOfArchetypesIDEAL;
    private int numOfArchetypesPLAYER;

    private Dictionary<string, string> archCharsDictionary = new Dictionary<string, string>();
    private Dictionary<string, string> PlayerArchCharsDictionary = new Dictionary<string, string>();
    private bool dictUpdateStopper1 = false; // The looping nature of Update() synergizes poorly with Dict keys...
    private bool dictUpdateStopper2 = false; // so we use these bools to stop update the dict after 1 loop.
    
    //"MISSING_ARCHS", "EXTRA_ARCHS", "WRONG_ARCH", "TOO_MANY_CHARS_1", "TOO_MANY_CHARS_2", "WRONG_BUTTON"
    private string FAILURE_CASE; 
    private char CONTEXT_1;
    private char CONTEXT_2;


//======================================================================

//--------###GAMESTATE FUNCTIONS/LOOPS----------------------------------------------------------------

    void Awake(){ // called on script startup
        playerBody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        sRenderer = GetComponent<SpriteRenderer>();
        feedbackText = GetComponent<Text>();
        textBox = GetComponent<Image>();
        stickUI = GetComponent<Image>();
        trialText = GetComponent<Text>();
        trialRepresentation = GetComponent<Image>();
        NextTextBox = GetComponent<Image>();
        NextText = GetComponent<Text>();
    }

    // Start is called (once) before the first frame update
    void Start(){
        // if (trialIndex != 0){ //remove after degugging complete //DEBUG
        //     defaultTrialText = "Trial " + (trialIndex + 1) + ", Attempt 1/5 _ _ _ _ _";
        // }
        defaultTrialText = "Current Goal: " + IDEAL_INPUT + "          Practice Mode";
        
        if (NextTextBox != null && gameObject.CompareTag("NextBox")){
            NextTextBox.enabled = true; // was false. now true, as practcie mode is on by default
        }
        if (textBox != null && gameObject.CompareTag("TextBox")){ // unless all subsequent enablers are tagCompared, asset.enable = false at Start() will be nullified
            textBox.enabled = true;
        }
        if (stickUI != null && gameObject.CompareTag("Stick")){ 
            stickUI.enabled = true;
        }
        if (trialRepresentation != null && gameObject.CompareTag("trialVis")){
            trialRepresentation.enabled = true;
        }
        IDEAL_INPUT = trialInputList[trialIndex];
        if (gameObject.CompareTag("Player")){
            writeResultsToFile(); // charts heading (Trial #[Attempt# Success? P.Input I.Input])
        }
    }

    // Update is called once per uncapped frame
    void Update(){

        if (trialText != null && gameObject.CompareTag("trialText")){
            trialText.text = defaultTrialText;
        }

        int directionalInput = takeDirectionalInputs();
        string buttonInput = takeButtonInputs();
        if (nextBasedInputLock){ // stops ALL inputs during the mandatory wait period
            directionalInput = 5;
            buttonInput = "";
        }
        nextButtonTake(); // Removes the context window and unlocks inputs

        // Handles the onscreen visual of the joystick
        if (gameObject.CompareTag("Stick")){
            AnimateStick(directionalInput);
        }

        // Raw input
        // Will "loop" here for a while until a button is pressed
        // This is because "" is returned until then.
        passdownInput = takeUnparssedInput(directionalInput, buttonInput);
        // Debug.Log("UNparssed Input is: " + passdownInput);

        // Input with duplicates gone (except for chargeInputs)
        passdownInput = parsePlayerInput(passdownInput, IDEAL_INPUT);
        // Debug.Log("Parssed Input is: " + passdownInput);

        // Input with no 5's (unless part of the input)
        passdownInput = removeFives(passdownInput, IDEAL_INPUT);
        
        // passdownInput exists to stop a lot of very unneccessary function calls
        // inputlock is needed because it stops unintended repeat inputs
        // nextBasedInputLock is similar, an inputLock based on the "bluebox"
        if (passdownInput != "" && !inputLock && !nextBasedInputLock && !killSwitch){
            if ((IDEAL_INPUT.Contains('[')) && (IDEAL_INPUT[IDEAL_INPUT.IndexOf('[') + 2] == ']')){
                    passdownInput = handleChargeInputs(passdownInput, IDEAL_INPUT);
                }
                else {
                    chargeEnough = true;
                }
            Debug.Log("Trial " + (trialIndex + 1) + ", Attempt " + attemptIndex + ", Five-less Input is: " + passdownInput); 
            Debug.Log("Trial " + (trialIndex + 1) + ", Attempt " + attemptIndex + ", Ideal Input is: " + IDEAL_INPUT);

            bool validatedInput = isInputValid(passdownInput, IDEAL_INPUT);

            AnimatePlayerSpecials(validatedInput, IDEAL_INPUT); //must be above handlePostTrialIndexing
            handlePostTrialIndexing(validatedInput, passdownInput); // updates several things:
                // IDEAL_INPUT is updated to the next trial
                // trial text is taken care of
                // writing results to a .txt file is also handled here
                // as well as changing the input rep graphics
                
            if (practiceMode){
                generatedFeedback = dynamicFeedbackProvider(validatedInput, IDEAL_INPUT, passdownInput);
            }
            else{
                generatedFeedback = dynamicFeedbackProvider(validatedInput, IDEAL_INPUT, passdownInput) + "\n\nPlease wait patiently for 3 seconds.";
            }

            displayUIElements(generatedFeedback); // Displays feedback-text on a textbox

            inputLock = true; //locks you out of input attempts for 3 seconds

            if (!practiceMode){
                nextBasedInputLock = true;
            }
        }
          
        // This comment dedicated to: MY FIRST UNITY CRASH WOOOO
        // Ok so dont use while loops unless you know what youre doing

        isPlayerCrouched(directionalInput);

        AnimatePlayerMovement(directionalInput);

        AnimatePlayerBasicAttacks(buttonInput);

    }

    //This function is called every fixed framerate frame, if the MonoBehaviour is enabled.
    //Primarily for physics calculations (including frame-based counters)
    // Effected by Fixed Timestep in the settings. Default is 0.02 (50 fps?)
    // I have set it to 0.01667 (60fps)
    //RUNS BEFORE Update() DOES
    void FixedUpdate(){
        int directionalInput = takeDirectionalInputs(); //only need to parse this once per frame anyway
        string buttonInput = takeButtonInputs();

        if ((IDEAL_INPUT.Contains('[')) && (IDEAL_INPUT[IDEAL_INPUT.IndexOf('[') + 2] == ']')){
            int chargeDirection;
            chargeDirection = int.Parse(IDEAL_INPUT[IDEAL_INPUT.IndexOf('[') + 1].ToString());
            chargeTimer(directionalInput, chargeDirection);
        }
        else {
            chargeEnough = true;
        }

        resetInputDetection(directionalInput, buttonInput); //must remain in use reguardless of chargeInput or not
    }

    //###FUNCTIONS----------------------------------------------------------

    //###BASIC INPUT TAKING----------------------------------------------------------

    /*
    Takes the player's inputs and returns their numeric annotation.
    */
    int takeDirectionalInputs(){ // will return int 1-9
        // Player will not be moving, so we wont actually transform player position with these inputs
        Xinput = Input.GetAxisRaw("Horizontal");
        Yinput = Input.GetAxisRaw("Vertical");
        
        if ((Xinput == 0) && (Yinput == 0)){ // 0
           return 5; //neutral case
        }
        else if ((Xinput == -1) && (Yinput == -1)){ // <v
            return 1;
        }
        else if((Xinput == 0) && (Yinput == -1)){ // v
            return 2;
        }
        else if ((Xinput == 1) && (Yinput == -1)){ // v>
            return 3;
        }
        else if ((Xinput == -1) && (Yinput == 0)){ // <
            return 4;
        }
        else if ((Xinput == 1) && (Yinput == 0)){ // >
            return 6;
        }
        else if((Xinput == -1) && (Yinput == 1)){ // <^
            return 7;
        }
        else if ((Xinput == 0) && (Yinput == 1)){ // ^
            return 8;
        } 
        else { //^>
            return 9;
        }
    }

    /*
    Takes the player's non-directional inputs.
    To simplify this experiemnt, we will only have 2
    distsinct attack buttons: (P)unch and (K)ick.
    Will return an empty string if when there is no button input.
    */
    string takeButtonInputs(){
        //Will return either P or K
        if (Input.GetButton("Fire1")){ // Punch
            return "P";
        }
        else if (Input.GetButton("Fire2")){ // Kick
            return "K";
        }
        else{
            return ""; // None, for the sake of a coherent input string
        }
    }

    void nextButtonTake(){

        if (Input.GetButton("Fire3") && !inputLock){

            updateContextBox(false, "N/A");
            if (feedbackText != null && gameObject.CompareTag("feedbackText")){
                feedbackText.text = "";
            }

            if (practiceMode){
                practiceMode = false;
            }
            else{ // normal 3-second wait period
                nextBasedInputLock = false;
            }
        }
    }

    /*
    Takes the identified player input, raw and unfiltered.
    The input will start with 5 and ends when a button press is registered.
    Returns the string we have created.
    A new input can be taken after 3 straight seconds of neutral (5).
    PARAMS:
    - directionalInput: the current hold direction of the control stick
    - buttonInput: the currently pressed button
    */
    string takeUnparssedInput(int directionalInput, string buttonInput){

        if ((unparsedPlayerInput[^1] != 'P') && (unparsedPlayerInput[^1] != 'K')){
            // adds to input as long as attack isnt signalled
            unparsedPlayerInput += directionalInput;
            unparsedPlayerInput += buttonInput;
        }

        if ((unparsedPlayerInput[^1] == 'P') || (unparsedPlayerInput[^1] == 'K')){
            return unparsedPlayerInput;
        }
        else{
            return "";
        }
    }

    /*
    Resets the ability for inputs to be taken.
    This happens every 3 seconds as long as no directions oe buttons are held.
    Also serves as a way to reset animation-based timers and the charge-timer.
    PARAMS:
    - directionalInput: the current hold direction of the control stick
    - buttonInput: the currently pressed button 
    */
    int resetInputDetection(int directionalInput, string buttonInput){
        // CALL IN FIXEdUPDATES()
        // allows an Unparsed input to be taken again

        // counts up for every (straight) frame in neutral
        // resets counter otherwise
        if ((directionalInput == 5) && (buttonInput == "")){
            neutralFrameCount += 1;
        }
        else{
            neutralFrameCount = 0;
        }

        if (neutralFrameCount > 59){ // 1 second
            // Debug.Log("1 Second has passed!");
            animator.SetBool(DPanimation, false);
            animator.SetBool(TatsuAnimation, false);

            if (practiceMode){
                updateContextBox(true, "Start Trial");
                inputLock = false;
                animationLockout = false;

                unparsedPlayerInput = "5";
                neutralFrameCount = 0;

                chargeEnough = false;

                CONTEXT_1 = '0';
                CONTEXT_2 = '0';

                FAILURE_CASE = "";

                // reset archetype dictionaries
                dictUpdateStopper1 = false;
                dictUpdateStopper2 = false;
                archCharsDictionary.Clear();
                PlayerArchCharsDictionary.Clear();
            }
        }

        //if in neutral for 180 frames straight (3 seconds),
        //resets unparsedPlayerInput, among other things
        if (neutralFrameCount > 179){

            if (inputLock){ // only should run if the "reset" is after an input
                if (!practiceMode){
                    updateContextBox(true, "Advance");
                }
            }
            
            inputLock = false;
            animationLockout = false;

            unparsedPlayerInput = "5";
            neutralFrameCount = 0;

            chargeEnough = false;

            CONTEXT_1 = '0';
            CONTEXT_2 = '0';

            FAILURE_CASE = "";

            // reset archetype dictionaries
            dictUpdateStopper1 = false;
            dictUpdateStopper2 = false;
            archCharsDictionary.Clear();
            PlayerArchCharsDictionary.Clear();

        }

        return neutralFrameCount;
    }

    //###INPUT REFINEMENT----------------------------------------------------------

    /*
    Takes the raw unfiltered input taken from takeUnparssedInput() and removes duplicates.
    The end result is a string that much more closely resembles a numerically annotated input.
    This result is returned.
    PARAMS:
    - unparsedPlayerInput: The raw player input
    - idealInput: The perfect input the system is expecting
    */
    string parsePlayerInput(string unparsedPlayerInput, string idealInput){

       if (string.IsNullOrEmpty(unparsedPlayerInput)){
            return unparsedPlayerInput;
            } // Return the input string if it's empty or null

        char[] inputArray = unparsedPlayerInput.ToCharArray();
        int length = inputArray.Length;

        // Initialize the result with the first character
        int resultLength = 1;
        for (int i = 1; i < length; i++){
                if (inputArray[i] != inputArray[i - 1]){
                    inputArray[resultLength] = inputArray[i];
                    resultLength++;  
                }
        }

        return new string(inputArray, 0, resultLength);

    }

    /*
    A standard feature of the numeric annotation system is that unless moves are strictly
    performed in the neutral postion (5P, 5K, ect.), they do not include fives in their
    numeric annotation. This function removes those fives, and returns its result.
    In events that a neutral input is the ideal, this function essentially does nothing.
    PARAMS:
    - parsedAndTrimmedPlayerInput: A player input with its unneccessary duplicates removed
    - idealInput: The perfect input the system is expecting
    */
    string removeFives(string parsedAndTrimmedPlayerInput, string idealInput){
            
        if (idealInput[0] == '5'){ //Neutral Inputs
            return parsedAndTrimmedPlayerInput;
        }
        else{

            StringBuilder result = new StringBuilder();
            
            foreach (char c in parsedAndTrimmedPlayerInput){
                if (c != '5'){
                    result.Append(c);
                }
            }

            return result.ToString();  

        }
    }

    /*
    In the event of a chargeInput, this function will handle it.
    This means adjusting the player input to match the ideal one.
    PARAMS:
    - parsedPlayerInput: The input from the player. Parsed and cleaned, ready for comparison. 
    - idealInput: The perfect input the system is expecting
    */
    string handleChargeInputs(string parsedPlayerInput, string idealInput){

        if(chargeEnough){
            // Debug.Log("Before Error: " + parsedPlayerInput);
            string chargedInput = "[" + parsedPlayerInput[0] + "]" + parsedPlayerInput.Substring(1);
            return chargedInput;
        }
        else{
            FAILURE_CASE = "NOT_ENOUGH_CHARGE";
            return parsedPlayerInput;
        }

    }

    /*
    Helper function that tracks the amount of time a charge is held for.
    PARAMS:
    - currentDirection: The currently held direction
    - chargeDirection: The direction meant to be held for charge
    */
    int chargeTimer(int currentDirection, int chargeDirection){

        if (currentDirection == chargeDirection){
            chargeFrameCount += 1;
        }
        else{
            chargeFrameCount = 0;
        }

        if (chargeFrameCount > 29){
            chargeEnough = true;
        }

        return chargeFrameCount;

    }

    //###VALIDITY AND FEEDBACK----------------------------------------------------------

    /*
    The big one.
    Takes the Parsed Player Input and compares it to the Ideal Input.
    If the Ideal Input is a Charge Input, that case will also be handled here (IF IMPLEMENTED, ANYWAY).
    Returns True or False depending on if an input is valid.
    PARAMS:
    - parsedPlayerInput: The input from the player. Parsed and cleaned, ready for comparison. 
    - idealInput: The perfect input the system is expecting
    */
    bool isInputValid(string parsedPlayerInput, string idealInput){
        
        if (FAILURE_CASE == "NOT_ENOUGH_CHARGE"){
            return false;
        }

        List<string> foundArchetypesIdeal = areArchetypes(idealInput, true);
        List<string> foundArchetypesPlayer = areArchetypes(parsedPlayerInput, false);
        //A list of our archetypes

        Debug.Log("found ideals: ");
        for (int i = 0; i < foundArchetypesIdeal.Count; i++){
            Debug.Log(foundArchetypesIdeal[i]);
        }

        Debug.Log("found players: ");
        for (int i = 0; i < foundArchetypesPlayer.Count; i++){
             Debug.Log(foundArchetypesPlayer[i]);
        }

        //lists should be identical, but that is handled later. This checks the amount of archetypes, not their makeup.
        if (foundArchetypesIdeal.Count != foundArchetypesPlayer.Count){
            Debug.Log("FAILURE_CASE: Wrong number of Archetypes");

            if (foundArchetypesIdeal.Count > foundArchetypesPlayer.Count){ //missing archs
                FAILURE_CASE = "MISSING_ARCHS";
                
            }
            else{ // extra archs
                FAILURE_CASE = "EXTRA_ARCHS";
            }

            return false;
        }

        // This loop is what checks the specifics of the archetypes.
        for (int i = 0; i < foundArchetypesIdeal.Count; i++){ //for each archetype in ideal input

            if ((foundArchetypesIdeal[i] != foundArchetypesPlayer[i]) && //archs are different... AND...
                !(((foundArchetypesIdeal[i])[^1] == '6') && ((foundArchetypesPlayer[i])[0] == '6')) && // as long as [the ideal arch ends with 6, while the player arch input starts with 6] is false AND...
                //archs ending in 6 -> archs starting with 6 was causing a glitch
                    // GLITCH: the fix for this makes it so incorrect archs meeting this criteria send a true, (! making it false), and thus denying the return false.

                //I dont particularly like hard coding, but I see no other way of solving this VERY niche issue
                !(foundArchetypesPlayer[i].Contains(foundArchetypesIdeal[i]))){ //the player arch isnt a different arch that happens to contains the ideal arch
                Debug.Log("FAILURE_CASE: Archetype is Wrong");
                FAILURE_CASE = "WRONG_ARCH";
                return false;
            }
        }

        return helperIsInputValid(idealInput, parsedPlayerInput);
        
    }

    /*
    Helper function.
    Returns a list of archetypes found in a given Input.
    PARAMS:
    - givenInput: An input. May be ideal or player provided
    - isIdealInput: Is the input ideal?
    */
    private List<string> areArchetypes(string givenInput, bool isInputIdeal){

        List<string> archetypesList = new List<string> {"632147896", "63214", "41236", "236", "214", "6323"};

        List<string> foundArchetypes = new List<string>(); //archetypes we find in the ideal input
        
        string input = ""; //effectively a list we can check
        int archNUMCounter = 1;

        for (int i = 0; i < givenInput.Length; i++){ //for each char in the input

            input += givenInput[i];

            if (input.Length > 2){ //no archs are shorter than 3 chars

                for (int archNum = 0; archNum < archetypesList.Count; archNum++){ // for each possible archetype in the list

                    if (input.Contains(archetypesList[archNum])){  //if input contains an archetype... 
                    
                        if (!((givenInput.Contains("632147896")) && ((archetypesList[archNum] == "63214") || (archetypesList[archNum] == "214")))){  //Stable (consideres (63)214 identical to 360)
                        // if (((givenInput.Contains("632147896")) && !((archetypesList[archNum] == "63214") || (archetypesList[archNum] == "214")))){     //Unknown, seems stable (does not detect (63)214 if 360) 
                            // Checking for a 360 before the input is "complete", but after it reads 63214 or 214, causes it to see that as the input
                            
                            //Will update the IDEAL int's if looking at IDEAL archs, but PLAYER if otherwise
                            // Only goes down ONE BRANCH (if or else) PER areArchetypes CALL
                            // This means that ALL the Ideal keys are added first, then ALL the player keys
                            // Will be called archNUMCounter-number times.
                            if (!(dictUpdateStopper1 && dictUpdateStopper2)){

                                if (isInputIdeal){
                                    archCharsDictionaryADD(archetypesList[archNum], "Ideal", archNUMCounter);
                                }
                                else{ //input is player's
                                    archCharsDictionaryADD(archetypesList[archNum], "Player", archNUMCounter);
                                }

                                archNUMCounter += 1;
                                }

                            foundArchetypes.Add(archetypesList[archNum]);
                            input = ""; //reset our string
                            i--; //we need to check the index again*
                        }
                    }
                }
            }
        }

        // Will update the IDEAL int's if looking at IDEAL archs, but PLAYER if otherwise
        // Only one branch is entered per areArchetypes call. After Both calls, both NumOfArchetypesTYPE Count will be updated.
        // Because strings are initialized at start, values are not lost.
        if (isInputIdeal){
            numOfArchetypesIDEAL = foundArchetypes.Count;
            dictUpdateStopper1 = true;
        }
        else{ //input must be Parsed Player Input
            numOfArchetypesPLAYER = foundArchetypes.Count;
            dictUpdateStopper2 = true;
        }

        return foundArchetypes;

    }

        /*
        Helper function.
        Runs after archetypes are valid.
        Checks to make sure at most 2 extra chars are found inbetween steps of the input.
        PARAMS:
        - idealInput: The perfect input the system is expecting
        - parsedPlayerInput: The input from the player. Parsed and cleaned, ready for comparison.
        */
        private bool helperIsInputValid(string idealInput, string parsedPlayerInput){

            char[] idealInput2Chars = idealInput.ToCharArray();

            int indexIdealInput = 0; //the index of the ideal input
            int indexparsedPlayerInput = 0; //the index of the player input

            int countUp = 0;
            int j = 0;

            while ((indexIdealInput < idealInput.Length) && (indexparsedPlayerInput < parsedPlayerInput.Length)){ //iterates until the shorter of the 2 inputs [or both] reaches its end
            
                if (idealInput[indexIdealInput] == parsedPlayerInput[indexparsedPlayerInput]){ // if ideal and player inputs are both same...
                    //increment both
                    indexIdealInput++;
                    indexparsedPlayerInput++;
                }
                else{ //if the player and ideal inputs are NOT identical at any given point

                    if ( (j < parsedPlayerInput.Length) && //shortcircuits to prevent Index OOB error
                            ((idealInput[indexIdealInput] != parsedPlayerInput[j]) || // current chars of each not equal, or...
                            (idealInput[indexIdealInput] != parsedPlayerInput[indexparsedPlayerInput]))){ // if current chars of each ARE equal, its not a mistake
                            
                        j++;
                        countUp++;
                    }

                    if (countUp <= 2){
                        
                        indexparsedPlayerInput++;
                        
                    }
                    else{
                        Debug.Log("FAILURE_CASE: \"Too many additional chars\" check passed 1");
                        FAILURE_CASE = "TOO_MANY_CHARS_1";
                        CONTEXT_1 = parsedPlayerInput[indexparsedPlayerInput]; //The char that caused a difference
                        CONTEXT_2 = idealInput[indexIdealInput]; // The intended char

                        /*For some God forsaken reason thats probably relatively simple, CONTEXT_2 would occasionally
                        take the button (P or K) instead of (what I assume to be) the last numeric char
                        in the idealInput string. The weird part is that this is the IDEAL CONTEXT, despite this entire
                        issue being caused byy errors in the PLAYER input.
                        To prevent this, I just check if it happens, and then do it again with a -1 added to the index.*/
                        if (CONTEXT_2 == 'P' || CONTEXT_2 == 'K'){
                            CONTEXT_2 = idealInput[indexIdealInput - 1];
                        }
                        if (CONTEXT_1 == 'P' || CONTEXT_1 == 'K'){
                            CONTEXT_1 = parsedPlayerInput[indexparsedPlayerInput - 1];
                        }

                        return false; // Too many additional chars
                    }
                }
            }


        countUp = 0;
        j = indexparsedPlayerInput;

        while ( (j < parsedPlayerInput.Length) && //shortcircuits to prevent Index OOB error
                (idealInput[indexIdealInput] != parsedPlayerInput[j])){
            j++; 
            countUp++;
        }

        if (countUp > 2){
            FAILURE_CASE = "TOO_MANY_CHARS_2";
            CONTEXT_1 = parsedPlayerInput[j]; //The char that caused a difference
            
            return false; // Too many additional chars  
        }
        else{ //final check for correct execution button (P or K)
            if (idealInput[^1] == parsedPlayerInput[^1]){
                return true;
            }
            else{
                FAILURE_CASE = "WRONG_BUTTON";
                return false;
            }
        }

    }

    /*
    This function is the single most important one in this entire script.
    It is the entire reason behind this project, but has a rather simple job.
    This function will provide dynamic feedback for the execution of motion inputs.
    Accomplishing this is going to be a nightmare, though.
    PARAMS:
    - inputValidity: the basic yes/no of an inputs correctness. We don't have to give feedback if its correct, right?
    - idealInput: the ideal input we compare against.
    - parsedPlayerInput: the input from the player. Parsed and cleaned, ready for comparison.
    */
    string dynamicFeedbackProvider(bool isInputCorrect, string idealInput, string parsedPlayerInput){

        if (isInputCorrect){ // The happy scenario...
            return "Congratulations! This input is valid and satisfactory, and needs no feedback!";
        }
        else{ // The likely scenario.

            string feedbackString = "";

            if (FAILURE_CASE.Equals("NOT_ENOUGH_CHARGE")){ // should go first
                //player did not charge their charge input enough

                feedbackString = "You did not hold this input's charge for long enough.\n";
                feedbackString += "Keep your stick in the "; 
                feedbackString += direction(IDEAL_INPUT[1].ToString());
                feedbackString += " direction for longer before sending it to the opposite direction.";
                return feedbackString; 
            }
            else if(FAILURE_CASE.Equals("MISSING_ARCHS")){
                //player is missing at least one arch, or may have none at all.

                feedbackString = "Your motion input is missing at least one archetype that would make it valid.\n";

                // Splits archCharsDirctionary into 2 dictionaries: an Ideal dict and a Player dict
                var keysToRemeove = new List<string>();
                foreach (var kvp in archCharsDictionary){
                    if (kvp.Key.Contains("Player")){
                        PlayerArchCharsDictionary.Add(kvp.Key, kvp.Value);
                        keysToRemeove.Add(kvp.Key);
                    }
                }
                foreach (var key in keysToRemeove){
                    archCharsDictionary.Remove(key);
                }

                feedbackString += "The required input is made up of " + (archCharsDictionary.Count / 3) + " archetype(s).\n";

                for (int i = 1; i < (archCharsDictionary.Count / 3) + 1; i++){ // for each arch in the ideal input...

                    if ( // player has an arch
                    PlayerArchCharsDictionary.ContainsKey("PlayerArch" + i + "Start") &&
                    PlayerArchCharsDictionary.ContainsKey("PlayerArch" + i + "Mid") &&
                    PlayerArchCharsDictionary.ContainsKey("PlayerArch" + i + "Last")
                    ){
                        if ( //if the arch is identical to the player's
                        archCharsDictionary["IdealArch" + i + "Start"] == PlayerArchCharsDictionary["PlayerArch" + i + "Start"] &&
                        archCharsDictionary["IdealArch" + i + "Mid"] == PlayerArchCharsDictionary["PlayerArch" + i + "Mid"] &&
                        archCharsDictionary["IdealArch" + i + "Last"] == PlayerArchCharsDictionary["PlayerArch" + i + "Last"])   
                        {
                            feedbackString += "Archetype number " + i + " is correct.\n";
                        }
                        else { // arch is there, but not identical (incorrect)

                            feedbackString += "Archetype number " + i + " was attempted, but is not correct. Rather, it resembles another motion input. ";
                            feedbackString += "Archetype number " + i + " should start with the stick in the ";
                            feedbackString += direction(archCharsDictionary["IdealArch" + i + "Start"]) + " direction "; 
                            feedbackString += "going through the " + direction(archCharsDictionary["IdealArch" + i + "Mid"]) + " direction, ";
                            feedbackString += "and end with it in the " + direction(archCharsDictionary["IdealArch" + i + "Last"]) + " direction.";
                            return feedbackString;
                        }
                    }
                    else { // arch is missing (not complete)
                        feedbackString += "Archetype number " + i + " was either not attempted, or is incorrect.\n";
                        feedbackString += "Archetype number " + i + " should start with the stick in the ";
                        feedbackString += direction(archCharsDictionary["IdealArch" + i + "Start"]) + " direction "; 
                        feedbackString += "going through the " + direction(archCharsDictionary["IdealArch" + i + "Mid"]) + " direction, ";
                        feedbackString += "and end with it in the " + direction(archCharsDictionary["IdealArch" + i + "Last"]) + " direction.";
                        return feedbackString;
                    }
                }

                return feedbackString; // this return is syntactically required, but shouldnt go off
            }
            else if (FAILURE_CASE.Equals("EXTRA_ARCHS")){
                // player has too many archs. Ideal input may have no archs, while the player has 1+.
                feedbackString = "Your motion input is a bit too large, ";
                feedbackString += "to the point where it resembles an entirely diffrent motion input.\n";

                // Splits archCharsDirctionary into 2 dictionaries: an Ideal dict and a Player dict
                var keysToRemeove = new List<string>();
                foreach (var kvp in archCharsDictionary){
                    if (kvp.Key.Contains("Player")){
                        PlayerArchCharsDictionary.Add(kvp.Key, kvp.Value);
                        keysToRemeove.Add(kvp.Key);
                    }
                }
                foreach (var key in keysToRemeove){
                    archCharsDictionary.Remove(key);
                }

                if ((archCharsDictionary.Count / 3) == 0){ // if there are no archetypes in the ideal input
                    feedbackString += "The input you are trying to perfrom has no archetypes,";

                    if (IDEAL_INPUT[0] == '['){ // While unexpected, it is possible a chargeInput would find itself here.
                        feedbackString += " it just requires holding your stick in the " + direction(IDEAL_INPUT[1].ToString()) + " direction for a bit, before moving it to the opposite direction and pressing the attack button.";
                    }
                    else{
                        feedbackString += " it just requires moving your stick in the " + direction(IDEAL_INPUT[0].ToString()) + " direction";
                        if ((IDEAL_INPUT[1] != 'P') || (IDEAL_INPUT[1] != 'K')){
                            feedbackString += ", then to the " + direction(IDEAL_INPUT[^2].ToString()) + " direction, then pressing the attack button.";
                        }
                        else{
                            feedbackString += ", then pressing the attack button.";
                        }
                    }

                    

                    return feedbackString;
                }
                else{ // The ideal input does have archetypes

                    feedbackString += "The required input is made up of " + (archCharsDictionary.Count / 3) + " archetype(s).\n";

                    int correctCounter = 0;

                    for (int i = 1; i < (PlayerArchCharsDictionary.Count / 3) + 1; i++){ // for each arch in the player input...
                        
                        if ( // if the ideal has an arch (will eeventually be false)
                        archCharsDictionary.ContainsKey("IdealArch" + i + "Start") &&
                        archCharsDictionary.ContainsKey("IdealArch" + i + "Mid") &&
                        archCharsDictionary.ContainsKey("IdealArch" + i + "Last"))
                        {

                            if ( //if the arch is identical to the player's
                            archCharsDictionary["IdealArch" + i + "Start"] == PlayerArchCharsDictionary["PlayerArch" + i + "Start"] &&
                            archCharsDictionary["IdealArch" + i + "Mid"] == PlayerArchCharsDictionary["PlayerArch" + i + "Mid"] &&
                            archCharsDictionary["IdealArch" + i + "Last"] == PlayerArchCharsDictionary["PlayerArch" + i + "Last"])   
                            {
                                correctCounter++;

                                feedbackString += "Archetype number " + i + " is correct.\n";
                            }
                            else { // arch is there, but not identical (incorrect)

                                feedbackString += "Archetype number " + i + " was attempted, but is not correct. Rather, it resembles another motion input. ";
                                feedbackString += "Archetype number " + i + " should start with the stick in the ";
                                feedbackString += direction(archCharsDictionary["IdealArch" + i + "Start"]) + " direction "; 
                                feedbackString += "going through the " + direction(archCharsDictionary["IdealArch" + i + "Mid"]) + " direction, ";
                                feedbackString += "and end with it in the " + direction(archCharsDictionary["IdealArch" + i + "Last"]) + " direction.";
                            }

                        }
                        else { // the extra arch
                        if (correctCounter == (archCharsDictionary.Count / 3)){ // all the archs that should be there are correct
                            feedbackString += "Your input was actually completely valid, but then you added an unnecessary archetype onto it.\n";
                            feedbackString += "To fix this, just end your input with the the attack button after your previous stick movement from ";
                            feedbackString += direction(archCharsDictionary["IdealArch" + correctCounter + "Start"]) + " to " + direction(archCharsDictionary["IdealArch" + correctCounter + "Last"]) + ".";
                            return feedbackString;
                        }
                        else {
                            feedbackString += "\nIn addition to the previously mentioned mistakes, there is also an unnecessary archetype that was added to your input. ";
                            feedbackString += "You should focus on fixing those mistakes first.";
                            return feedbackString;
                        }

                        }
                    }
                    return feedbackString;
                }

            }
            else if (FAILURE_CASE.Equals("WRONG_ARCH")){
                //Same number of archs are detected, but they are not the same.
                feedbackString = "Your motion input resembles another motion input too closely (or just is a different motion input). ";
                feedbackString += "The input that yours resembles deviates at the ";

                    // splits into 2 dicts: 1 of IDEALs and 1 of PLAYERs
                    int idealPlayerSplitIndex = archCharsDictionary.Count / 2;
                    var PlayerArchCharsDictionary = archCharsDictionary.Skip(idealPlayerSplitIndex).ToDictionary(kv => kv.Key, kv => kv.Value);
                    var keysToRemeove = archCharsDictionary.Keys.Skip(idealPlayerSplitIndex).ToList();
                    foreach (var key in keysToRemeove){
                        archCharsDictionary.Remove(key);
                    }

                    // lengths should be exact same in both dicts
                    for (int i = 0; i < archCharsDictionary.Count; i++){

                        string key1 = archCharsDictionary.Keys.ElementAt(i); //ideal
                        string key2 = PlayerArchCharsDictionary.Keys.ElementAt(i); //player
                        // keys are the long index names

                        int value1 = int.Parse(archCharsDictionary[key1]); //ideal
                        int value2 = int.Parse(PlayerArchCharsDictionary[key2]); //player
                        // values are the directions/chars stored in the dictionary keys

                        if (value1 != value2){ //check for difference, similarity is irrelevant
                            if (key1.Contains("Start") && key2.Contains("Start")){ // start char of arch is wrong
                                feedbackString += "start.\n";
                                feedbackString += "This probably means you mistakenly did an input that started in a different direction that was not the ";
                                if ((IDEAL_INPUT == "632147896K" || IDEAL_INPUT == "632146P") && value2 == 2){ 
                                    value2 = 6;
                                }
                                /*Hard coding to solve a singular issue related to the input system, discovered very
                                late in development. While Hard Coding is not a practice I prefer, a complete rebuilding
                                of the input system is just not possible in a timely manner. I think it is very unlikely that
                                this hard-fix causes any other adverse effects, asside from serving as a bandaid-fix on a problem
                                observed in testing.*/
                                feedbackString += direction(value2.ToString());
                                feedbackString += " direction.";
                                return feedbackString;
                            }
                            else if (key1.Contains("Mid") && key2.Contains("Mid")){ // middle char of arch is wrong
                                feedbackString += "middle.\n";
                                feedbackString += "This probably means you started out the input in the ";
                                feedbackString += direction(IDEAL_INPUT[0].ToString());
                                feedbackString += " direction like you were supposed to, but then went off in a different direction by accident.";
                                return feedbackString;
                            }
                            else if (key1.Contains("Last") && key2.Contains("Last")){ // end char of arch is wrong
                                feedbackString += "end.\n";
                                feedbackString += "This probably means you somehow performed an input that had the same start and middle as the one you were supposed to perform, but had a different end.";
                                feedbackString += "If this is the case, know that your stick should end in the ";
                                feedbackString += direction(value1.ToString()); //ideal
                                feedbackString += " direction, as opposed to the ";
                                feedbackString += direction(value2.ToString()); //player
                                feedbackString += " direction.";
                                return feedbackString;
                            }
                            else{ // should not ever occur
                                Debug.Log("Debug info: ");
                                Debug.Log("key1: " + key1);
                                Debug.Log("key2: " + key2);
                                Debug.Log("value1: " + value1);
                                Debug.Log("value2: " + value2);
                                return "UNIDENTIDFIED ERROR 1, SHOULD NOT OCCUR";
                            }
                        }
                        
                    }

                    return "Unidentified Error 2, should not occur"; //only occurs if entire for-loop is skipped
                    // really, this shouldnt occur at all.
            }
            else if (FAILURE_CASE.Equals("TOO_MANY_CHARS_1")){ // can occur from start until end between each char
                //In this case, archetypes are all perfect, but problems between them:
                // may occur before first input, or between others, but not last.
                feedbackString = "Your motion input has all of the archetypes, ";
                feedbackString += "but makes a small mistake inbetween two of the directions in the input. ";
                feedbackString += "\nAt some point in the input, your stick went too far ";
                
                Debug.Log("Before CONTEXT_2 is PARSED: " + CONTEXT_2);
                Debug.Log("Before CONTEXT_1 is PARSED: " + CONTEXT_1);
                int IDEAL_LAST = int.Parse(CONTEXT_2.ToString());
                int PLAYER_LAST = int.Parse(CONTEXT_1.ToString());
                /*Converting from a char to an int will result in a conversion
                from a char to that char's "unicode" ID, rather than an int, EVEN 
                if that char happens to itself, be an int.*/
                Debug.Log("After CONTEXT_2 is PARSED: " + CONTEXT_2);
                Debug.Log("After CONTEXT_1 is PARSED: " + CONTEXT_1);

                if (PLAYER_LAST < IDEAL_LAST){ // stick position lower than expected
                     feedbackString += "down.";
                }
                else{ // stick position higher than expected
                    feedbackString += "up.";
                }
                feedbackString += "\nTry to keep the stick more ";
                feedbackString += direction(IDEAL_LAST.ToString());
                feedbackString += ".";
                return feedbackString; 

            }
            else if (FAILURE_CASE.Equals("TOO_MANY_CHARS_2")){ //Occurs at very end of input
                // In this case, archetypes are all perfect. can only occur if failure is between last arch and correct button press
                feedbackString = "Your input is almost entirely correct, ";
                feedbackString += "but you made a mistake on the last step of the input before the button press.";
                feedbackString += "\nTowards the end of the input, the stick was too far ";
                int IDEAL_LAST = (int)IDEAL_INPUT[^2]; // The last NUMERIC character in the IDEAL (this isnt actually the last char, as that would be P or K)
                int PLAYER_LAST = (int)CONTEXT_1;
                
                if (PLAYER_LAST < IDEAL_LAST){ // lower than expected
                    feedbackString += "down.";
                }
                else{ // higher than expected
                    feedbackString += "up.";
                }
                feedbackString += "\nTry to keep the stick more ";
                feedbackString += direction(IDEAL_LAST.ToString());
                feedbackString += ".";
                return feedbackString; 
            }
            else{ //WRONG_BUTTON
                feedbackString = "All of your motions were correct, but you pressed the";
                if (IDEAL_INPUT[^1] == 'P'){
                    feedbackString += " K button instead of P.";
                    return feedbackString; 
                }
                else{ //Ideal input must end with K
                    feedbackString += " P button instead of K.";
                    return feedbackString; 
                }
            }
        }
    }

    /*
    Helper function that allows us to turn a number into a description of its direction.
    Might be helpful with information hiding.
    Takes a string parameter because ints have been behaving oddly,
    often representing themselves as their Decimal equivelents (4 -> 52).
    PARAMS:
    - num: a number in an input, meant to signify a direction. Is a String due to (See above)
    */
    private string direction(string num){
        // Debug.Log("num: " + num);
        if (num == "6"){ // >
           return "forward (6)";
        }
        else if (num == "4"){ // <
            return "back (4)";
        }
        else if(num == "2"){ // v
            return "down (2)";
        }
        else if (num == "3"){ // v>
            return "down-forward (3)";
        }
        else if (num == "1"){ // <v
            return "down-back (1)";
        } //less common cases
        else if (num == "8"){ // ^
            return "up (8)";
        }
        else if(num == "7"){ // <^
            return "up-back (7)";
        }
        else if (num == "9"){ // ^>
            return "up-forward (9)";
        } 
        else if (num == "5"){
            return "neutral (5)";
        }
        else{ // Impossible case
            return "NULL ERROR";
        }
    }

    /*
    Helper function to store contextual information about strings in a dictionary.
    PARAMS:
    - givenArch: the archetype.
    - IDEALorPLAYER: string thats either "Ideal" or "Player".
    - archNUM: the archetype number. Realistically, will never be more than 3.
    This combination of variables should ensure the key is always unique.
    */
    private void archCharsDictionaryADD(string givenArch, string IDEALorPLAYER, int archNUM){

        string dictionaryKey = IDEALorPLAYER; //type
        dictionaryKey += "Arch"; //typeArch
        dictionaryKey += archNUM.ToString(); //typeArch#
        dictionaryKey += "Start"; //typeArch#PosF
        Debug.Log("Adding Key: " + dictionaryKey + ". Adding Value: " + givenArch[0].ToString());
        archCharsDictionary.Add(dictionaryKey, givenArch[0].ToString());

        dictionaryKey = IDEALorPLAYER; //type
        dictionaryKey += "Arch"; //typeArch
        dictionaryKey += archNUM.ToString(); //typeArch#
        dictionaryKey += "Mid"; 
        Debug.Log("Adding Key: " + dictionaryKey + ". Adding Value: " + givenArch[givenArch.Length / 2].ToString());
        archCharsDictionary.Add(dictionaryKey, givenArch[givenArch.Length / 2].ToString());

        dictionaryKey = IDEALorPLAYER; //type
        dictionaryKey += "Arch"; //typeArch
        dictionaryKey += archNUM.ToString(); //typeArch#
        dictionaryKey += "Last"; 
        Debug.Log("Adding Key: " + dictionaryKey + ". Adding Value: " + givenArch[^1].ToString());
        archCharsDictionary.Add(dictionaryKey, givenArch[^1].ToString());

        /*
        The order of keys in the dictionary goes as follows:
        Type-Archnum-position
            All the ideals will be first. 
            For each ideal  , the archNums are ordered 1->?. 
            For each archNum, the order is start->middle->end.
            After all thee ideals, the pattern repeats for players.
        */

    }

    /*
    Displays UI elements, like feedback or directional inputs.
    PARAMS:
    - feedback: a string representing the feedback generated by dynamicFeedbackProvider
    */
    void displayUIElements(string feedback){

        /* Ok so this is just absurd. I NEED to log this:
           So the line "feedbackText.text = feedback;" causes a NullReferenceException.
           I have no idea why, and hours of searching leads me to no sensical conclusion.
           For some INSANE reason, putting the line in this basic if-statement solves the problem.

           To elaborate: 
           Checking if it's not Null (it isn't) suddenly makes it not Null. What?
        */
        if (feedbackText != null && gameObject.CompareTag("feedbackText")){
            feedbackText.text = feedback;
        }
        if (textBox != null && gameObject.CompareTag("TextBox")){
            textBox.enabled = true;
        }
    }
    /*
    Updates the displayed trial-text.
    PARAMS:
    - isInputCorrect: Is this input correct, or does it need feedback?
    */
    void handlePostTrialIndexing(bool isInputCorrect, string input){

        //Practice mode essentially disables this function
        if (!practiceMode){

            if (gameObject.CompareTag("Player")){ // writes to file 5 times if not singularly tagged
                writeResultsToFile(attemptIndex, IDEAL_INPUT);  // Trial# (if first attempt, puts IDEAL on sheet)
                writeResultsToFile(isInputCorrect, input);      // result/input (charts if input is correct, then what your input was)
            }

            attemptIndex++; // your attemptNo goes up by 1

            if (attemptIndex > 5){ // if this number goes above 5...
                attemptIndex = 1;   // resets the number back to 1
                trialIndex++;       //"brings" you to the next trial
                if (gameObject.CompareTag("Player")){ // writes to file 5 times if not singularly tagged
                    writeResultsToFile(true); // newline  
                }

                if (trialIndex < 10){ // as long as you are not done with trials...
                    IDEAL_INPUT = trialInputList[trialIndex]; //the IDEAL_INPUT is updated to the next one
                }
                practiceMode = true;
            }

            if (trialIndex < 10){ // if you are not done with trials...
                
                // Because we set practiceMode mid-function, we must check for it again.
                // The only part that really changes though is the text, we still update the visRep.
                if (practiceMode){ 
                    defaultTrialText = "Current Goal: " + IDEAL_INPUT + "          Practice Mode";
                }
                else{
                    defaultTrialText = "Current Goal: " + IDEAL_INPUT + "          Attempt " + attemptIndex + "/5";
                }
                if (gameObject.CompareTag("trialVis")){
                    ShowTrialReps(trialIndex); // update graphical representations
                }  
                
            }
            else{
                if (gameObject.CompareTag("Player")){ // writes to file 5 times if not singularly tagged
                    writeResultsToFile(true); // newline   
                }
                defaultTrialText = "Your trial is now complete. Thank you for your time.";
                killSwitch = true;
            }
        }
        else{ // A few things change if we are in Practice Mode:
            defaultTrialText = "Current Goal: " + IDEAL_INPUT + "          Practice Mode";
        }
    }

    void writeResultsToFile(bool isInputCorrect, string input){ //result/input variant

        string filePath = Path.Combine(Application.dataPath, "feedback.txt");

        File.AppendAllText(filePath, ",");

        if (isInputCorrect){
            File.AppendAllText(filePath, "1,"); //TRUE
        }
        else{
            File.AppendAllText(filePath, "0,"); //FALSE
        }

        File.AppendAllText(filePath, input + ",");

    }

    void writeResultsToFile(int attemptNo, string Trial){ // Trial# variant
        string filePath = Path.Combine(Application.dataPath, "feedback.txt");

        if (attemptNo == 1){
            File.AppendAllText(filePath, Trial + ",");
        }
    }

    void writeResultsToFile(bool newLine){ // newline variant

        string filePath = Path.Combine(Application.dataPath, "feedback.txt");

        File.AppendAllText(filePath, "\n");
    }

    void writeResultsToFile(){ //intro (heading) variant
        string filePath = Path.Combine(Application.dataPath, "feedback.txt");
         File.AppendAllText(filePath, "Trial,Attempt 1,Success?,Player Input,Attempt 2,Success?,Player Input,Attempt 3,Success?,Player Input,Attempt 4,Success?,Player Input,Attempt 5,Success?,Player Input\n");
    }

    /*
    Detects if the player is crouched.
    PARAMS:
    - direction: the current numeric annotation of the player (1, 2, or 3).
    */
    bool isPlayerCrouched(int direction){
        //1-3 signify a crouch (if not midair)
        if (isGrounded && (direction == 1 || direction == 2 || direction == 3)){
            return true;
        }
        else{
            return false;
        }
    }

//------------------###ANIMATION and IMAGE ALTERING---------------------------------------------------------

    /*
    Animates the player's basic movement
    PARAMS:
    - directionalInput: The current hold direction of the control stick
    */
    void AnimatePlayerMovement(int directionalInput){ //moves not yet implemented
        //Player starts in idle
        int NumericAnnotation = directionalInput;
        if(NumericAnnotation == 6){ //Player "moves" right
        //should probably be replaced with a call similar to isPlayerCrouched
            animator.SetBool(walkAnimation, true);
            animator.SetBool(crouchAnimation, false);
        }
        else if (isPlayerCrouched(NumericAnnotation)){ //crouch
            animator.SetBool(crouchAnimation, true);
            animator.SetBool(walkAnimation, false);
        }
        else{ //idle
            animator.SetBool(walkAnimation, false);
            animator.SetBool(crouchAnimation, false);
        }
    }

    /*
    Animates the player's basic attacks
    PARAMS:
    - buttonInput: the currently pressed button 
    */
    void AnimatePlayerBasicAttacks(string buttonInput){
        string attack = buttonInput;
        if (attack == "P"){
            animator.SetBool(punchAnimation, true);
            animator.SetBool(kickAnimation, false);
            animator.SetBool(crouchAnimation, false);
        }
        else if (attack == "K"){
            animator.SetBool(punchAnimation, false);
            animator.SetBool(kickAnimation, true);
            animator.SetBool(crouchAnimation, false);
        }
        else{
            animator.SetBool(punchAnimation, false);
            animator.SetBool(kickAnimation, false);
        }
    }

    /*
    Animates the player's special attacks
    PARAMS:
    - isInputValid: is the input correct?
    - idealInput: the ideal input
    */
    void AnimatePlayerSpecials(bool isInputValid, string idealInput){
        if(isInputValid && !(animationLockout)){
            if(idealInput[^1] == 'P'){ // attack is a punch
                if (gameObject.CompareTag("Player")){ 
                    // An unfortunate byproduct of how I coded this project is that theres only one script.
                    // More specifically, that one script is applied to more than one different elements.
                    // This if-statement is to prevent the tranformation from applying to other non-player elements.
                    transform.position += new Vector3(0f, 4f, 0f);   
                }
                animator.SetBool(DPanimation, true);
            }
            else{ // Attack is a kick
                animator.SetBool(TatsuAnimation, true);
            }
            animationLockout = true;
        }
    }

    /*
    Animates the onscreen representation of the joystick
    PARAMS:
     directionalInput: the current hold direction of the control stick
    */
    void AnimateStick(int directionalInput){
        if (directionalInput == 5){ // x
            stickUI.sprite = stickSprites[0];
        }
        else if (directionalInput == 6){ // >
            stickUI.sprite = stickSprites[1];
        }
        else if (directionalInput == 4){ // <
            stickUI.sprite = stickSprites[2];
        }
        else if(directionalInput == 2){ // v
            stickUI.sprite = stickSprites[4];
        }
        else if (directionalInput == 3){ // v>
            stickUI.sprite = stickSprites[6];
        }
        else if (directionalInput == 1){ // <v
            stickUI.sprite = stickSprites[8];
        } //less common cases
        else if (directionalInput == 8){ // ^
            stickUI.sprite = stickSprites[3];
        }
        else if(directionalInput == 7){ // <^
            stickUI.sprite = stickSprites[7];
        }
        else if (directionalInput == 9){ // ^>
            stickUI.sprite = stickSprites[5];
        }
        else{ // Impossible case
           stickUI.sprite = stickSprites[0];
        }
    }

    void ShowTrialReps(int trialNumber){
        if (trialNumber == 0){ // Trial 1
            trialRepresentation.sprite = trialReps[trialNumber];
        }
        else if (trialNumber == 1){ // Trial 2
            trialRepresentation.sprite = trialReps[trialNumber];
        }
        else if (trialNumber == 2){ // Trial 3
            trialRepresentation.sprite = trialReps[trialNumber];
        }
        else if(trialNumber == 3){ // Trial 4
            trialRepresentation.sprite = trialReps[trialNumber];
        }
        else if (trialNumber == 4){ // Trial 5
            trialRepresentation.sprite = trialReps[trialNumber];
        }
        else if (trialNumber == 5){ // Trial 6
            trialRepresentation.sprite = trialReps[trialNumber];
        } 
        else if (trialNumber == 6){ // Trial 7
            trialRepresentation.sprite = trialReps[trialNumber];
        }
        else if(trialNumber == 7){ // Trial 8
            trialRepresentation.sprite = trialReps[trialNumber];
        }
        else if (trialNumber == 8){ // Trial 9
            trialRepresentation.sprite = trialReps[trialNumber];
        }
        else if(trialNumber == 9){ // Trial 10
            trialRepresentation.sprite = trialReps[trialNumber];
        }
        else{ // Impossible case
           trialRepresentation.sprite = trialReps[trialNumber];
        }
    }

    // All-purpose function dealing with the context-dependant textbox thats controlled by a buttton press
    void updateContextBox(bool enabled, string variant){
        if (!enabled){ //deactivates 
            if (NextTextBox != null && gameObject.CompareTag("NextBox")){
                NextTextBox.enabled = false;
            }
            if (NextText != null && gameObject.CompareTag("NextText")){
                NextText.text = "";
            }
            defaultTrialText = "Current Goal: " + IDEAL_INPUT + "          Attempt " + attemptIndex + "/5";
        }
        else{
            if (variant == "Advance"){ //lets the player know they can advance to the next attempt
                if (NextTextBox != null && gameObject.CompareTag("NextBox")){
                    NextTextBox.enabled = true;
                }
                if (NextText != null && gameObject.CompareTag("NextText")){
                    Debug.Log("TestTest");
                    NextText.text = "Thank you for waiting. You may now press the NEXT button at any time to advance.";
                }
            }
            else if (variant == "Start Trial"){ //lets the player know they can start their attempts (after practice)
                if (NextTextBox != null && gameObject.CompareTag("NextBox")){
                    NextTextBox.enabled = true;
                }
                if (NextText != null && gameObject.CompareTag("NextText")){
                    NextText.text = "Any time you wish, you can press the NEXT button to exit practice mode and begin your attempts.\nPlease wait 1 second between practice attempts.";
                }
            }
            else{ // not intended for use
                NextText.text = "DEBUG";
            }
        }
    }

    /*
    Exists to prevent the player from falling through the floor.
    */
    private void OnCollisionEnter2D(Collision2D collision){
        if (collision.gameObject.CompareTag(groundTag)){
            isGrounded = true;
            // Debug.Log("we have touched ground");
        }
    }
}
