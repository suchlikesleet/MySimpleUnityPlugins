using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyGame.Plugins.MyCustom
{
    public class FloteNote : EditorWindow
    {
        private string noteContent = ""; // The content of the floating notepad
        private string saveFilePath = ""; // Path to save the file
        private string customTitle = "";  // Custom title for the floating notepad
        private bool isRenaming = false;  // Tracks whether the title is being renamed
        private Color backgroundColor = new Color(0.9f, 0.9f, 0.9f); // Light gray background color
        private Color textColor = Color.black; // Default black text color

        // To create a unique identifier for each window
        private static int noteCounter = 0;

        [MenuItem("Window/Floating FloteNote")]
        public static void ShowWindow()
        {
            // Create a new instance of the floating FloteNote window
            FloteNote window = CreateInstance<FloteNote>();

            // Increment the note counter to ensure each note has a unique identifier
            noteCounter++;

            // Set default custom title
            window.customTitle = $"FloteNote {noteCounter}";

            // Show it as a floating utility window that remains on top
            window.titleContent = new GUIContent(window.customTitle);
            window.ShowUtility();
        }

        private void OnEnable()
        {
            // Start with a default save path if no file is loaded
            if (string.IsNullOrEmpty(saveFilePath))
            {
                saveFilePath = $"Assets/Scripts/Editor/FloteNote_{noteCounter}.txt";
            }
        }

        private void OnDisable()
        {
            // Automatically save notes when the window is closed
            SaveNotes();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);

            // Check if the user is in renaming mode
            if (isRenaming)
            {
                // If renaming, show an editable text field for the title
                GUI.SetNextControlName("RenameField");
                customTitle = EditorGUILayout.TextField(customTitle);

                // Focus the text field when renaming starts
                EditorGUI.FocusTextInControl("RenameField");

                // If the user presses Enter or clicks outside, finish renaming
                if (Event.current.isKey && Event.current.keyCode == KeyCode.Return)
                {
                    EndRename();
                }
            }
            else
            {
                // Display the title as a label
                GUILayout.Label(customTitle, EditorStyles.boldLabel);

                // If the user clicks the title label, start renaming
                var lastRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.MouseDown && lastRect.Contains(Event.current.mousePosition))
                {
                    StartRename();
                }
            }

            GUILayout.Space(10);

            // Create a custom GUIStyle for the text area
            GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea);
            textAreaStyle.normal.background = MakeTex(2, 2, backgroundColor); // Set custom background color
            textAreaStyle.normal.textColor = textColor; // Set custom text color
            textAreaStyle.fontSize = 14; // Optional: set a custom font size

            // Text area for writing notes with the custom style
            noteContent = EditorGUILayout.TextArea(noteContent, textAreaStyle, GUILayout.Height(400));

            GUILayout.Space(10);

            // Button to save the notes
            if (GUILayout.Button("Save Notes"))
            {
                SaveNotes();
            }

            // Button to clear the text area
            if (GUILayout.Button("Clear Notes"))
            {
                noteContent = "";
            }

            // Button to load notes from a specific file
            if (GUILayout.Button("Load Notes"))
            {
                LoadNotesFromFile();
            }

            // Optional: Color pickers to change text and background color in the editor
            backgroundColor = EditorGUILayout.ColorField("Background Color", backgroundColor);
            textColor = EditorGUILayout.ColorField("Text Color", textColor);
        }

        private void StartRename()
        {
            isRenaming = true;
        }

        private void EndRename()
        {
            isRenaming = false;
            titleContent = new GUIContent(customTitle);  // Update window title
            UpdateSaveFilePath(); // Update the save file path with the new title
            GUI.FocusControl(null); // Remove focus from the text field
        }

        private void SaveNotes()
        {
            try
            {
                UpdateSaveFilePath(); // Ensure the file path matches the title
                File.WriteAllText(saveFilePath, noteContent);
                AssetDatabase.Refresh();
                Debug.Log($"Notes saved to {saveFilePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save notes: {e.Message}");
            }
        }

        private void LoadNotesFromFile()
        {
            string path = EditorUtility.OpenFilePanel("Load FloteNote", "Assets/Scripts/Editor", "txt");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    // Load notes from the selected file
                    noteContent = File.ReadAllText(path);

                    // Extract file name and update the title (e.g., todo.txt -> todo)
                    customTitle = Path.GetFileNameWithoutExtension(path);
                    titleContent = new GUIContent(customTitle);

                    // Update the save path to match the loaded file
                    saveFilePath = path;
                    Debug.Log($"Loaded notes from {saveFilePath}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to load notes: {e.Message}");
                }
            }
        }

        private void UpdateSaveFilePath()
        {
            // Save file should match the custom title (e.g., todo -> todo.txt)
            saveFilePath = $"Assets/Scripts/Editor/{customTitle}.txt";
        }

        // Helper method to create a texture for the background color
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
