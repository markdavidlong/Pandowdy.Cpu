# Icon Assets

This directory contains icon and image assets for the Pandowdy application.

## Icon Files

The following icon files should be placed in this directory:

### Required Icons
- `pandowdy.ico` - Windows ICO file (multi-resolution: 16x16, 32x32, 48x48, 64x64, 128x128, 256x256)
- `pandowdy_16.png` - 16x16 PNG icon
- `pandowdy_32.png` - 32x32 PNG icon  
- `pandowdy_48.png` - 48x48 PNG icon
- `pandowdy_64.png` - 64x64 PNG icon
- `pandowdy_128.png` - 128x128 PNG icon
- `pandowdy_256.png` - 256x256 PNG icon

### Optional Images
- `pandowdy_logo.png` - Application logo
- `pandowdy_splash.png` - Splash screen image

## Usage

### In Code
Icons are available through Qt's resource system:

```cpp
// Set application icon
QApplication::setWindowIcon(QIcon(":/icons/ico/pandowdy_64.png"));

// Use specific size icon
QIcon icon(":/icons/ico/pandowdy_32.png");

// Use ICO file (Windows)
QIcon windowsIcon(":/icons/ico/pandowdy.ico");
```

### Platform-Specific Behavior

- **Linux**: Uses PNG icons from the resource system
- **Windows**: Uses the ICO file embedded in the executable via the .rc file
- **macOS**: Uses PNG icons (ICO support varies)

## Creating Icons

### From SVG
If you have an SVG source, you can generate the required PNG sizes:

```bash
# Using Inkscape
for size in 16 32 48 64 128 256; do
    inkscape -w $size -h $size source.svg -o pandowdy_${size}.png
done

# Create ICO file (using ImageMagick)
convert pandowdy_*.png pandowdy.ico
```

### Guidelines
- Use simple, recognizable designs that work at small sizes
- Ensure good contrast for visibility
- Test icons at actual sizes they'll be displayed
- Consider dark/light theme compatibility