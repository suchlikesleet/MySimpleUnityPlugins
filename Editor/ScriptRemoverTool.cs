using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

namespace MyGame.Plugins
{

public class ScriptRemoverTool : EditorWindow
{
    private Vector2 scrollPosition;
    private int selectedTab = 0;
    private string[] tabs = { "Remove Scripts", "Rename Scripts" };

    // Remove Scripts Tab
    private List<MonoScript> selectedScriptsToRemove = new List<MonoScript>();
    private bool showRemovePreview = false;
    private List<GameObject> affectedObjects = new List<GameObject>();
    private Dictionary<GameObject, List<MonoBehaviour>> componentsToRemove = new Dictionary<GameObject, List<MonoBehaviour>>();

    // Rename Scripts Tab
    private List<MonoScript> selectedScriptsToRename = new List<MonoScript>();
    private bool usePrefix = false;
    private string prefix = "";
    private bool useSuffix = false;
    private string suffix = "";
    private bool useNamespace = false;
    private string newNamespace = "";
    private bool keepOriginalCopy = false;
    private bool showRenamePreview = false;
    private List<RenameOperation> renameOperations = new List<RenameOperation>();
    private bool updateReferences = true;

    [System.Serializable]
    public class RenameOperation
    {
        public MonoScript script;
        public string oldClassName;
        public string newClassName;
        public string oldFileName;
        public string newFileName;
        public string filePath;
        public string oldNamespace;
        public string newNamespace;
        public bool namespaceChanged;
        public List<string> referencingFiles = new List<string>();
    }

    [MenuItem("Tools/Script Manager")]
    public static void ShowWindow()
    {
        GetWindow<ScriptRemoverTool>("Script Manager");
    }

    private void OnGUI()
    {
        GUILayout.Label("Unity Script Manager", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // Tab selection
        selectedTab = GUILayout.Toolbar(selectedTab, tabs);
        GUILayout.Space(10);

        switch (selectedTab)
        {
            case 0:
                DrawRemoveScriptsTab();
                break;
            case 1:
                DrawRenameScriptsTab();
                break;
        }
    }

    #region Remove Scripts Tab

    private void DrawRemoveScriptsTab()
    {
        GUILayout.Label("Remove Selected Scripts from Scene", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // Script selection area
        GUILayout.Label("Select Scripts to Remove:", EditorStyles.label);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
        
        for (int i = 0; i < selectedScriptsToRemove.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            selectedScriptsToRemove[i] = (MonoScript)EditorGUILayout.ObjectField(selectedScriptsToRemove[i], typeof(MonoScript), false);
            
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                selectedScriptsToRemove.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();

        // Add script button
        if (GUILayout.Button("Add Script Slot"))
        {
            selectedScriptsToRemove.Add(null);
        }

        GUILayout.Space(10);

        // Preview button
        if (GUILayout.Button("Preview Changes"))
        {
            PreviewRemoveChanges();
            showRemovePreview = true;
        }

        // Preview results
        if (showRemovePreview && componentsToRemove.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("Preview - Components to be removed:", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var kvp in componentsToRemove)
            {
                EditorGUILayout.LabelField($"GameObject: {kvp.Key.name}", EditorStyles.boldLabel);
                foreach (var component in kvp.Value)
                {
                    EditorGUILayout.LabelField($"  - {component.GetType().Name}", EditorStyles.miniLabel);
                }
                GUILayout.Space(5);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            
            // Warning
            EditorGUILayout.HelpBox($"This will remove {GetTotalComponentCount()} component(s) from {componentsToRemove.Count} GameObject(s). This action cannot be undone!", MessageType.Warning);
            
            GUILayout.Space(5);
            
            // Remove button
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("REMOVE COMPONENTS", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Confirm Removal", 
                    $"Are you sure you want to remove {GetTotalComponentCount()} component(s) from {componentsToRemove.Count} GameObject(s)?\n\nThis action cannot be undone!", 
                    "Remove", "Cancel"))
                {
                    RemoveComponents();
                }
            }
            GUI.backgroundColor = Color.white;
        }
        else if (showRemovePreview)
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("No matching components found in the current scene.", MessageType.Info);
        }

        GUILayout.Space(10);
        
        // Instructions
        EditorGUILayout.HelpBox("Instructions:\n1. Add script slots using 'Add Script Slot'\n2. Drag MonoScript assets into the slots\n3. Click 'Preview Changes' to see what will be removed\n4. Click 'REMOVE COMPONENTS' to execute", MessageType.Info);
    }

    #endregion

    #region Rename Scripts Tab

    private void DrawRenameScriptsTab()
    {
        GUILayout.Label("Rename Scripts with Prefix/Suffix/Namespace", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // Prefix option
        EditorGUILayout.BeginHorizontal();
        usePrefix = EditorGUILayout.Toggle(usePrefix, GUILayout.Width(20));
        GUI.enabled = usePrefix;
        GUILayout.Label("Prefix:", GUILayout.Width(50));
        prefix = EditorGUILayout.TextField(prefix);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        // Suffix option
        EditorGUILayout.BeginHorizontal();
        useSuffix = EditorGUILayout.Toggle(useSuffix, GUILayout.Width(20));
        GUI.enabled = useSuffix;
        GUILayout.Label("Suffix:", GUILayout.Width(50));
        suffix = EditorGUILayout.TextField(suffix);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Namespace option
        EditorGUILayout.BeginHorizontal();
        useNamespace = EditorGUILayout.Toggle(useNamespace, GUILayout.Width(20));
        GUI.enabled = useNamespace;
        GUILayout.Label("Namespace:", GUILayout.Width(70));
        newNamespace = EditorGUILayout.TextField(newNamespace);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (useNamespace)
        {
            EditorGUILayout.HelpBox("Will replace existing namespace or add new one if none exists", MessageType.Info);
        }

        GUILayout.Space(5);

        keepOriginalCopy = EditorGUILayout.Toggle("Keep Original Copy", keepOriginalCopy);
        if (keepOriginalCopy)
        {
            EditorGUILayout.HelpBox("Original scripts will be duplicated with '_Original' suffix before renaming", MessageType.Info);
        }

        updateReferences = EditorGUILayout.Toggle("Update References in Other Scripts", updateReferences);

        GUILayout.Space(10);

        // Script selection area
        GUILayout.Label("Select Scripts to Rename:", EditorStyles.label);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
        
        for (int i = 0; i < selectedScriptsToRename.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            selectedScriptsToRename[i] = (MonoScript)EditorGUILayout.ObjectField(selectedScriptsToRename[i], typeof(MonoScript), false);
            
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                selectedScriptsToRename.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();

        // Add script button
        if (GUILayout.Button("Add Script Slot"))
        {
            selectedScriptsToRename.Add(null);
        }

        GUILayout.Space(10);

        // Preview button
        if (GUILayout.Button("Preview Rename Operations"))
        {
            PreviewRenameOperations();
            showRenamePreview = true;
        }

        // Preview results
        if (showRenamePreview && renameOperations.Count > 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("Preview - Rename Operations:", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var operation in renameOperations)
            {
                EditorGUILayout.LabelField($"File: {operation.oldFileName} → {operation.newFileName}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Class: {operation.oldClassName} → {operation.newClassName}", EditorStyles.miniLabel);
                
                if (operation.namespaceChanged)
                {
                    string namespaceText = string.IsNullOrEmpty(operation.oldNamespace) 
                        ? $"Namespace: (none) → {operation.newNamespace}"
                        : $"Namespace: {operation.oldNamespace} → {operation.newNamespace}";
                    EditorGUILayout.LabelField(namespaceText, EditorStyles.miniLabel);
                }
                
                if (updateReferences && operation.referencingFiles.Count > 0)
                {
                    EditorGUILayout.LabelField($"References found in {operation.referencingFiles.Count} files:", EditorStyles.miniLabel);
                    foreach (var refFile in operation.referencingFiles.Take(3))
                    {
                        EditorGUILayout.LabelField($"  - {Path.GetFileName(refFile)}", EditorStyles.miniLabel);
                    }
                    if (operation.referencingFiles.Count > 3)
                    {
                        EditorGUILayout.LabelField($"  ... and {operation.referencingFiles.Count - 3} more", EditorStyles.miniLabel);
                    }
                }
                GUILayout.Space(5);
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            
            // Warning
            int totalReferences = renameOperations.Sum(op => op.referencingFiles.Count);
            string warningText = $"This will rename {renameOperations.Count} script(s)";
            if (keepOriginalCopy)
            {
                warningText += $" (keeping original copies)";
            }
            if (renameOperations.Any(op => op.namespaceChanged))
            {
                warningText += $" and modify namespaces";
            }
            if (updateReferences && totalReferences > 0)
            {
                warningText += $" and update {totalReferences} reference(s) in other files";
            }
            warningText += ". Make sure to backup your project!";
            
            EditorGUILayout.HelpBox(warningText, MessageType.Warning);
            
            GUILayout.Space(5);
            
            // Rename button
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("EXECUTE RENAME OPERATIONS", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Confirm Rename", 
                    $"Are you sure you want to rename {renameOperations.Count} script(s)?\n\nMake sure you have backed up your project!", 
                    "Rename", "Cancel"))
                {
                    ExecuteRenameOperations();
                }
            }
            GUI.backgroundColor = Color.white;
        }
        else if (showRenamePreview)
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("No valid scripts selected for renaming, or no prefix/suffix/namespace options enabled.", MessageType.Info);
        }

        GUILayout.Space(10);
        
        // Instructions
        EditorGUILayout.HelpBox("Instructions:\n1. Check desired options (prefix/suffix/namespace/keep original)\n2. Enter values for checked options\n3. Add scripts to rename\n4. Choose whether to update references\n5. Preview operations\n6. Execute rename", MessageType.Info);
    }

    #endregion

    #region Remove Scripts Methods

    private void PreviewRemoveChanges()
    {
        componentsToRemove.Clear();
        affectedObjects.Clear();

        // Filter out null scripts
        var validScripts = selectedScriptsToRemove.Where(s => s != null).ToList();
        
        if (validScripts.Count == 0)
        {
            EditorUtility.DisplayDialog("No Scripts Selected", "Please select at least one script to remove.", "OK");
            return;
        }

        // Get all GameObjects in the scene
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject go in allObjects)
        {
            List<MonoBehaviour> componentsOnThisObject = new List<MonoBehaviour>();
            
            // Get all MonoBehaviour components on this GameObject
            MonoBehaviour[] components = go.GetComponents<MonoBehaviour>();
            
            foreach (MonoBehaviour component in components)
            {
                if (component == null) continue; // Skip missing script references
                
                // Check if this component's script is in our removal list
                foreach (MonoScript script in validScripts)
                {
                    if (script.GetClass() == component.GetType())
                    {
                        componentsOnThisObject.Add(component);
                        break;
                    }
                }
            }
            
            if (componentsOnThisObject.Count > 0)
            {
                componentsToRemove[go] = componentsOnThisObject;
                affectedObjects.Add(go);
            }
        }
    }

    private void RemoveComponents()
    {
        int totalRemoved = 0;
        
        // Register undo for all affected GameObjects
        foreach (GameObject go in affectedObjects)
        {
            Undo.RegisterCompleteObjectUndo(go, "Remove Scripts");
        }

        // Remove the components
        foreach (var kvp in componentsToRemove)
        {
            foreach (var component in kvp.Value)
            {
                if (component != null)
                {
                    DestroyImmediate(component);
                    totalRemoved++;
                }
            }
        }

        // Mark scene as dirty
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().rootCount > 0)
            EditorUtility.SetDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()[0]);
        
        // Clear preview
        showRemovePreview = false;
        componentsToRemove.Clear();
        affectedObjects.Clear();
        
        EditorUtility.DisplayDialog("Complete", $"Successfully removed {totalRemoved} component(s) from the scene.", "OK");
        
        Debug.Log($"ScriptRemoverTool: Removed {totalRemoved} components from {affectedObjects.Count} GameObjects");
    }

    private int GetTotalComponentCount()
    {
        return componentsToRemove.Values.Sum(list => list.Count);
    }

    #endregion

    #region Rename Scripts Methods

    private void PreviewRenameOperations()
    {
        renameOperations.Clear();

        // Filter out null scripts
        var validScripts = selectedScriptsToRename.Where(s => s != null).ToList();
        
        if (validScripts.Count == 0 || (!usePrefix && !useSuffix && !useNamespace))
        {
            return;
        }

        foreach (MonoScript script in validScripts)
        {
            string scriptPath = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(scriptPath)) continue;

            string oldClassName = script.name;
            string newClassName = oldClassName;
            
            // Apply prefix/suffix only if enabled
            if (usePrefix && !string.IsNullOrEmpty(prefix))
            {
                newClassName = prefix + newClassName;
            }
            if (useSuffix && !string.IsNullOrEmpty(suffix))
            {
                newClassName = newClassName + suffix;
            }
            
            string oldFileName = Path.GetFileName(scriptPath);
            string newFileName = oldFileName;
            
            // Only change filename if class name changed
            if (newClassName != oldClassName)
            {
                newFileName = prefix + Path.GetFileNameWithoutExtension(scriptPath) + suffix + Path.GetExtension(scriptPath);
            }

            // Get current namespace
            string currentNamespace = GetNamespaceFromScript(scriptPath);

            RenameOperation operation = new RenameOperation
            {
                script = script,
                oldClassName = oldClassName,
                newClassName = newClassName,
                oldFileName = oldFileName,
                newFileName = newFileName,
                filePath = scriptPath,
                oldNamespace = currentNamespace,
                newNamespace = useNamespace ? newNamespace : currentNamespace,
                namespaceChanged = useNamespace && currentNamespace != newNamespace
            };

            // Find references if enabled
            if (updateReferences)
            {
                operation.referencingFiles = FindScriptReferences(oldClassName);
            }

            renameOperations.Add(operation);
        }
    }

    private string GetNamespaceFromScript(string scriptPath)
    {
        try
        {
            string fullPath = Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - 6), scriptPath);
            string content = File.ReadAllText(fullPath);
            
            // Look for namespace declaration
            Match namespaceMatch = Regex.Match(content, @"namespace\s+([^\s\{]+)");
            if (namespaceMatch.Success)
            {
                return namespaceMatch.Groups[1].Value;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not read namespace from {scriptPath}: {e.Message}");
        }
        
        return string.Empty;
    }

    private List<string> FindScriptReferences(string className)
    {
        List<string> referencingFiles = new List<string>();
        
        // Get all C# script files in the project
        string[] allScripts = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
        
        foreach (string scriptPath in allScripts)
        {
            try
            {
                string content = File.ReadAllText(scriptPath);
                
                // Look for various reference patterns
                string[] patterns = {
                    $@"\b{className}\b",                    // Direct class name reference
                    $@":\s*{className}\b",                  // Inheritance
                    $@"<{className}>",                      // Generic type parameter
                    $@"typeof\s*\(\s*{className}\s*\)",     // typeof expressions
                    $@"GetComponent<{className}>",          // GetComponent calls
                    $@"AddComponent<{className}>",          // AddComponent calls
                };
                
                bool hasReference = false;
                foreach (string pattern in patterns)
                {
                    if (Regex.IsMatch(content, pattern))
                    {
                        hasReference = true;
                        break;
                    }
                }
                
                if (hasReference)
                {
                    // Convert absolute path to relative path from Assets folder
                    string relativePath = "Assets" + scriptPath.Substring(Application.dataPath.Length);
                    referencingFiles.Add(relativePath);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not read file {scriptPath}: {e.Message}");
            }
        }
        
        return referencingFiles;
    }

    private void ExecuteRenameOperations()
    {
        int successCount = 0;
        int errorCount = 0;

        EditorUtility.DisplayProgressBar("Renaming Scripts", "Starting rename operations...", 0f);

        try
        {
            // First, update references in other files
            if (updateReferences)
            {
                for (int i = 0; i < renameOperations.Count; i++)
                {
                    var operation = renameOperations[i];
                    EditorUtility.DisplayProgressBar("Updating References", 
                        $"Updating references for {operation.oldClassName}...", 
                        (float)i / (renameOperations.Count * 2));

                    UpdateReferencesInFiles(operation);
                
                // Update namespace references if namespace changed
                if (operation.namespaceChanged)
                {
                    UpdateNamespaceReferences(operation);
                }
                }
            }

            // Then rename the actual script files and their contents
            for (int i = 0; i < renameOperations.Count; i++)
            {
                var operation = renameOperations[i];
                EditorUtility.DisplayProgressBar("Renaming Files", 
                    $"Renaming {operation.oldFileName}...", 
                    0.5f + (float)i / (renameOperations.Count * 2));

                if (RenameScriptFile(operation))
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // Refresh the asset database
        AssetDatabase.Refresh();

        // Clear preview
        showRenamePreview = false;
        renameOperations.Clear();

        string message = $"Rename operations completed!\nSuccess: {successCount}\nErrors: {errorCount}";
        if (errorCount > 0)
        {
            message += "\n\nCheck the Console for error details.";
        }

        EditorUtility.DisplayDialog("Rename Complete", message, "OK");
        
        Debug.Log($"ScriptManager: Renamed {successCount} scripts successfully, {errorCount} errors");
    }

    private void CreateBackupCopy(RenameOperation operation)
    {
        try
        {
            string originalPath = Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - 6), operation.filePath);
            string directory = Path.GetDirectoryName(originalPath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);
            
            string backupFileName = fileNameWithoutExt + "_Original" + extension;
            string backupPath = Path.Combine(directory, backupFileName);
            
            // Make sure backup filename is unique
            int counter = 1;
            while (File.Exists(backupPath))
            {
                backupFileName = fileNameWithoutExt + "_Original" + counter + extension;
                backupPath = Path.Combine(directory, backupFileName);
                counter++;
            }
            
            // Copy the file
            File.Copy(originalPath, backupPath);
            
            // Copy the .meta file if it exists
            string originalMetaPath = originalPath + ".meta";
            if (File.Exists(originalMetaPath))
            {
                string backupMetaPath = backupPath + ".meta";
                File.Copy(originalMetaPath, backupMetaPath);
            }
            
            Debug.Log($"Created backup: {backupFileName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create backup for {operation.oldFileName}: {e.Message}");
        }
    }

    private void UpdateReferencesInFiles(RenameOperation operation)
    {
        foreach (string filePath in operation.referencingFiles)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - 6), filePath);
                string content = File.ReadAllText(fullPath);
                
                // Replace various reference patterns
                string[] patterns = {
                    $@"\b{operation.oldClassName}\b",
                    $@":\s*{operation.oldClassName}\b",
                    $@"<{operation.oldClassName}>",
                    $@"typeof\s*\(\s*{operation.oldClassName}\s*\)",
                    $@"GetComponent<{operation.oldClassName}>",
                    $@"AddComponent<{operation.oldClassName}>",
                };
                
                string[] replacements = {
                    operation.newClassName,
                    $": {operation.newClassName}",
                    $"<{operation.newClassName}>",
                    $"typeof({operation.newClassName})",
                    $"GetComponent<{operation.newClassName}>",
                    $"AddComponent<{operation.newClassName}>",
                };
                
                for (int i = 0; i < patterns.Length; i++)
                {
                    content = Regex.Replace(content, patterns[i], replacements[i]);
                }
                
                File.WriteAllText(fullPath, content);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to update references in {filePath}: {e.Message}");
            }
        }
    }

    private bool RenameScriptFile(RenameOperation operation)
    {
        try
        {
            // Read the current script content
            string fullPath = Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - 6), operation.filePath);
            string content = File.ReadAllText(fullPath);
            
            // Replace class name in the script content
            content = Regex.Replace(content, $@"\bclass\s+{operation.oldClassName}\b", $"class {operation.newClassName}");
            content = Regex.Replace(content, $@"\bstruct\s+{operation.oldClassName}\b", $"struct {operation.newClassName}");
            content = Regex.Replace(content, $@"\binterface\s+{operation.oldClassName}\b", $"interface {operation.newClassName}");
            
            // Update namespace if changed
            if (operation.namespaceChanged)
            {
                if (!string.IsNullOrEmpty(operation.oldNamespace))
                {
                    // Replace existing namespace
                    content = Regex.Replace(content, $@"namespace\s+{Regex.Escape(operation.oldNamespace)}", $"namespace {operation.newNamespace}");
                }
                else if (!string.IsNullOrEmpty(operation.newNamespace))
                {
                    // Add new namespace - find the first using statement or class declaration
                    string[] lines = content.Split('\n');
                    List<string> newLines = new List<string>();
                    bool namespaceAdded = false;
                    
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        
                        // Add namespace before the first class/struct/interface declaration
                        if (!namespaceAdded && (line.Trim().StartsWith("public class") || 
                                              line.Trim().StartsWith("class") ||
                                              line.Trim().StartsWith("public struct") ||
                                              line.Trim().StartsWith("struct") ||
                                              line.Trim().StartsWith("public interface") ||
                                              line.Trim().StartsWith("interface")))
                        {
                            newLines.Add($"namespace {operation.newNamespace}");
                            newLines.Add("{");
                            
                            // Indent the existing content
                            for (int j = i; j < lines.Length; j++)
                            {
                                if (!string.IsNullOrWhiteSpace(lines[j]))
                                {
                                    newLines.Add("    " + lines[j]);
                                }
                                else
                                {
                                    newLines.Add(lines[j]);
                                }
                            }
                            
                            newLines.Add("}");
                            namespaceAdded = true;
                            break;
                        }
                        else
                        {
                            newLines.Add(line);
                        }
                    }
                    
                    if (namespaceAdded)
                    {
                        content = string.Join("\n", newLines);
                    }
                }
            }
            
            // Write the updated content
            File.WriteAllText(fullPath, content);
            
            // Rename the file
            string directory = Path.GetDirectoryName(fullPath);
            string newFullPath = Path.Combine(directory, operation.newFileName);
            
            if (File.Exists(newFullPath))
            {
                Debug.LogError($"Cannot rename {operation.oldFileName} to {operation.newFileName}: Target file already exists");
                return false;
            }
            
            File.Move(fullPath, newFullPath);
            
            // Handle .meta file
            string oldMetaPath = fullPath + ".meta";
            string newMetaPath = newFullPath + ".meta";
            if (File.Exists(oldMetaPath))
            {
                File.Move(oldMetaPath, newMetaPath);
            }
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to rename script {operation.oldFileName}: {e.Message}");
            return false;
        }
    }

    private void UpdateNamespaceReferences(RenameOperation operation)
    {
        // Find all files that might reference the old namespace
        string[] allScripts = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
        
        foreach (string scriptPath in allScripts)
        {
            try
            {
                string content = File.ReadAllText(scriptPath);
                bool modified = false;
                
                // Update using directives
                if (!string.IsNullOrEmpty(operation.oldNamespace))
                {
                    string usingPattern = $@"using\s+{Regex.Escape(operation.oldNamespace)}\s*;";
                    if (Regex.IsMatch(content, usingPattern))
                    {
                        content = Regex.Replace(content, usingPattern, $"using {operation.newNamespace};");
                        modified = true;
                    }
                }
                
                // Update fully qualified references
                if (!string.IsNullOrEmpty(operation.oldNamespace))
                {
                    string qualifiedPattern = $@"\b{Regex.Escape(operation.oldNamespace)}\.{operation.oldClassName}\b";
                    if (Regex.IsMatch(content, qualifiedPattern))
                    {
                        content = Regex.Replace(content, qualifiedPattern, $"{operation.newNamespace}.{operation.newClassName}");
                        modified = true;
                    }
                }
                
                if (modified)
                {
                    File.WriteAllText(scriptPath, content);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not update namespace references in {scriptPath}: {e.Message}");
            }
        }
    }

    #endregion
}
}