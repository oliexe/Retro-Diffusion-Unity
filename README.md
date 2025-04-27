# Retro Diffusion Plugin for Unity

This Unity Editor plugin allows you to generate pixel art and retro-style images using the Retro Diffusion API and import them directly into your Unity project, and optionally generate corresponding Sprite assets for immediate use in your scenes. The plugin also supports customizing import settings such as texture compression, filter modes, and sprite pivot configurations, making it easy to integrate AI-generated retro visuals into your workflow.

<img src="https://i.ibb.co/DH8s5st6/Screenshot-2025-04-27-at-15-45-19.png" alt="Unity Editor" width="800">

## Features

- Generate pixel art images (sprites, sheets, animations) with different styles and settings from your Unity editor.
- Unity texture import settings.

## Setup

1. Import package into your Unity project.
2. Open the Retro Diffusion Generator window in Unity by going to `Window > Retro Diffusion`
3. Enter your API key in the API Settings section (You need to create an account at https://retrodiffusion.ai/ to get an API key)
4. Configure the save path where generated images will be stored (default is "Assets/RetroImages")

## Usage

### Basic Settings

1. Select a model (currently only RD_FLUX is supported)
2. Choose a style from the dropdown menu
3. Enter a text prompt describing the image you want to generate
4. Set the width and height of the output image
5. Choose how many images to generate 
6. Click "Generate Images"

### Advanced Settings

- **Use Seed**: Enable to use a specific seed value for reproducible results
- **Remove Background**: Enable to generate images with transparent backgrounds
- **Tile Horizontally/Vertically**: Enable to create seamless tiling textures

## Credits

This plugin uses the Retro Diffusion API: https://retrodiffusion.ai/ 
