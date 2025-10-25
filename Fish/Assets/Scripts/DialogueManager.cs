// ---- Place this at the top of your DialogueManager.cs file ----

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Defines which character is speaking.
/// </summary>
public enum DialogueCharacter
{
    TK,
    Mafia
}

/// <summary>
/// This is your "dialogue line object". It holds the data for a single line.
/// [System.Serializable] lets you edit this in the Inspector.
/// </summary>
[System.Serializable]
public class DialogueLine
{
    public DialogueCharacter character;

    [TextArea(3, 10)] // Makes the text box bigger in the Inspector
    public string text;
}

public class DialogueManager : MonoBehaviour
{
    [Header("Character UI")]
    public TextMeshProUGUI tkText;
    public GameObject tkBackground;

    public TextMeshProUGUI mafiaText;
    public GameObject mafiaBackground;

    [Header("Dialogue")]
    public DialogueLine[] dialogueLines; // An array of all dialogue for this scene
    public float typingSpeed = 0.05f;

    [Header("Scene Transition")] 
    public string nextSceneName;

    private int currentLineIndex = 0;
    private bool isTyping = false;
    private Coroutine typingCoroutine;

    void Start()
    {
        // Start the dialogue as soon as the scene loads
        StartDialogue();
    }

    void Update()
    {
        // Listen for a click
        if (Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                // If typing, click skips to the end of the line
                SkipTyping();
            }
            else
            {
                // If not typing, click moves to the next line
                NextLine();
            }
        }
    }

    /// <summary>
    /// Hides all UI and starts the first line of dialogue.
    /// </summary>
    public void StartDialogue()
    {
        currentLineIndex = 0;
        // Hide both characters' UI to start
        SetActiveCharacter(DialogueCharacter.TK, false);
        SetActiveCharacter(DialogueCharacter.Mafia, false);

        if (dialogueLines.Length > 0)
        {
            DisplayLine(dialogueLines[currentLineIndex]);
        }
    }

    /// <summary>
    /// Displays the correct UI and starts the typewriter effect.
    /// </summary>
    private void DisplayLine(DialogueLine line)
    {
        // Determine who is speaking
        DialogueCharacter speaker = line.character;
        DialogueCharacter listener = (speaker == DialogueCharacter.TK) ? DialogueCharacter.Mafia : DialogueCharacter.TK;

        // Show the speaker, hide the listener
        SetActiveCharacter(speaker, true);
        SetActiveCharacter(listener, false);

        // Get the correct text box and start typing
        TextMeshProUGUI activeTextBox = (speaker == DialogueCharacter.TK) ? tkText : mafiaText;
        typingCoroutine = StartCoroutine(TypeSentence(activeTextBox, line.text));
    }

    /// <summary>
    /// The typewriter effect coroutine.
    /// </summary>
    private IEnumerator TypeSentence(TextMeshProUGUI textBox, string sentence)
    {
        isTyping = true;
        textBox.text = ""; // Clear the text

        foreach (char letter in sentence.ToCharArray())
        {
            textBox.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
    }

    /// <summary>
    /// Stops the typewriter coroutine and fills the text box instantly.
    /// </summary>
    private void SkipTyping()
    {
        if (isTyping)
        {
            StopCoroutine(typingCoroutine);
            isTyping = false;

            // Get the current line's full text and display it
            DialogueLine line = dialogueLines[currentLineIndex];
            TextMeshProUGUI activeTextBox = (line.character == DialogueCharacter.TK) ? tkText : mafiaText;
            activeTextBox.text = line.text;
        }
    }

    /// <summary>
    /// Moves to the next line in the dialogue array.
    /// </summary>
    private void NextLine()
    {
        currentLineIndex++; // Move to the next index

        if (currentLineIndex < dialogueLines.Length)
        {
            // If there are more lines, display the next one
            DisplayLine(dialogueLines[currentLineIndex]);
        }
        else
        {
            // If we're at the end, end the dialogue
            EndDialogue();
        }
    }

    /// <summary>
    /// Hides all dialogue UI.
    /// </summary>
    private void EndDialogue()
    {
        // Hide both characters
        SetActiveCharacter(DialogueCharacter.TK, false);
        SetActiveCharacter(DialogueCharacter.Mafia, false);

        Debug.Log("Dialogue ended.");

        // --- 4. THIS IS THE NEW LOGIC ---
        // Check if a scene name has actually been provided
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            // Load the next scene
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            // Log a warning if no scene was set, so you can debug easily
            Debug.LogWarning("End of dialogue, but no 'nextSceneName' was set in the DialogueManager.");
        }
    }

    /// <summary>
    /// A helper function to easily show/hide a character's UI.
    /// </summary>
    private void SetActiveCharacter(DialogueCharacter character, bool isActive)
    {
        if (character == DialogueCharacter.TK)
        {
            tkText.gameObject.SetActive(isActive);
            tkBackground.gameObject.SetActive(isActive);
        }
        else // It's Mafia
        {
            mafiaText.gameObject.SetActive(isActive);
            mafiaBackground.gameObject.SetActive(isActive);
        }
    }
}