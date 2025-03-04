using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MyGame.Plugins.MyCustom
{
    public class ShapeEditor : EditorWindow
    {
       private enum ShapeType { Rectangle, Triangle, CustomSprite, DrawSprite }
        private enum DrawMode { Polygon, Circle }
        
        private ShapeType currentShape = ShapeType.Rectangle;
        private DrawMode drawMode = DrawMode.Polygon; // Add a draw mode toggle
        
        // Common Settings
        private Color shapeColor = Color.white;
        private Texture2D previewTexture;
        private string exportFileName = "Shape";

        // Rectangle Settings
        private int rectWidth = 100;
        private int rectHeight = 100;
        private float rectCornerRadius = 10f;

        // Triangle Settings
        private int triBaseLength = 100;
        private int triHeight = 100;
        private float triRotationAngle = 0f;

        // Custom Sprite Settings
        private Sprite customSprite;
        private Color customSpriteColor = Color.white;
        private Vector2 customSpriteSize = Vector2.zero;
        private float customSpriteRotation = 0f;
        private bool flipHorizontally = false;

        // DrawSprite Settings
        private Texture2D canvasTexture;
        private Color[] canvasPixels;
        private int canvasWidth = 256;
        private int canvasHeight = 256;
        private Vector2 previousMousePosition;
        //private bool isMouseDown = false;
        private List<Vector2> shapePoints = new List<Vector2>();
        private bool isCreatingShape = false;
        private bool isDragging = false;
        private Vector2 startPoint;
        private Vector2 endPoint;
        private Color currentColor = Color.white;
        private int scaleFactor = 1;

        [MenuItem("Window/Shape Creator")]
        public static void ShowWindow()
        {
            GetWindow<ShapeEditor>("Shape Creator");
        }

        private void OnEnable()
        {
            InitializeCanvas();
        }

        private void InitializeCanvas()
        {
            canvasTexture = new Texture2D(canvasWidth, canvasHeight, TextureFormat.ARGB32, false);
            ClearCanvas();
        }

        private void ClearCanvas()
        {
            canvasPixels = new Color[canvasWidth * canvasHeight];
            for (int i = 0; i < canvasPixels.Length; i++)
            {
                canvasPixels[i] = Color.clear;
            }
            canvasTexture.SetPixels(canvasPixels);
            canvasTexture.Apply();
        }

        private void OnGUI()
        {
            currentShape = (ShapeType)GUILayout.Toolbar((int)currentShape, new string[] { "Rectangle", "Triangle", "Custom Sprite", "DrawSprite" });

            if (currentShape == ShapeType.Rectangle || currentShape == ShapeType.Triangle)
            {
                shapeColor = EditorGUILayout.ColorField("Shape Color", shapeColor);
            }

            exportFileName = EditorGUILayout.TextField("File Name", exportFileName);

            if (currentShape == ShapeType.Rectangle)
            {
                DrawRectangleOptions();
            }
            else if (currentShape == ShapeType.Triangle)
            {
                DrawTriangleOptions();
            }
            else if (currentShape == ShapeType.CustomSprite)
            {
                DrawCustomSpriteOptions();
            }
            else if (currentShape == ShapeType.DrawSprite)
            {
                DrawSpriteOptions();
            }

            if (GUILayout.Button("Generate Preview"))
            {
                GeneratePreview();
            }
            if (GUILayout.Button("Export as Sprite"))
            {
                ExportSprite();
            }
            if (previewTexture != null)
            {
                GUILayout.Label(previewTexture, GUILayout.Width(200), GUILayout.Height(200));
            }
        }

        private bool eraserMode = false; // Add a toggle for eraser mode

        private void DrawSpriteOptions()
        {
            GUILayout.Label("Draw Sprite Settings", EditorStyles.boldLabel);

            // Toggle between Polygon and Circle mode
            drawMode = (DrawMode)EditorGUILayout.EnumPopup("Draw Mode", drawMode);

            // Display the canvas
            Rect canvasRect = GUILayoutUtility.GetRect(canvasWidth, canvasHeight, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            GUI.DrawTexture(canvasRect, canvasTexture, ScaleMode.StretchToFill, false);

            // Eraser Mode Toggle
            eraserMode = EditorGUILayout.Toggle("Eraser Mode", eraserMode);

            // If eraser mode is enabled, set color's alpha to 0
            if (eraserMode)
            {
                currentColor.a = 0;
            }
            else
            {
                // Ensure color alpha is restored to non-zero when not erasing
                currentColor.a = 1; // You can adjust this to any alpha you'd prefer for drawing mode.
            }

            // Handle drawing input based on the selected mode
            if (drawMode == DrawMode.Polygon)
            {
                HandlePolygonDrawing(canvasRect);
            }
            else if (drawMode == DrawMode.Circle)
            {
                HandleCircleDrawing(canvasRect);
            }

            currentColor = EditorGUILayout.ColorField("Current Color", currentColor);
            scaleFactor = EditorGUILayout.IntField("Scale Factor", scaleFactor);
            if (GUILayout.Button("Clear Canvas"))
            {
                ClearCanvas();
            }

            if (GUILayout.Button("Send to Custom"))
            {
                SendToCustomSprite();
            }
        }


        private void HandlePolygonDrawing(Rect canvasRect)
{
    Event e = Event.current;
    if (e.type == EventType.MouseDown && canvasRect.Contains(e.mousePosition))
    {
        if (e.button == 0)
        {
            if (!isCreatingShape)
            {
                isCreatingShape = true;
                shapePoints.Clear();
            }
            shapePoints.Add(e.mousePosition - canvasRect.position);
            e.Use();
        }
        else if (e.button == 1 && isCreatingShape)
        {
            FillShape();
            isCreatingShape = false;
            shapePoints.Clear();
            e.Use();
        }
    }

    if (isCreatingShape && shapePoints.Count > 1)
    {
        Handles.BeginGUI();
        // If eraser mode is enabled, show the shape outline as a wireframe in current color
        Handles.color = eraserMode ? Color.red : currentColor; // Use red or any other color for wireframe visualization

        for (int i = 0; i < shapePoints.Count - 1; i++)
        {
            Handles.DrawLine(shapePoints[i] + canvasRect.position, shapePoints[i + 1] + canvasRect.position);
        }
        Handles.DrawLine(shapePoints[shapePoints.Count - 1] + canvasRect.position, shapePoints[0] + canvasRect.position);

        Handles.EndGUI();
    }
}

private void HandleCircleDrawing(Rect canvasRect)
{
    Event e = Event.current;

    if (e.type == EventType.MouseDown && e.button == 0 && canvasRect.Contains(e.mousePosition))
    {
        isDragging = true;
        startPoint = e.mousePosition - canvasRect.position;
        e.Use();
    }
    else if (e.type == EventType.MouseDrag && isDragging)
    {
        endPoint = e.mousePosition - canvasRect.position;
        e.Use();
        Repaint();
    }
    else if (e.type == EventType.MouseUp && e.button == 0 && isDragging)
    {
        isDragging = false;
        endPoint = e.mousePosition - canvasRect.position;
        DrawFilledCircle();
        e.Use();
    }

    if (isDragging)
    {
        Handles.BeginGUI();
        // If eraser mode is enabled, show the wireframe of the circle
        Handles.color = eraserMode ? Color.red : currentColor; // Red or another color for wireframe visualization

        Vector2 center = startPoint;
        float radius = Vector2.Distance(startPoint, endPoint);
        Handles.DrawWireDisc(center + canvasRect.position, Vector3.forward, radius);

        Handles.EndGUI();
    }
}

        private void DrawFilledCircle()
        {
            Vector2 center = startPoint;
            float radius = Vector2.Distance(startPoint, endPoint);

            int cx = (int)center.x;
            int cy = canvasHeight - (int)center.y;

            int radiusInt = Mathf.CeilToInt(radius);
            for (int y = -radiusInt; y <= radiusInt; y++)
            {
                for (int x = -radiusInt; x <= radiusInt; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        SetPixel(px, py, currentColor);
                    }
                }
            }

            canvasTexture.SetPixels(canvasPixels);
            canvasTexture.Apply();
        }


        

        private void DrawRectangleOptions()
        {
            GUILayout.Label("Rectangle Settings", EditorStyles.boldLabel);
            rectWidth = EditorGUILayout.IntField("Width", rectWidth);
            rectHeight = EditorGUILayout.IntField("Height", rectHeight);
            rectCornerRadius = EditorGUILayout.Slider("Corner Radius", rectCornerRadius, 0f, Mathf.Min(rectWidth, rectHeight) / 2f);
        }

        private void DrawTriangleOptions()
        {
            GUILayout.Label("Triangle Settings", EditorStyles.boldLabel);
            triBaseLength = EditorGUILayout.IntField("Base Length", triBaseLength);
            triHeight = EditorGUILayout.IntField("Height", triHeight);
            triRotationAngle = EditorGUILayout.Slider("Rotation Angle", triRotationAngle, 0f, 360f);
        }

        private void DrawCustomSpriteOptions()
        {
            GUILayout.Label("Custom Sprite Settings", EditorStyles.boldLabel);
            Sprite selectedSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", customSprite, typeof(Sprite), false);
            if (selectedSprite != customSprite && selectedSprite != null)
            {
                customSprite = selectedSprite;
                customSpriteSize = new Vector2(customSprite.texture.width, customSprite.texture.height);
            }
            
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                ClearCustomSprite();
            }

            //GUILayout.EndHorizontal();
            customSpriteSize = EditorGUILayout.Vector2Field("Sprite Size", customSpriteSize);
            customSpriteColor = EditorGUILayout.ColorField("Sprite Color", customSpriteColor);
            customSpriteRotation = EditorGUILayout.Slider("Rotation", customSpriteRotation, 0f, 360f);
            flipHorizontally = EditorGUILayout.Toggle("Flip Horizontally", flipHorizontally);
            
            
        }
        
        private void ClearCustomSprite()
        {
            customSprite = null;
            customSpriteSize = Vector2.zero;
        }

        
        
        private void SendToCustomSprite()
        {
            // Set the custom sprite to the canvas texture (assuming it's a Texture2D, convert it to a Sprite)
            Rect spriteRect = new Rect(0, 0, canvasTexture.width, canvasTexture.height);
            Vector2 pivot = new Vector2(0.5f, 0.5f); // Pivot point in the center of the texture
            customSprite = Sprite.Create(canvasTexture, spriteRect, pivot);

            // Set the size of the custom sprite to match the canvas
            customSpriteSize = new Vector2(canvasWidth, canvasHeight);
        }

        private void HandleDrawingInput(Rect canvasRect)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && canvasRect.Contains(e.mousePosition))
            {
                if (e.button == 0)
                {
                    if (!isCreatingShape)
                    {
                        isCreatingShape = true;
                        shapePoints.Clear();
                    }
                    shapePoints.Add(e.mousePosition - canvasRect.position);
                    e.Use();
                }
                else if (e.button == 1 && isCreatingShape)
                {
                    FillShape();
                    isCreatingShape = false;
                    shapePoints.Clear();
                    e.Use();
                }
            }

            if (isCreatingShape && shapePoints.Count > 1)
            {
                Handles.BeginGUI();
                Handles.color = currentColor;
                for (int i = 0; i < shapePoints.Count - 1; i++)
                {
                    Handles.DrawLine(shapePoints[i] + canvasRect.position, shapePoints[i + 1] + canvasRect.position);
                }
                Handles.DrawLine(shapePoints[shapePoints.Count - 1] + canvasRect.position, shapePoints[0] + canvasRect.position);
                Handles.EndGUI();
            }
        }

        private void FillShape()
        {
            Vector2[] points = new Vector2[shapePoints.Count];
            for (int i = 0; i < shapePoints.Count; i++)
            {
                Vector2 p = shapePoints[i];
                p.y = canvasHeight - p.y;
                points[i] = p;
            }
            FillPolygon(points, currentColor);
            canvasTexture.SetPixels(canvasPixels);
            canvasTexture.Apply();
        }

        private void FillPolygon(Vector2[] polygon, Color fillColor)
        {
            Rect bounds = GetPolygonBounds(polygon);
            int minX = Mathf.Clamp(Mathf.FloorToInt(bounds.xMin), 0, canvasWidth - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(bounds.xMax), 0, canvasWidth - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(bounds.yMin), 0, canvasHeight - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(bounds.yMax), 0, canvasHeight - 1);

            for (int y = minY; y <= maxY; y++)
            {
                List<int> nodeX = new List<int>();
                int j = polygon.Length - 1;
                for (int i = 0; i < polygon.Length; i++)
                {
                    if ((polygon[i].y < y && polygon[j].y >= y) || (polygon[j].y < y && polygon[i].y >= y))
                    {
                        int x = Mathf.RoundToInt(polygon[i].x + (y - polygon[i].y) / (polygon[j].y - polygon[i].y) * (polygon[j].x - polygon[i].x));
                        nodeX.Add(x);
                    }
                    j = i;
                }
                nodeX.Sort();
                for (int i = 0; i < nodeX.Count; i += 2)
                {
                    if (i + 1 >= nodeX.Count) break;
                    int xStart = Mathf.Clamp(nodeX[i], 0, canvasWidth - 1);
                    int xEnd = Mathf.Clamp(nodeX[i + 1], 0, canvasWidth - 1);
                    for (int x = xStart; x <= xEnd; x++)
                    {
                        SetPixel(x, y, fillColor);
                    }
                }
            }
        }

        private Rect GetPolygonBounds(Vector2[] polygon)
        {
            float xMin = float.MaxValue, xMax = float.MinValue;
            float yMin = float.MaxValue, yMax = float.MinValue;
            foreach (Vector2 point in polygon)
            {
                if (point.x < xMin) xMin = point.x;
                if (point.x > xMax) xMax = point.x;
                if (point.y < yMin) yMin = point.y;
                if (point.y > yMax) yMax = point.y;
                    }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void SetPixel(int x, int y, Color color)
        {
            if (x >= 0 && x < canvasWidth && y >= 0 && y < canvasHeight)
            {
                // If the color alpha is 0, erase the pixel by setting it to Color.clear
                canvasPixels[y * canvasWidth + x] = (color.a == 0) ? Color.clear : color;
            }
        }

        private void ExportSprite()
        {
            if (currentShape == ShapeType.DrawSprite)
            {
                ExportDrawnSprite();
            }
            else
            {
                // Other shape export logic (Rectangle, Triangle, Custom Sprite)
                ExportShapeSprite();
            }
        }

        private void ExportDrawnSprite()
        {
            // Prompt for file path
            string path = EditorUtility.SaveFilePanel("Save Image", "", exportFileName + ".png", "png");
            if (string.IsNullOrEmpty(path)) return;

            // Create a scaled texture
            int width = canvasWidth * scaleFactor;
            int height = canvasHeight * scaleFactor;
            Texture2D exportTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);

            // Scale the canvas texture
            for (int y = 0; y < height; y++)
            {
                int sourceY = y / scaleFactor;
                for (int x = 0; x < width; x++)
                {
                    int sourceX = x / scaleFactor;
                    Color pixelColor = canvasPixels[sourceY * canvasWidth + sourceX];
                    exportTexture.SetPixel(x, y, pixelColor);
                }
            }

            exportTexture.Apply();

            // Crop transparent pixels
            Texture2D croppedTexture = CropTransparentPixels(exportTexture);

            // Encode texture into PNG
            byte[] bytes = croppedTexture.EncodeToPNG();

            // Write to file
            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();

            // Clean up
            DestroyImmediate(exportTexture);
            DestroyImmediate(croppedTexture);

            Debug.Log("Image exported to: " + path);
        }

        private Texture2D CropTransparentPixels(Texture2D source)
        {
            int width = source.width;
            int height = source.height;

            int xMin = width;
            int xMax = 0;
            int yMin = height;
            int yMax = 0;

            Color32[] pixels = source.GetPixels32();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color32 pixel = pixels[y * width + x];
                    if (pixel.a != 0)
                    {
                        if (x < xMin) xMin = x;
                        if (x > xMax) xMax = x;
                        if (y < yMin) yMin = y;
                        if (y > yMax) yMax = y;
                    }
                }
            }

            int croppedWidth = xMax - xMin + 1;
            int croppedHeight = yMax - yMin + 1;

            if (croppedWidth <= 0 || croppedHeight <= 0)
            {
                // No content, return empty texture
                return new Texture2D(2, 2);
            }

            Texture2D croppedTexture = new Texture2D(croppedWidth, croppedHeight, TextureFormat.ARGB32, false);
            Color[] newPixels = source.GetPixels(xMin, yMin, croppedWidth, croppedHeight);
            croppedTexture.SetPixels(newPixels);
            croppedTexture.Apply();

            return croppedTexture;
        }

        private void GeneratePreview()
        {
            if (currentShape == ShapeType.Rectangle)
            {
                previewTexture = new Texture2D(rectWidth, rectHeight);
                FillRectangle(previewTexture, rectWidth, rectHeight, rectCornerRadius, shapeColor);
            }
            else if (currentShape == ShapeType.Triangle)
            {
                // Update bounds after rotation
                int newSize = Mathf.CeilToInt(Mathf.Max(triBaseLength, triHeight) * Mathf.Sqrt(2));  // Estimate the new size after rotation
                previewTexture = new Texture2D(newSize, newSize);
                FillTriangle(previewTexture, triBaseLength, triHeight, shapeColor, triRotationAngle, newSize);
            }
            else if (currentShape == ShapeType.CustomSprite)
            {
                if (customSprite != null)
                {
                    EnsureTextureIsReadable(customSprite.texture);
                    previewTexture = new Texture2D((int)customSpriteSize.x, (int)customSpriteSize.y);
                    FillCustomSprite(previewTexture, customSprite, customSpriteSize, customSpriteRotation, customSpriteColor, flipHorizontally);
                }
            }
            else if (currentShape == ShapeType.DrawSprite)
            {
                previewTexture = canvasTexture;
            }

            if (previewTexture != null)
            {
                previewTexture.Apply();
            }
        }
                private void FillRectangle(Texture2D texture, int width, int height, float cornerRadius, Color color)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (IsInsideRoundedRectangle(x, y, width, height, cornerRadius))
                    {
                        texture.SetPixel(x, y, color);
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }
        }

        private void FillTriangle(Texture2D texture, int baseLength, int height, Color color, float rotationAngle, int newSize)
        {
            Vector2 center = new Vector2(newSize / 2f, newSize / 2f);  // Use center of the new bounds for rotation
            Vector2 top = RotatePoint(new Vector2(baseLength / 2f, height), rotationAngle, center.x, center.y);
            Vector2 bottomLeft = RotatePoint(new Vector2(0, 0), rotationAngle, center.x, center.y);
            Vector2 bottomRight = RotatePoint(new Vector2(baseLength, 0), rotationAngle, center.x, center.y);

            for (int y = 0; y < newSize; y++)
            {
                for (int x = 0; x < newSize; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    if (IsInsideTriangle(point, top, bottomLeft, bottomRight))
                    {
                        texture.SetPixel(x, y, color);
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }
        }

        private void FillCustomSprite(Texture2D texture, Sprite sprite, Vector2 size, float rotation, Color color, bool flipHorizontally)
        {
            // Ensure the sprite is readable and create a resized, rotated, and flipped version if necessary
            Texture2D spriteTexture = sprite.texture;
            Texture2D resizedTexture = ResizeTexture(spriteTexture, (int)size.x, (int)size.y);
            Texture2D rotatedTexture = RotateTexture(resizedTexture, rotation);
            Texture2D flippedTexture = flipHorizontally ? FlipTextureHorizontally(rotatedTexture) : rotatedTexture;

            flippedTexture.filterMode = FilterMode.Bilinear;
            // Apply the color and set the texture
            Color[] finalPixels = flippedTexture.GetPixels();
            for (int i = 0; i < finalPixels.Length; i++)
            {
                finalPixels[i] *= color;  // Apply color tint
            }
            texture.SetPixels(finalPixels);
            texture.Apply();
        }

                private bool IsInsideRoundedRectangle(int x, int y, int width, int height, float cornerRadius)
        {
            // Check corners for roundness and straight edges for regular fill
            if (x < cornerRadius && y < cornerRadius)
                return (x - cornerRadius) * (x - cornerRadius) + (y - cornerRadius) * (y - cornerRadius) <= cornerRadius * cornerRadius;
            if (x >= width - cornerRadius && y < cornerRadius)
                return (x - (width - cornerRadius)) * (x - (width - cornerRadius)) + (y - cornerRadius) * (y - cornerRadius) <= cornerRadius * cornerRadius;
            if (x < cornerRadius && y >= height - cornerRadius)
                return (x - cornerRadius) * (x - cornerRadius) + (y - (height - cornerRadius)) * (y - (height - cornerRadius)) <= cornerRadius * cornerRadius;
            if (x >= width - cornerRadius && y >= height - cornerRadius)
                return (x - (width - cornerRadius)) * (x - (width - cornerRadius)) + (y - (height - cornerRadius)) * (y - (height - cornerRadius)) <= cornerRadius * cornerRadius;

            return true;  // Inside straight edges
        }

        private bool IsInsideTriangle(Vector2 point, Vector2 top, Vector2 bottomLeft, Vector2 bottomRight)
        {
            // Barycentric coordinate system to check if a point is inside a triangle
            float denominator = (bottomLeft.y - bottomRight.y) * (top.x - bottomRight.x) + (bottomRight.x - bottomLeft.x) * (top.y - bottomRight.y);
            float a = ((bottomLeft.y - bottomRight.y) * (point.x - bottomRight.x) + (bottomRight.x - bottomLeft.x) * (point.y - bottomRight.y)) / denominator;
            float b = ((bottomRight.y - top.y) * (point.x - bottomRight.x) + (top.x - bottomRight.x) * (point.y - bottomRight.y)) / denominator;
            float c = 1 - a - b;

            return a >= 0 && b >= 0 && c >= 0;
        }

        private Texture2D ResizeTexture(Texture2D texture, int newWidth, int newHeight)
        {
            Texture2D newTexture = new Texture2D(newWidth, newHeight);
            Color[] pixels = texture.GetPixels();
            Color[] resizedPixels = new Color[newWidth * newHeight];

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    float scaleX = (float)x / (float)newWidth;
                    float scaleY = (float)y / (float)newHeight;
                    int pixelX = Mathf.FloorToInt(scaleX * texture.width);
                    int pixelY = Mathf.FloorToInt(scaleY * texture.height);

                    resizedPixels[y * newWidth + x] = texture.GetPixel(pixelX, pixelY);
                }
            }

            newTexture.SetPixels(resizedPixels);
            newTexture.Apply();
            return newTexture;
        }

        private Texture2D RotateTexture(Texture2D texture, float angle)
        {
            int width = texture.width;
            int height = texture.height;
            Texture2D rotatedTexture = new Texture2D(width, height);

            float radAngle = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radAngle);
            float sin = Mathf.Sin(radAngle);

            int x0 = width / 2;
            int y0 = height / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int dx = x - x0;
                    int dy = y - y0;

                    int newX = Mathf.RoundToInt(cos * dx - sin * dy) + x0;
                    int newY = Mathf.RoundToInt(sin * dx + cos * dy) + y0;

                    if (newX >= 0 && newX < width && newY >= 0 && newY < height)
                    {
                        rotatedTexture.SetPixel(x, y, texture.GetPixel(newX, newY));
                    }
                    else
                    {
                        rotatedTexture.SetPixel(x, y, Color.clear);
                    }
                }
            }

            rotatedTexture.Apply();
            return rotatedTexture;
        }

        private Texture2D FlipTextureHorizontally(Texture2D texture)
        {
            int width = texture.width;
            int height = texture.height;
            Texture2D flippedTexture = new Texture2D(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    flippedTexture.SetPixel(x, y, texture.GetPixel(width - x - 1, y));
                }
            }

            flippedTexture.Apply();
            return flippedTexture;
        }

        private Vector2 RotatePoint(Vector2 point, float angle, float centerX, float centerY)
        {
            float radAngle = angle * Mathf.Deg2Rad;
            float cosAngle = Mathf.Cos(radAngle);
            float sinAngle = Mathf.Sin(radAngle);

            float dx = point.x - centerX;
            float dy = point.y - centerY;

            float newX = centerX + (dx * cosAngle - dy * sinAngle);
            float newY = centerY + (dx * sinAngle + dy * cosAngle);

            return new Vector2(newX, newY);
        }


        private void ExportShapeSprite()
        {
            string path = EditorUtility.SaveFilePanel("Save Shape", "", exportFileName + ".png", "png");
            if (string.IsNullOrEmpty(path)) return;

            Texture2D exportTexture = previewTexture;

            if (exportTexture != null)
            {
                // Crop transparent pixels if needed
                Texture2D croppedTexture = CropTransparentPixels(exportTexture);

                byte[] bytes = croppedTexture.EncodeToPNG();
                System.IO.File.WriteAllBytes(path, bytes);
                AssetDatabase.Refresh();

                Debug.Log("Shape exported to: " + path);
            }
            else
            {
                Debug.LogWarning("No shape generated to export.");
            }
        }
        private void EnsureTextureIsReadable(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                AssetDatabase.ImportAsset(path);
            }
        }
    }
}

           
