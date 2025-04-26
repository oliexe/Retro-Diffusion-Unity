# Retro Diffusion AI Plugin for Unity

### Warning: This is still very early version I made for my project, so feel free to use it, let me know about issues but It's probably not very stable.

This Unity Editor plugin allows you to generate pixel art and retro-style images using the Retro Diffusion API and save them directly into your Unity project.
Based on [Retro Diffusion API Examples](https://github.com/Retro-Diffusion/api-examples)

<img src="https://i.ibb.co/M3GS6dD/Screenshot-2025-04-26-at-16-19-08.png" alt="Unity Editor" width="800">

## Features

- Generate pixel art images with different styles and settings from your Unity editor.
- Unity texture import settings.

## Setup

1. Import "Editor" folder into your Unity project.
2. Open the Retro Diffusion Generator window in Unity by going to `Window > Retro Diffusion Generator`
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
