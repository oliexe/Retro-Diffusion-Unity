using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Collections;
using System.Linq;

namespace RetroDiffusion
{
    [Serializable]
    public class RetroImageResult
    {
        public long created_at;
        public int credit_cost;
        public List<string> base64_images;
        public string model;
        public string type;
        public int remaining_credits;
    }

    [Serializable]
    public class RetroCreditsResult
    {
        public int credits;
    }

    public class RetroModelStyle
    {
        public string name;
        public string displayName;
        
        public RetroModelStyle(string name, string displayName)
        {
            this.name = name;
            this.displayName = displayName;
        }
    }

    public class RetroModel
    {
        public string name;
        public string displayName;
        public List<RetroModelStyle> styles = new List<RetroModelStyle>();
        
        public RetroModel(string name, string displayName)
        {
            this.name = name;
            this.displayName = displayName;
        }
    }

    [Serializable]
    public class RetroImageRequestPayload
    {
        // Required parameters - these will always be included in the JSON
        public string model;
        public int width;
        public int height;
        public string prompt;
        public int num_images;
        
        // Optional parameters - these will be conditionally added to the JSON
        public string prompt_style;
        public float? strength;
        public bool? remove_bg;
        public bool? tile_x;
        public bool? tile_y;
        public int? seed;
        public bool? return_spritesheet;
        public string input_image;
        public string input_palette;
        public float? upscale_output_factor;
        
        // Helper method for creating JSON through a Dictionary
        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>
            {
                { "model", model },
                { "width", width },
                { "height", height },
                { "prompt", prompt },
                { "num_images", num_images }
            };
            
            if (!string.IsNullOrEmpty(prompt_style))
            {
                dict.Add("prompt_style", prompt_style);
            }
            
            if (strength.HasValue)
            {
                dict.Add("strength", strength.Value);
            }
            
            if (remove_bg.HasValue && remove_bg.Value)
            {
                dict.Add("remove_bg", true);
            }
            
            if (tile_x.HasValue && tile_x.Value)
            {
                dict.Add("tile_x", true);
            }
            
            if (tile_y.HasValue && tile_y.Value)
            {
                dict.Add("tile_y", true);
            }
            
            if (seed.HasValue)
            {
                dict.Add("seed", seed.Value);
            }
            
            if (return_spritesheet.HasValue && return_spritesheet.Value)
            {
                dict.Add("return_spritesheet", true);
            }
            
            if (!string.IsNullOrEmpty(input_image))
            {
                dict.Add("input_image", input_image);
            }
            
            if (!string.IsNullOrEmpty(input_palette))
            {
                dict.Add("input_palette", input_palette);
            }
            
            if (upscale_output_factor.HasValue)
            {
                dict.Add("upscale_output_factor", upscale_output_factor.Value);
            }
            
            return dict;
        }
    }

    public enum RetroGenerationMode
    {
        Text2Img,
        Img2Img,
        WithPalette
    }

    [Serializable]
    public class RetroTextureImportSettings
    {
        public TextureImporterType textureType = TextureImporterType.Sprite;
        public bool isReadable = true;
        public TextureWrapMode wrapMode = TextureWrapMode.Clamp;
        public FilterMode filterMode = FilterMode.Point; // Point filtering for pixel art
        public bool generateMipMaps = false;
        public bool alphaIsTransparency = true;
        public SpriteMeshType spriteMeshType = SpriteMeshType.FullRect;
        
        // Sprite-specific settings
        public SpriteImportMode spriteImportMode = SpriteImportMode.Single;
        public int pixelsPerUnit = 16; // Good for pixel art
        public SpritePivot spritePivot = SpritePivot.Center;
        
        // Animation settings
        public bool createAnimatorController = false;
        public float frameRate = 8f; // For animation clips
    }

    public enum SpritePivot
    {
        Center,
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
        Custom
    }

    public class RetroTextureImportManager
    {
        public static void SetupTextureImportSettings(string assetPath, RetroTextureImportSettings settings)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            {
                Debug.LogError($"Cannot set texture import settings for {assetPath}: File does not exist");
                return;
            }

            // Get importer for the texture
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"Failed to get TextureImporter for {assetPath}");
                return;
            }

            // Set texture import settings
            importer.textureType = settings.textureType;
            importer.isReadable = settings.isReadable;
            importer.wrapMode = settings.wrapMode;
            importer.filterMode = settings.filterMode;
            importer.mipmapEnabled = settings.generateMipMaps;
            importer.alphaIsTransparency = settings.alphaIsTransparency;

            // Set sprite-specific settings if it's a sprite
            if (settings.textureType == TextureImporterType.Sprite)
            {
                importer.spriteImportMode = settings.spriteImportMode;
                importer.spritePixelsPerUnit = settings.pixelsPerUnit;
                
                // Set mesh type and pivot using TextureImporterSettings
                var spriteSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(spriteSettings);
                
                spriteSettings.spriteMeshType = settings.spriteMeshType;
                spriteSettings.spritePivot = GetPivotForEnum(settings.spritePivot);
                
                importer.SetTextureSettings(spriteSettings);
            }

            // Apply the changes
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            
            Debug.Log($"Applied texture import settings to {assetPath}");
            
            // Create animation if needed
            if (settings.createAnimatorController && 
                (Path.GetExtension(assetPath) == ".gif" || 
                 (Path.GetExtension(assetPath) == ".png" && IsLikelySpritesheet(assetPath))))
            {
                CreateAnimationFromImage(assetPath, settings);
            }
        }
        
        private static Vector2 GetPivotForEnum(SpritePivot pivot)
        {
            switch (pivot)
            {
                case SpritePivot.Center: return new Vector2(0.5f, 0.5f);
                case SpritePivot.TopLeft: return new Vector2(0f, 1f);
                case SpritePivot.TopCenter: return new Vector2(0.5f, 1f);
                case SpritePivot.TopRight: return new Vector2(1f, 1f);
                case SpritePivot.MiddleLeft: return new Vector2(0f, 0.5f);
                case SpritePivot.MiddleRight: return new Vector2(1f, 0.5f);
                case SpritePivot.BottomLeft: return new Vector2(0f, 0f);
                case SpritePivot.BottomCenter: return new Vector2(0.5f, 0f);
                case SpritePivot.BottomRight: return new Vector2(1f, 0f);
                default: return new Vector2(0.5f, 0.5f);
            }
        }
        
        private static bool IsLikelySpritesheet(string assetPath)
        {
            // Check if the filename or containing folder suggests a spritesheet
            string filename = Path.GetFileNameWithoutExtension(assetPath);
            string folder = Path.GetDirectoryName(assetPath);
            
            return filename.Contains("spritesheet") || 
                   folder.Contains("spritesheet") ||
                   filename.Contains("animation") ||
                   folder.Contains("animation");
        }
        
        private static void CreateAnimationFromImage(string assetPath, RetroTextureImportSettings settings)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            
            // For GIFs, we need multiple sprites
            if (Path.GetExtension(assetPath) == ".gif" || IsLikelySpritesheet(assetPath))
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                
                // Default to grid settings for spritesheets
                if (IsLikelySpritesheet(assetPath))
                {
                    // Get texture dimensions
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (texture != null)
                    {
                        // For a 4-angle walking animation, assume 4 columns
                        int columns = 4;
                        int cellWidth = texture.width / columns;
                        int cellHeight = cellWidth; // Assume square cells
                        
                        // Create sprite sheet settings
                        importer.spritesheet = GenerateSpriteSheetData(texture.width, texture.height, 
                                                                        cellWidth, cellHeight, columns);
                    }
                }
                
                importer.SaveAndReimport();
                
                // Create animation clip from sprites
                CreateAnimationClip(assetPath, settings.frameRate);
            }
        }
        
        private static SpriteMetaData[] GenerateSpriteSheetData(int textureWidth, int textureHeight, 
                                                                 int cellWidth, int cellHeight, int columns)
        {
            int rows = textureHeight / cellHeight;
            int totalCells = rows * columns;
            
            var spriteSheet = new SpriteMetaData[totalCells];
            
            for (int i = 0; i < totalCells; i++)
            {
                int row = i / columns;
                int col = i % columns;
                
                spriteSheet[i] = new SpriteMetaData
                {
                    name = $"frame_{i}",
                    rect = new Rect(col * cellWidth, textureHeight - (row + 1) * cellHeight, cellWidth, cellHeight),
                    pivot = new Vector2(0.5f, 0.5f),
                    alignment = (int)SpriteAlignment.Center
                };
            }
            
            return spriteSheet;
        }
        
        private static void CreateAnimationClip(string assetPath, float frameRate)
        {
            // Get all sprites from the texture
            var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                                      .OfType<Sprite>()
                                      .ToArray();
            
            if (sprites.Length <= 1) return;
            
            // Create animation clip
            string directory = Path.GetDirectoryName(assetPath);
            string clipName = Path.GetFileNameWithoutExtension(assetPath) + "_Animation";
            string clipPath = Path.Combine(directory, clipName + ".anim");
            
            // Normalize path for Unity
            clipPath = clipPath.Replace('\\', '/');
            
            // Create animation clip
            var clip = new AnimationClip
            {
                frameRate = frameRate
            };
            
            // Create sprite curve
            var spriteBinding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };
            
            // Create keyframes
            var keyframes = new ObjectReferenceKeyframe[sprites.Length];
            float timePerFrame = 1f / frameRate;
            
            for (int i = 0; i < sprites.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i * timePerFrame,
                    value = sprites[i]
                };
            }
            
            // Set curve
            AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);
            
            // Make it loop
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            
            // Save the clip as an asset
            AssetDatabase.CreateAsset(clip, clipPath);
            AssetDatabase.SaveAssets();
            
            // Create an animator controller
            string controllerPath = Path.Combine(directory, clipName + "_Controller.controller");
            controllerPath = controllerPath.Replace('\\', '/');
            
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var rootStateMachine = controller.layers[0].stateMachine;
            var state = rootStateMachine.AddState("Animation");
            state.motion = clip;
            
            AssetDatabase.SaveAssets();
            Debug.Log($"Created animation clip and controller at {clipPath}");
        }
    }

    public class RetroImageSettings
    {
        public string model = "RD_FLUX";
        public int width = 256;
        public int height = 256;
        public string prompt;
        public int numImages = 1;
        public string promptStyle = "default";
        public float strength = 0.8f;
        public bool removeBackground = false;
        public bool tileX = false;
        public bool tileY = false;
        public int? seed = null;
        public bool returnSpritesheet = false;
        public string inputImagePath = "";
        public string inputPalettePath = "";
        public RetroGenerationMode generationMode = RetroGenerationMode.Text2Img;
        public float? upscaleOutputFactor = null;

        // For animation
        public bool isAnimation = false;

        // Add texture import settings
        public RetroTextureImportSettings textureImportSettings = new RetroTextureImportSettings();

        public RetroImageRequestPayload ToRequestPayload()
        {
            // Create payload with only the required parameters
            var payload = new RetroImageRequestPayload
            {
                model = model,
                width = width,
                height = height,
                prompt = prompt,
                num_images = numImages
            };
            
            // Only add optional parameters when applicable
            if (!string.IsNullOrEmpty(promptStyle) && promptStyle != "default")
            {
                payload.prompt_style = promptStyle;
            }

            // Handle background removal
            if (removeBackground)
            {
                payload.remove_bg = true;
                Debug.Log("Adding remove_bg=true to payload");
            }

            // Handle tiling
            if (tileX)
            {
                payload.tile_x = true;
            }

            if (tileY)
            {
                payload.tile_y = true;
            }

            // Handle seed
            if (seed.HasValue)
            {
                payload.seed = seed.Value;
            }

            // Handle animation settings
            if (isAnimation && promptStyle == "animation_four_angle_walking" && returnSpritesheet)
            {
                payload.return_spritesheet = true;
            }

            // Handle img2img - ONLY add strength and input_image for img2img mode
            if (generationMode == RetroGenerationMode.Img2Img && !string.IsNullOrEmpty(inputImagePath))
            {
                try
                {
                    string base64Image = ConvertImageToBase64(inputImagePath);
                    if (!string.IsNullOrEmpty(base64Image))
                    {
                        payload.input_image = base64Image;
                        payload.strength = strength;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error converting input image to base64: {ex.Message}");
                }
            }

            // Handle palette reference
            if (generationMode == RetroGenerationMode.WithPalette && !string.IsNullOrEmpty(inputPalettePath))
            {
                try
                {
                    string base64Palette = ConvertImageToBase64(inputPalettePath);
                    if (!string.IsNullOrEmpty(base64Palette))
                    {
                        payload.input_palette = base64Palette;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error converting palette image to base64: {ex.Message}");
                }
            }

            // Handle upscale factor
            if (upscaleOutputFactor.HasValue)
            {
                payload.upscale_output_factor = upscaleOutputFactor.Value;
            }

            return payload;
        }

        private string ConvertImageToBase64(string imagePath)
        {
            if (!File.Exists(imagePath))
            {
                Debug.LogError($"Image file not found: {imagePath}");
                return null;
            }

            try
            {
                // Load the image and convert to texture
                byte[] fileData = File.ReadAllBytes(imagePath);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(fileData);

                // Create an RGB copy without transparency
                Texture2D rgbTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                rgbTexture.SetPixels(texture.GetPixels());
                rgbTexture.Apply();

                // Encode to PNG and convert to base64
                byte[] pngBytes = rgbTexture.EncodeToPNG();
                string base64String = Convert.ToBase64String(pngBytes);
                
                return base64String;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error converting image to base64: {ex.Message}");
                return null;
            }
        }
    }

    public class RetroInputManager
    {
        private const string API_KEY_PREF_KEY = "RetroInputManager_ApiKey";
        private const string SAVE_PATH_PREF_KEY = "RetroInputManager_SavePath";
        
        public static string GetApiKey()
        {
            return EditorPrefs.GetString(API_KEY_PREF_KEY, "");
        }
        
        public static void SetApiKey(string apiKey)
        {
            EditorPrefs.SetString(API_KEY_PREF_KEY, apiKey);
        }
        
        public static string GetSavePath()
        {
            return EditorPrefs.GetString(SAVE_PATH_PREF_KEY, "Assets/RetroImages");
        }
        
        public static void SetSavePath(string path)
        {
            EditorPrefs.SetString(SAVE_PATH_PREF_KEY, path);
        }
    }

    public class RetroApiClient
    {
        private const string BASE_URL = "https://api.retrodiffusion.ai/v1";
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public RetroApiClient(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-RD-Token", _apiKey);
            
            // Set timeout to 90 seconds to ensure it can handle long-running requests
            _httpClient.Timeout = TimeSpan.FromSeconds(90);
        }

        public async Task<RetroImageResult> GenerateImages(RetroImageSettings settings)
        {
            try
            {
                var url = $"{BASE_URL}/inferences";
                
                // Convert settings to request payload
                var payload = settings.ToRequestPayload();
                
                // Use Newtonsoft.Json to properly handle nullable types
                var dict = payload.ToDictionary();
                string json = SimpleJsonConverter.ToJson(dict);
                
                Debug.Log($"Sending request to URL: {url}");
                Debug.Log($"API Key: {_apiKey.Substring(0, Math.Min(4, _apiKey.Length))}... (hidden for security)");
                Debug.Log($"Request payload: {json}");
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                
                // Test if API key exists
                if (string.IsNullOrEmpty(_apiKey) || _apiKey.Length < 10)
                {
                    throw new Exception("API key is missing or appears invalid. Please check your API key in the settings.");
                }

                Debug.Log("Sending request to Retro Diffusion API. This may take up to 60 seconds...");
                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                Debug.Log($"Response status code: {response.StatusCode}");
                Debug.Log($"Response body: {responseBody}");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new Exception("Unauthorized: Your API key appears to be invalid. Please check your API key in the settings.");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    {
                        throw new Exception("Internal Server Error: The Retro Diffusion API is experiencing issues. Please try again later or contact their support.");
                    }
                    else
                    {
                        throw new Exception($"API Error ({response.StatusCode}): {responseBody}");
                    }
                }

                try
                {
                    var result = JsonUtility.FromJson<RetroImageResult>(responseBody);
                    if (result == null)
                    {
                        throw new Exception("Failed to parse response JSON");
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing response: {ex.Message}");
                    Debug.LogError($"Response body: {responseBody}");
                    throw new Exception($"Failed to parse API response: {ex.Message}");
                }
            }
            catch (TaskCanceledException)
            {
                Debug.LogError("The request timed out. The API may be experiencing high load or the image generation is taking longer than expected.");
                throw new Exception("The request timed out after 90 seconds. Please try again later.");
            }
            catch (HttpRequestException ex)
            {
                Debug.LogError($"HTTP request error: {ex.Message}");
                throw new Exception($"Network error: {ex.Message}. Please check your internet connection.");
            }
            catch (Exception ex) when (!(ex is HttpRequestException || ex is TaskCanceledException))
            {
                Debug.LogError($"Error generating images: {ex.Message}");
                throw;
            }
        }

        public async Task<RetroCreditsResult> GetCredits()
        {
            try
            {
                var url = $"{BASE_URL}/inferences/credits";
                Debug.Log($"Checking credits at URL: {url}");
                
                var response = await _httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                Debug.Log($"Credits response status code: {response.StatusCode}");
                Debug.Log($"Credits response body: {responseBody}");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new Exception("Unauthorized: Your API key appears to be invalid. Please check your API key in the settings.");
                    }
                    else
                    {
                        throw new Exception($"API Error ({response.StatusCode}): {responseBody}");
                    }
                }

                try
                {
                    var result = JsonUtility.FromJson<RetroCreditsResult>(responseBody);
                    if (result == null)
                    {
                        throw new Exception("Failed to parse credits response JSON");
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing credits response: {ex.Message}");
                    Debug.LogError($"Response body: {responseBody}");
                    throw new Exception($"Failed to parse API response: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting credits: {ex.Message}");
                throw;
            }
        }
    }

    public class RetroImageManager
    {
        public static void SaveImages(RetroImageResult result, RetroImageSettings settings, string basePath)
        {
            // Create directory if it doesn't exist
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            // Create subdirectory for this generation batch
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string promptSummary = settings.prompt.Length > 20 
                ? settings.prompt.Substring(0, 20).Replace(" ", "_") 
                : settings.prompt.Replace(" ", "_");
            
            string batchDir = Path.Combine(basePath, $"{timestamp}_{promptSummary}");
            if (!Directory.Exists(batchDir))
            {
                Directory.CreateDirectory(batchDir);
            }

            // List to store saved asset paths for import settings
            List<string> savedAssetPaths = new List<string>();

            // Save images
            for (int i = 0; i < result.base64_images.Count; i++)
            {
                string base64Image = result.base64_images[i];
                byte[] imageBytes = Convert.FromBase64String(base64Image);
                
                string extension = settings.isAnimation && settings.promptStyle == "animation_four_angle_walking" && !settings.returnSpritesheet
                    ? "gif"
                    : "png";

                string filePath = Path.Combine(batchDir, $"image_{i}.{extension}");
                File.WriteAllBytes(filePath, imageBytes);
                
                Debug.Log($"Saved image to: {filePath}");
                
                // Convert to asset path and add to list
                string assetPath = GetUnityAssetPath(filePath);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    savedAssetPaths.Add(assetPath);
                }
            }

            // Refresh asset database to detect new files
            AssetDatabase.Refresh();
            
            // Apply texture import settings to saved assets
            foreach (var assetPath in savedAssetPaths)
            {
                RetroTextureImportManager.SetupTextureImportSettings(assetPath, settings.textureImportSettings);
            }
        }
        
        private static string GetUnityAssetPath(string fullPath)
        {
            // Convert full system path to Unity asset path
            string assetPath = fullPath;
            
            // If the path is inside the project, convert it to relative path
            string dataPath = Application.dataPath;
            
            if (fullPath.StartsWith(dataPath))
            {
                assetPath = "Assets" + fullPath.Substring(dataPath.Length);
            }
            else if (!fullPath.StartsWith("Assets/"))
            {
                // Try to make it a valid asset path if it's not already
                assetPath = Path.GetFullPath(fullPath);
                if (assetPath.StartsWith(dataPath))
                {
                    assetPath = "Assets" + assetPath.Substring(dataPath.Length);
                }
                else
                {
                    Debug.LogWarning($"Path is not inside the Unity project: {fullPath}");
                    return null;
                }
            }
            
            // Always use forward slashes for Unity
            assetPath = assetPath.Replace('\\', '/');
            
            return assetPath;
        }
    }

    public class RetroCreditsManager
    {
        private static int _remainingCredits = -1;
        
        public static int GetRemainingCredits()
        {
            return _remainingCredits;
        }
        
        public static void SetRemainingCredits(int credits)
        {
            _remainingCredits = credits;
        }
        
        public static async Task RefreshCredits(RetroApiClient client)
        {
            try
            {
                var result = await client.GetCredits();
                _remainingCredits = result.credits;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to refresh credits: {ex.Message}");
            }
        }
    }

    public class RetroModelManager
    {
        private static List<RetroModel> _models;
        
        public static List<RetroModel> GetModels()
        {
            if (_models == null)
            {
                InitializeModels();
            }
            
            return _models;
        }
        
        private static void InitializeModels()
        {
            _models = new List<RetroModel>();
            
            // RD_FLUX
            var fluxModel = new RetroModel("RD_FLUX", "RD FLUX");
            fluxModel.styles.Add(new RetroModelStyle("default", "Default"));
            fluxModel.styles.Add(new RetroModelStyle("retro", "Retro"));
            fluxModel.styles.Add(new RetroModelStyle("simple", "Simple"));
            fluxModel.styles.Add(new RetroModelStyle("detailed", "Detailed"));
            fluxModel.styles.Add(new RetroModelStyle("anime", "Anime"));
            fluxModel.styles.Add(new RetroModelStyle("game_asset", "Game Asset"));
            fluxModel.styles.Add(new RetroModelStyle("portrait", "Portrait"));
            fluxModel.styles.Add(new RetroModelStyle("texture", "Texture"));
            fluxModel.styles.Add(new RetroModelStyle("ui", "UI"));
            fluxModel.styles.Add(new RetroModelStyle("item_sheet", "Item Sheet"));
            fluxModel.styles.Add(new RetroModelStyle("mc_texture", "MC Texture"));
            fluxModel.styles.Add(new RetroModelStyle("mc_item", "MC Item"));
            fluxModel.styles.Add(new RetroModelStyle("character_turnaround", "Character Turnaround"));
            fluxModel.styles.Add(new RetroModelStyle("1_bit", "1-Bit"));
            fluxModel.styles.Add(new RetroModelStyle("animation_four_angle_walking", "Animation (4-angle walking)"));
            fluxModel.styles.Add(new RetroModelStyle("no_style", "No Style"));
            
            _models.Add(fluxModel);
        }
    }

    public class RetroTaskManager
    {
        private static List<Task> _runningTasks = new List<Task>();
        
        public static void AddTask(Task task)
        {
            _runningTasks.Add(task);
            
            task.ContinueWith(t => 
            {
                _runningTasks.Remove(task);
                
                if (t.IsFaulted)
                {
                    Debug.LogError($"Task failed: {t.Exception}");
                }
            });
        }
        
        public static bool HasRunningTasks()
        {
            _runningTasks.RemoveAll(t => t.IsCompleted);
            return _runningTasks.Count > 0;
        }
    }

    public class RetroResultManager
    {
        private static List<RetroImageResult> _results = new List<RetroImageResult>();
        
        public static void AddResult(RetroImageResult result)
        {
            _results.Add(result);
        }
        
        public static List<RetroImageResult> GetResults()
        {
            return _results;
        }
        
        public static void ClearResults()
        {
            _results.Clear();
        }
    }

    public class RetroUIStyles
    {
        private static GUIStyle _header;
        private static GUIStyle _subHeader;
        private static GUIStyle _helpBox;
        private static GUIStyle _button;
        private static GUIStyle _footerText;
        private static GUIStyle _footerLink;
        
        public static GUIStyle Header
        {
            get
            {
                if (_header == null)
                {
                    _header = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 16,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleLeft,
                        margin = new RectOffset(0, 0, 10, 10)
                    };
                }
                
                return _header;
            }
        }
        
        public static GUIStyle SubHeader
        {
            get
            {
                if (_subHeader == null)
                {
                    _subHeader = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleLeft,
                        margin = new RectOffset(0, 0, 5, 5)
                    };
                }
                
                return _subHeader;
            }
        }
        
        public static GUIStyle HelpBox
        {
            get
            {
                if (_helpBox == null)
                {
                    _helpBox = new GUIStyle(EditorStyles.helpBox)
                    {
                        margin = new RectOffset(0, 0, 10, 10),
                        padding = new RectOffset(10, 10, 10, 10),
                        wordWrap = true
                    };
                }
                
                return _helpBox;
            }
        }
        
        public static GUIStyle Button
        {
            get
            {
                if (_button == null)
                {
                    _button = new GUIStyle(GUI.skin.button)
                    {
                        padding = new RectOffset(20, 20, 10, 10),
                        margin = new RectOffset(0, 0, 10, 10)
                    };
                }
                
                return _button;
            }
        }
        
        public static GUIStyle FooterText
        {
            get
            {
                if (_footerText == null)
                {
                    _footerText = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 10,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
                        margin = new RectOffset(0, 0, 10, 0)
                    };
                }
                
                return _footerText;
            }
        }
        
        public static GUIStyle FooterLink
        {
            get
            {
                if (_footerLink == null)
                {
                    _footerLink = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 10,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(0.2f, 0.4f, 0.8f) },
                        hover = { textColor = new Color(0.3f, 0.5f, 0.9f) },
                        active = { textColor = new Color(0.4f, 0.6f, 1.0f) },
                        fontStyle = FontStyle.Bold,
                        margin = new RectOffset(0, 0, 0, 0)
                    };
                }
                
                return _footerLink;
            }
        }
    }

    public class RetroUIUtils
    {
        public static void DrawSeparator()
        {
            EditorGUILayout.Space();
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            EditorGUILayout.Space();
        }
        
        public static void DrawHeader(string text)
        {
            EditorGUILayout.LabelField(text, RetroUIStyles.Header);
        }
        
        public static void DrawSubHeader(string text)
        {
            EditorGUILayout.LabelField(text, RetroUIStyles.SubHeader);
        }
        
        public static void DrawHelpBox(string text, MessageType messageType = MessageType.Info)
        {
            EditorGUILayout.HelpBox(text, messageType);
        }
    }

    public class RetroAnimationSettings
    {
        public bool isAnimation = false;
        public bool returnSpritesheet = true;
    }

    public class RetroSettingsStorage
    {
        private const string SETTINGS_PREF_KEY = "RetroSettingsStorage_Settings";
        private const string ANIMATION_SETTINGS_PREF_KEY = "RetroSettingsStorage_AnimationSettings";
        private const string TEXTURE_IMPORT_SETTINGS_PREF_KEY = "RetroSettingsStorage_TextureImportSettings";
        
        public static RetroImageSettings LoadSettings()
        {
            string json = EditorPrefs.GetString(SETTINGS_PREF_KEY, "");
            
            if (string.IsNullOrEmpty(json))
            {
                return new RetroImageSettings();
            }
            
            try
            {
                return JsonUtility.FromJson<RetroImageSettings>(json);
            }
            catch
            {
                return new RetroImageSettings();
            }
        }
        
        public static void SaveSettings(RetroImageSettings settings)
        {
            string json = JsonUtility.ToJson(settings);
            EditorPrefs.SetString(SETTINGS_PREF_KEY, json);
        }
        
        public static RetroAnimationSettings LoadAnimationSettings()
        {
            string json = EditorPrefs.GetString(ANIMATION_SETTINGS_PREF_KEY, "");
            
            if (string.IsNullOrEmpty(json))
            {
                return new RetroAnimationSettings();
            }
            
            try
            {
                return JsonUtility.FromJson<RetroAnimationSettings>(json);
            }
            catch
            {
                return new RetroAnimationSettings();
            }
        }
        
        public static void SaveAnimationSettings(RetroAnimationSettings settings)
        {
            string json = JsonUtility.ToJson(settings);
            EditorPrefs.SetString(ANIMATION_SETTINGS_PREF_KEY, json);
        }
        
        public static RetroTextureImportSettings LoadTextureImportSettings()
        {
            string json = EditorPrefs.GetString(TEXTURE_IMPORT_SETTINGS_PREF_KEY, "");
            
            if (string.IsNullOrEmpty(json))
            {
                return new RetroTextureImportSettings();
            }
            
            try
            {
                return JsonUtility.FromJson<RetroTextureImportSettings>(json);
            }
            catch
            {
                return new RetroTextureImportSettings();
            }
        }
        
        public static void SaveTextureImportSettings(RetroTextureImportSettings settings)
        {
            string json = JsonUtility.ToJson(settings);
            EditorPrefs.SetString(TEXTURE_IMPORT_SETTINGS_PREF_KEY, json);
        }
    }

    public class RetroNativeResolutionSettings
    {
        public bool useNativeResolution = false;
    }

    public class RetroUI : EditorWindow
    {
        private RetroImageSettings _settings;
        private RetroAnimationSettings _animationSettings;
        private Vector2 _scrollPosition;
        private string _apiKey;
        private string _savePath;
        private bool _showAdvancedSettings = false;
        private bool _showApiSettings = false;
        private bool _showAnimationSettings = false;
        private bool _showImg2ImgSettings = false;
        private bool _showPaletteSettings = false;
        private bool _showTextureImportSettings = false;
        private RetroNativeResolutionSettings _nativeResSettings = new RetroNativeResolutionSettings();

        private string _selectedModelName = "RD FLUX";
        private string _selectedStyleName = "Default";
        private List<string> _modelNames;
        private List<string> _styleNames;
        private Dictionary<string, RetroModel> _modelsByName;

        [MenuItem("Window/Retro Diffusion Generator")]
        public static void ShowWindow()
        {
            GetWindow<RetroUI>("Retro Diffusion");
        }

        private void OnEnable()
        {
            InitializeData();
        }

        private void InitializeData()
        {
            _settings = RetroSettingsStorage.LoadSettings();
            _animationSettings = RetroSettingsStorage.LoadAnimationSettings();
            _settings.textureImportSettings = RetroSettingsStorage.LoadTextureImportSettings();
            _apiKey = RetroInputManager.GetApiKey();
            _savePath = RetroInputManager.GetSavePath();
            
            var models = RetroModelManager.GetModels();
            _modelNames = new List<string>();
            _modelsByName = new Dictionary<string, RetroModel>();
            
            foreach (var model in models)
            {
                _modelNames.Add(model.displayName);
                _modelsByName[model.displayName] = model;
            }
            
            UpdateStyleNames();
        }

        private void UpdateStyleNames()
        {
            _styleNames = new List<string>();
            
            if (_modelsByName.TryGetValue(_selectedModelName, out var model))
            {
                foreach (var style in model.styles)
                {
                    _styleNames.Add(style.displayName);
                }
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            RetroUIUtils.DrawHeader("Retro Diffusion Generator");
            EditorGUILayout.Space();

            // API Settings Foldout
            _showApiSettings = EditorGUILayout.Foldout(_showApiSettings, "API Settings");
            if (_showApiSettings)
            {
                string newApiKey = EditorGUILayout.PasswordField("API Key", _apiKey);
                if (newApiKey != _apiKey)
                {
                    _apiKey = newApiKey;
                    RetroInputManager.SetApiKey(_apiKey);
                }

                string newSavePath = EditorGUILayout.TextField("Save Path", _savePath);
                if (newSavePath != _savePath)
                {
                    _savePath = newSavePath;
                    RetroInputManager.SetSavePath(_savePath);
                }

                if (GUILayout.Button("Browse...", GUILayout.Width(100)))
                {
                    string path = EditorUtility.OpenFolderPanel("Select Save Directory", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        string relativePath = path;
                        if (path.StartsWith(Application.dataPath))
                        {
                            relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                        }
                        
                        _savePath = relativePath;
                        RetroInputManager.SetSavePath(_savePath);
                    }
                }

                if (GUILayout.Button("Check Credits"))
                {
                    Task task = CheckCredits();
                    RetroTaskManager.AddTask(task);
                }
                
                int credits = RetroCreditsManager.GetRemainingCredits();
                if (credits >= 0)
                {
                    EditorGUILayout.LabelField($"Remaining Credits: {credits}");
                }
            }

            RetroUIUtils.DrawSeparator();

            // Basic Settings
            RetroUIUtils.DrawSubHeader("Image Settings");

            // Generation Mode Selection
            RetroGenerationMode newMode = (RetroGenerationMode)EditorGUILayout.EnumPopup("Generation Mode", _settings.generationMode);
            if (newMode != _settings.generationMode)
            {
                _settings.generationMode = newMode;
                RetroSettingsStorage.SaveSettings(_settings);
            }

            // Model and Style Selection
            int modelIndex = _modelNames.IndexOf(_selectedModelName);
            int newModelIndex = EditorGUILayout.Popup("Model", modelIndex, _modelNames.ToArray());
            if (newModelIndex != modelIndex && newModelIndex >= 0 && newModelIndex < _modelNames.Count)
            {
                _selectedModelName = _modelNames[newModelIndex];
                _settings.model = _modelsByName[_selectedModelName].name;
                UpdateStyleNames();
                RetroSettingsStorage.SaveSettings(_settings);
            }

            int styleIndex = _styleNames.IndexOf(_selectedStyleName);
            int newStyleIndex = EditorGUILayout.Popup("Style", styleIndex, _styleNames.ToArray());
            if (newStyleIndex != styleIndex && newStyleIndex >= 0 && newStyleIndex < _styleNames.Count)
            {
                _selectedStyleName = _styleNames[newStyleIndex];
                RetroModel model = _modelsByName[_selectedModelName];
                _settings.promptStyle = model.styles[newStyleIndex].name;
                RetroSettingsStorage.SaveSettings(_settings);

                // Check if we switched to animation style
                bool isAnimationStyle = _settings.promptStyle == "animation_four_angle_walking";
                if (isAnimationStyle)
                {
                    _settings.width = 48;
                    _settings.height = 48;
                    _settings.isAnimation = true;
                    _showAnimationSettings = true;
                    RetroSettingsStorage.SaveSettings(_settings);
                }
                else
                {
                    _settings.isAnimation = false;
                    RetroSettingsStorage.SaveSettings(_settings);
                }
            }

            // Animation settings if required
            if (_settings.promptStyle == "animation_four_angle_walking")
            {
                _showAnimationSettings = EditorGUILayout.Foldout(_showAnimationSettings, "Animation Settings");
                if (_showAnimationSettings)
                {
                    _animationSettings.returnSpritesheet = EditorGUILayout.Toggle(
                        "Return as Spritesheet", _animationSettings.returnSpritesheet);
                    
                    _settings.returnSpritesheet = _animationSettings.returnSpritesheet;
                    
                    RetroSettingsStorage.SaveAnimationSettings(_animationSettings);
                    RetroSettingsStorage.SaveSettings(_settings);
                }
            }

            // Prompt and basic settings
            string newPrompt = EditorGUILayout.TextField("Prompt", _settings.prompt);
            if (newPrompt != _settings.prompt)
            {
                _settings.prompt = newPrompt;
                RetroSettingsStorage.SaveSettings(_settings);
            }

            // Only allow dimension changes if not animation
            if (_settings.promptStyle != "animation_four_angle_walking")
            {
                int newWidth = EditorGUILayout.IntSlider("Width", _settings.width, 32, 512);
                if (newWidth != _settings.width)
                {
                    _settings.width = newWidth;
                    RetroSettingsStorage.SaveSettings(_settings);
                }

                int newHeight = EditorGUILayout.IntSlider("Height", _settings.height, 32, 512);
                if (newHeight != _settings.height)
                {
                    _settings.height = newHeight;
                    RetroSettingsStorage.SaveSettings(_settings);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Width: 48 (fixed for animations)");
                EditorGUILayout.LabelField("Height: 48 (fixed for animations)");
            }

            int newNumImages = EditorGUILayout.IntSlider("Number of Images", _settings.numImages, 1, 4);
            if (newNumImages != _settings.numImages)
            {
                _settings.numImages = newNumImages;
                RetroSettingsStorage.SaveSettings(_settings);
            }

            // Img2Img Settings
            if (_settings.generationMode == RetroGenerationMode.Img2Img)
            {
                _showImg2ImgSettings = EditorGUILayout.Foldout(_showImg2ImgSettings, "Img2Img Settings");
                if (_showImg2ImgSettings)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Input Image");
                    
                    string inputImagePath = _settings.inputImagePath;
                    if (GUILayout.Button(string.IsNullOrEmpty(inputImagePath) ? "Select Image..." : Path.GetFileName(inputImagePath)))
                    {
                        string path = EditorUtility.OpenFilePanel("Select Input Image", "Assets", "png,jpg,jpeg");
                        if (!string.IsNullOrEmpty(path))
                        {
                            _settings.inputImagePath = path;
                            RetroSettingsStorage.SaveSettings(_settings);
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(inputImagePath) && GUILayout.Button("Clear", GUILayout.Width(60)))
                    {
                        _settings.inputImagePath = "";
                        RetroSettingsStorage.SaveSettings(_settings);
                    }
                    
                    EditorGUILayout.EndHorizontal();

                    float newStrength = EditorGUILayout.Slider("Strength", _settings.strength, 0f, 1f);
                    if (newStrength != _settings.strength)
                    {
                        _settings.strength = newStrength;
                        RetroSettingsStorage.SaveSettings(_settings);
                    }

                    EditorGUILayout.HelpBox("Higher strength values will make the output less like the input image and more like the prompt.", MessageType.Info);
                }
            }

            // Palette Settings
            if (_settings.generationMode == RetroGenerationMode.WithPalette)
            {
                _showPaletteSettings = EditorGUILayout.Foldout(_showPaletteSettings, "Palette Settings");
                if (_showPaletteSettings)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Palette Image");
                    
                    string inputPalettePath = _settings.inputPalettePath;
                    if (GUILayout.Button(string.IsNullOrEmpty(inputPalettePath) ? "Select Palette..." : Path.GetFileName(inputPalettePath)))
                    {
                        string path = EditorUtility.OpenFilePanel("Select Palette Image", "Assets", "png,jpg,jpeg");
                        if (!string.IsNullOrEmpty(path))
                        {
                            _settings.inputPalettePath = path;
                            RetroSettingsStorage.SaveSettings(_settings);
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(inputPalettePath) && GUILayout.Button("Clear", GUILayout.Width(60)))
                    {
                        _settings.inputPalettePath = "";
                        RetroSettingsStorage.SaveSettings(_settings);
                    }
                    
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.HelpBox("The palette should be a small image containing the colors you want to use in the generated image.", MessageType.Info);
                }
            }

            // Advanced Settings Foldout
            _showAdvancedSettings = EditorGUILayout.Foldout(_showAdvancedSettings, "Advanced Settings");
            if (_showAdvancedSettings)
            {
                // Seed input
                bool hasSeed = _settings.seed.HasValue;
                bool newHasSeed = EditorGUILayout.Toggle("Use Seed", hasSeed);
                
                if (newHasSeed != hasSeed)
                {
                    if (newHasSeed)
                    {
                        _settings.seed = UnityEngine.Random.Range(1, 999999999);
                    }
                    else
                    {
                        _settings.seed = null;
                    }
                    
                    RetroSettingsStorage.SaveSettings(_settings);
                }
                
                if (newHasSeed)
                {
                    int newSeed = EditorGUILayout.IntField("Seed Value", _settings.seed.Value);
                    if (newSeed != _settings.seed)
                    {
                        _settings.seed = newSeed;
                        RetroSettingsStorage.SaveSettings(_settings);
                    }
                    
                    if (GUILayout.Button("Generate Random Seed"))
                    {
                        _settings.seed = UnityEngine.Random.Range(1, 999999999);
                        RetroSettingsStorage.SaveSettings(_settings);
                    }
                }

                // Background removal
                bool newRemoveBackground = EditorGUILayout.Toggle("Remove Background", _settings.removeBackground);
                if (newRemoveBackground != _settings.removeBackground)
                {
                    _settings.removeBackground = newRemoveBackground;
                    RetroSettingsStorage.SaveSettings(_settings);
                }

                // Tiling options
                bool newTileX = EditorGUILayout.Toggle("Tile Horizontally", _settings.tileX);
                if (newTileX != _settings.tileX)
                {
                    _settings.tileX = newTileX;
                    RetroSettingsStorage.SaveSettings(_settings);
                }

                bool newTileY = EditorGUILayout.Toggle("Tile Vertically", _settings.tileY);
                if (newTileY != _settings.tileY)
                {
                    _settings.tileY = newTileY;
                    RetroSettingsStorage.SaveSettings(_settings);
                }

                // Native resolution option
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Resolution Options", EditorStyles.boldLabel);
                
                bool useNative = _settings.upscaleOutputFactor.HasValue;
                bool newUseNative = EditorGUILayout.Toggle("Use Native Resolution", useNative);
                if (newUseNative != useNative)
                {
                    if (newUseNative)
                    {
                        _settings.upscaleOutputFactor = 1.0f;
                    }
                    else
                    {
                        _settings.upscaleOutputFactor = null;
                    }
                    RetroSettingsStorage.SaveSettings(_settings);
                }
            }

            // Add Texture Import Settings after Advanced Settings
            _showTextureImportSettings = EditorGUILayout.Foldout(_showTextureImportSettings, "Texture Import Settings");
            if (_showTextureImportSettings)
            {
                EditorGUI.indentLevel++;
                
                // Texture Type
                var newTextureType = (TextureImporterType)EditorGUILayout.EnumPopup(
                    "Texture Type", _settings.textureImportSettings.textureType);
                if (newTextureType != _settings.textureImportSettings.textureType)
                {
                    _settings.textureImportSettings.textureType = newTextureType;
                    RetroSettingsStorage.SaveTextureImportSettings(_settings.textureImportSettings);
                }
                
                // Filter Mode
                var newFilterMode = (FilterMode)EditorGUILayout.EnumPopup(
                    "Filter Mode", _settings.textureImportSettings.filterMode);
                if (newFilterMode != _settings.textureImportSettings.filterMode)
                {
                    _settings.textureImportSettings.filterMode = newFilterMode;
                    RetroSettingsStorage.SaveTextureImportSettings(_settings.textureImportSettings);
                }
                
                // Wrap Mode
                var newWrapMode = (TextureWrapMode)EditorGUILayout.EnumPopup(
                    "Wrap Mode", _settings.textureImportSettings.wrapMode);
                if (newWrapMode != _settings.textureImportSettings.wrapMode)
                {
                    _settings.textureImportSettings.wrapMode = newWrapMode;
                    RetroSettingsStorage.SaveTextureImportSettings(_settings.textureImportSettings);
                }
                
                // Read/Write
                bool newIsReadable = EditorGUILayout.Toggle(
                    "Read/Write Enabled", _settings.textureImportSettings.isReadable);
                if (newIsReadable != _settings.textureImportSettings.isReadable)
                {
                    _settings.textureImportSettings.isReadable = newIsReadable;
                    RetroSettingsStorage.SaveTextureImportSettings(_settings.textureImportSettings);
                }
                
                // Generate Mip Maps
                bool newGenerateMipMaps = EditorGUILayout.Toggle(
                    "Generate Mip Maps", _settings.textureImportSettings.generateMipMaps);
                if (newGenerateMipMaps != _settings.textureImportSettings.generateMipMaps)
                {
                    _settings.textureImportSettings.generateMipMaps = newGenerateMipMaps;
                    RetroSettingsStorage.SaveTextureImportSettings(_settings.textureImportSettings);
                }
                
                // Alpha Is Transparency
                bool newAlphaIsTransparency = EditorGUILayout.Toggle(
                    "Alpha Is Transparency", _settings.textureImportSettings.alphaIsTransparency);
                if (newAlphaIsTransparency != _settings.textureImportSettings.alphaIsTransparency)
                {
                    _settings.textureImportSettings.alphaIsTransparency = newAlphaIsTransparency;
                    RetroSettingsStorage.SaveTextureImportSettings(_settings.textureImportSettings);
                }
                
                // Sprite-specific settings
                if (_settings.textureImportSettings.textureType == TextureImporterType.Sprite)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Sprite Settings", EditorStyles.boldLabel);
                    
                    // Sprite Mode
                    var newSpriteImportMode = (SpriteImportMode)EditorGUILayout.EnumPopup(
                        "Sprite Mode", _settings.textureImportSettings.spriteImportMode);
                    if (newSpriteImportMode != _settings.textureImportSettings.spriteImportMode)
                    {
                        _settings.textureImportSettings.spriteImportMode = newSpriteImportMode;
                        RetroSettingsStorage.SaveTextureImportSettings(_settings.textureImportSettings);
                    }
                    
                    // Pixels Per Unit
                    int newPixelsPerUnit = EditorGUILayout.IntField(
                        "Pixels Per Unit", _settings.textureImportSettings.pixelsPerUnit);
                    if (newPixelsPerUnit != _settings.textureImportSettings.pixelsPerUnit)
                    {
                        _settings.textureImportSettings.pixelsPerUnit = newPixelsPerUnit;
                        RetroSettingsStorage.SaveTextureImportSettings(_settings.textureImportSettings);
                    }
                    
                    // Mesh Type
                    var newMeshType = (SpriteMeshType)EditorGUILayout.EnumPopup(
                        "Mesh Type", _settings.textureImportSettings.spriteMeshType);
                    if (newMeshType != _settings.textureImportSettings.spriteMeshType)
                    {
                        _settings.textureImportSettings.spriteMeshType = newMeshType;
                        RetroSettingsStorage.SaveTextureImportSettings(_settings.textureImportSettings);
                    }
                    
                    // Pivot
                    var newPivot = (SpritePivot)EditorGUILayout.EnumPopup(
                        "Pivot", _settings.textureImportSettings.spritePivot);
                    if (newPivot != _settings.textureImportSettings.spritePivot)
                    {
                        _settings.textureImportSettings.spritePivot = newPivot;
                        RetroSettingsStorage.SaveTextureImportSettings(_settings.textureImportSettings);
                    }
                }
                
                // Animation Settings
                if (_settings.isAnimation || _settings.promptStyle == "animation_four_angle_walking" || 
                    _settings.textureImportSettings.spriteImportMode == SpriteImportMode.Multiple)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Animation Settings", EditorStyles.boldLabel);
                    
                    // Create Animator
                    bool newCreateAnimator = EditorGUILayout.Toggle(
                        "Create Animator Controller", _settings.textureImportSettings.createAnimatorController);
                    if (newCreateAnimator != _settings.textureImportSettings.createAnimatorController)
                    {
                        _settings.textureImportSettings.createAnimatorController = newCreateAnimator;
                        RetroSettingsStorage.SaveTextureImportSettings(_settings.textureImportSettings);
                    }
                    
                    // Frame Rate
                    float newFrameRate = EditorGUILayout.FloatField(
                        "Frame Rate", _settings.textureImportSettings.frameRate);
                    if (newFrameRate != _settings.textureImportSettings.frameRate)
                    {
                        _settings.textureImportSettings.frameRate = newFrameRate;
                        RetroSettingsStorage.SaveTextureImportSettings(_settings.textureImportSettings);
                    }
                }
                
                EditorGUI.indentLevel--;
            }

            RetroUIUtils.DrawSeparator();

            // Generation Button
            GUI.enabled = !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_settings.prompt) && !RetroTaskManager.HasRunningTasks();
            
            if (GUILayout.Button("Generate Images", RetroUIStyles.Button))
            {
                Task task = GenerateImages();
                RetroTaskManager.AddTask(task);
            }
            
            GUI.enabled = true;

            if (RetroTaskManager.HasRunningTasks())
            {
                EditorGUILayout.HelpBox("Processing...", MessageType.Info);
            }

            EditorGUILayout.Space();
            
            // Footer with credits and GitHub link
            RetroUIUtils.DrawSeparator();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Made by", RetroUIStyles.FooterText, GUILayout.Width(50));
            
            // GitHub link
            if (GUILayout.Button("oliexe", RetroUIStyles.FooterLink, GUILayout.Width(50)))
            {
                Application.OpenURL("https://github.com/oliexe/Retro-Diffusion-Unity");
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndScrollView();
        }

        private async Task CheckCredits()
        {
            try
            {
                var client = new RetroApiClient(_apiKey);
                await RetroCreditsManager.RefreshCredits(client);
                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to check credits: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to check credits: {ex.Message}", "OK");
            }
        }

        private async Task GenerateImages()
        {
            try
            {
                // Validate settings before making API call
                if (string.IsNullOrEmpty(_settings.prompt))
                {
                    EditorUtility.DisplayDialog("Error", "Please enter a prompt for the image", "OK");
                    return;
                }
                
                if (string.IsNullOrEmpty(_apiKey))
                {
                    EditorUtility.DisplayDialog("Error", "Please enter your API key in the API Settings section", "OK");
                    return;
                }

                // Check img2img requirements
                if (_settings.generationMode == RetroGenerationMode.Img2Img && string.IsNullOrEmpty(_settings.inputImagePath))
                {
                    EditorUtility.DisplayDialog("Error", "For Image-to-Image mode, you must select an input image", "OK");
                    return;
                }

                // Check palette requirements
                if (_settings.generationMode == RetroGenerationMode.WithPalette && string.IsNullOrEmpty(_settings.inputPalettePath))
                {
                    EditorUtility.DisplayDialog("Error", "For Palette mode, you must select a palette image", "OK");
                    return;
                }

                Debug.Log("Starting image generation...");
                var client = new RetroApiClient(_apiKey);
                
                // First check if we have credits
                try
                {
                    var credits = await client.GetCredits();
                    if (credits.credits <= 0)
                    {
                        EditorUtility.DisplayDialog("Error", "You don't have enough credits to generate images. Please add credits to your account.", "OK");
                        return;
                    }
                    Debug.Log($"Credits available: {credits.credits}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to check credits: {ex.Message}");
                    // Continue anyway, the API call might still work
                }
                
                Debug.Log("Generating images... This may take up to 60 seconds.");
                EditorUtility.DisplayProgressBar("Retro Diffusion", "Generating images... This may take up to 60 seconds.", 0.5f);
                
                try
                {
                    var result = await client.GenerateImages(_settings);
                    
                    RetroResultManager.AddResult(result);
                    RetroCreditsManager.SetRemainingCredits(result.remaining_credits);
                    
                    RetroImageManager.SaveImages(result, _settings, _savePath);
                    
                    EditorUtility.DisplayDialog("Success", $"Generated {result.base64_images.Count} images. Cost: {result.credit_cost} credits. Remaining: {result.remaining_credits} credits.", "OK");
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
                
                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"Failed to generate images: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to generate images: {ex.Message}", "OK");
            }
        }
    }

    // Simple JSON converter for Dictionary<string, object>
    public static class SimpleJsonConverter
    {
        public static string ToJson(Dictionary<string, object> dict)
        {
            var entries = new List<string>();
            
            foreach (var kvp in dict)
            {
                string valueStr;
                
                if (kvp.Value == null)
                {
                    valueStr = "null";
                }
                else if (kvp.Value is bool b)
                {
                    valueStr = b ? "true" : "false";
                }
                else if (kvp.Value is int || kvp.Value is float || kvp.Value is double)
                {
                    valueStr = kvp.Value.ToString();
                }
                else
                {
                    // Strings need to be quoted and escaped
                    valueStr = "\"" + kvp.Value.ToString().Replace("\"", "\\\"") + "\"";
                }
                
                entries.Add($"\"{kvp.Key}\": {valueStr}");
            }
            
            return "{" + string.Join(",", entries) + "}";
        }
    }
} 