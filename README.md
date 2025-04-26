# Retro Diffusion Generator for Unity

This Unity Editor plugin allows you to generate pixel art and retro-style images using the Retro Diffusion API and save them directly into your Unity project.

## Features

- Generate pixel art images with different styles and settings
- Support for animations and spritesheets
- Background removal
- Seamless tiling textures
- Image size customization
- Seed control for reproducible results
- Credit tracking and management

## Setup

1. Open the Retro Diffusion Generator window in Unity by going to `Window > Retro Diffusion Generator`
2. Enter your API key in the API Settings section (You need to create an account at https://retrodiffusion.ai/ to get an API key)
3. Configure the save path where generated images will be stored (default is "Assets/RetroImages")

## Usage

### Basic Settings

1. Select a model (currently only RD_FLUX is supported)
2. Choose a style from the dropdown menu
3. Enter a text prompt describing the image you want to generate
4. Set the width and height of the output image
5. Choose how many images to generate (1-4)
6. Click "Generate Images"

### Advanced Settings

- **Use Seed**: Enable to use a specific seed value for reproducible results
- **Remove Background**: Enable to generate images with transparent backgrounds
- **Tile Horizontally/Vertically**: Enable to create seamless tiling textures

### Animation Settings

When using the "Animation (4-angle walking)" style:
- Image dimensions are fixed at 48x48
- You can choose between a GIF animation or a spritesheet

## Examples

- For a game asset: "A fantasy sword with glowing blue runes" (Game Asset style)
- For a texture: "Stone brick wall with moss" (Texture style, with tiling enabled)
- For a character: "Pixel art warrior with armor and shield" (Character Turnaround style)
- For animation: "Red dragon with wings" (Animation style)

## Troubleshooting

- Make sure you have enough credits in your Retro Diffusion account
- If you get an error, check the console for more details
- If images aren't appearing, try clicking the Refresh button in the Project window

## Credits

This plugin uses the Retro Diffusion API: https://retrodiffusion.ai/ 