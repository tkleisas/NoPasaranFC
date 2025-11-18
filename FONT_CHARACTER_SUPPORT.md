# Font Character Support

## Overview
The game uses a Consolas Bold 24pt sprite font with extensive Unicode character support for multilingual text and UI symbols.

## Character Ranges Included

### 1. Basic ASCII (32-126)
- **Range**: `&#32;` to `&#126;`
- **Characters**: Space, !"#$%&'()*+,-./0-9:;<=>?@A-Z[\]^_`a-z{|}~
- **Usage**: Basic English text, numbers, punctuation

### 2. Greek Basic (902-974)
- **Range**: `&#902;` to `&#974;`
- **Characters**: Greek alphabet (uppercase and lowercase)
- **Usage**: Greek language support (Ά-ώ)
- **Examples**: 
  - ΕΠΙΛΟΓΕΣ (Options)
  - ΠΡΟΒΟΛΗ ΑΠΟΤΕΛΕΣΜΑΤΩΝ (View Results)
  - ΝΕΟ ΠΡΩΤΑΘΛΗΜΑ (New Championship)

### 3. Extended Latin and Symbols (160-255)
- **Range**: `&#160;` to `&#255;`
- **Characters**: À-ÿ, ¡¢£¤¥¦§¨©ª«¬®¯°±²³´µ¶·¸¹º»¼½¾¿
- **Usage**: Extended Latin characters for other European languages
- **Examples**: 
  - French: é, è, ê, à, ç
  - Spanish: ñ, á, í, ó, ú
  - German: ä, ö, ü, ß

### 4. Greek Extended (7936-8191)
- **Range**: `&#7936;` to `&#8191;`
- **Characters**: Extended Greek with diacritics
- **Usage**: Ancient Greek, polytonic Greek text

### 5. General Punctuation and Currency (8192-8303)
- **Range**: `&#8192;` to `&#8303;`
- **Characters**: Various spaces, dashes, quotes, bullets
- **Examples**: 
  - Em dash (—)
  - En dash (–)
  - Ellipsis (…)
  - Bullet (•)
  - Currency symbols (€, £, ¥)

### 6. Arrows (8592-8703)
- **Range**: `&#8592;` to `&#8703;`
- **Characters**: All directional arrows
- **Usage**: UI navigation indicators
- **Examples**:
  - ← (U+2190) Left arrow
  - → (U+2192) Right arrow
  - ↑ (U+2191) Up arrow
  - ↓ (U+2193) Down arrow
  - ⇐ (U+21D0) Double left arrow
  - ⇒ (U+21D2) Double right arrow

### 7. Mathematical Operators (8704-8959)
- **Range**: `&#8704;` to `&#8959;`
- **Characters**: Math symbols
- **Examples**: 
  - × (U+00D7) Multiplication
  - ÷ (U+00F7) Division
  - ± (U+00B1) Plus-minus
  - ≤ ≥ ≠ ≈

### 8. Box Drawing and Geometric Shapes (9472-9727)
- **Range**: `&#9472;` to `&#9727;`
- **Characters**: Lines, corners, and geometric shapes
- **Usage**: UI borders, progress bars, indicators
- **Examples**:
  - ─ │ ┌ ┐ └ ┘ (box drawing)
  - ■ □ ▪ ▫ (squares)
  - ▲ (U+25B2) Up triangle
  - ▼ (U+25BC) Down triangle
  - ◄ (U+25C4) Left triangle
  - ► (U+25BA) Right triangle
  - ● ○ (circles)

### 9. Miscellaneous Symbols (9728-9983)
- **Range**: `&#9728;` to `&#9983;`
- **Characters**: Various UI symbols
- **Examples**:
  - ☀ ☁ ☂ (weather)
  - ★ ☆ (stars)
  - ♠ ♣ ♥ ♦ (card suits)
  - ⚽ (soccer ball - if supported by font)

## Usage in Game

### Settings Screen
- **▲ MORE** - Up scroll indicator (U+25B2)
- **▼ MORE** - Down scroll indicator (U+25BC)
- Arrow keys mentioned in instructions (←→↑↓)

### Menu System
- Greek text for main menu options
- Percentage symbols (%) for volume settings
- Multiplication symbols (×) for multipliers

### Match Screen
- Player names (Greek characters)
- Score display
- Time indicators

### Standings Screen
- Greek column headers
- Team names with Greek characters
- Number formatting

## Technical Notes

### Font File
- **Location**: `Content/Font.spritefont`
- **Font**: Consolas Bold
- **Size**: 24pt
- **Kerning**: Enabled
- **Total Character Ranges**: 9

### Build Process
The MonoGame Content Pipeline processes the `.spritefont` file and generates a texture atlas containing all specified characters. This happens during compilation.

### Performance
- **Memory**: ~1-2 MB texture atlas for all character ranges
- **Load Time**: Fast (pre-compiled during build)
- **Rendering**: Hardware-accelerated sprite batch rendering

### Troubleshooting

#### Missing Character Error
If you get "Character X is not in the sprite font" error:
1. Check if the character's Unicode value falls within a defined range
2. Add a new `<CharacterRegion>` if needed
3. Rebuild the project (`dotnet clean && dotnet build`)

#### Font Too Large
If the font texture is too large:
1. Remove unused character ranges
2. Reduce font size in `.spritefont`
3. Split into multiple fonts (e.g., UI font and text font)

#### Character Not Rendering
Even if in range, the font must support the glyph:
1. Consolas supports most Latin, Greek, and Cyrillic
2. Some symbols may not be available in Consolas
3. Consider alternative fonts for specialized symbols

## Adding New Character Ranges

To add support for additional characters:

```xml
<CharacterRegion>
  <Start>&#XXXX;</Start>
  <End>&#YYYY;</End>
</CharacterRegion>
```

Where `XXXX` and `YYYY` are decimal Unicode values.

### Common Additions

**Cyrillic (Russian, etc.):**
```xml
<CharacterRegion>
  <Start>&#1024;</Start>
  <End>&#1279;</End>
</CharacterRegion>
```

**Emoji/Symbols:**
```xml
<CharacterRegion>
  <Start>&#9984;</Start>
  <End>&#10175;</End>
</CharacterRegion>
```

**Chinese/Japanese/Korean:**
*Note: These require much larger character ranges and may need a different font*
```xml
<!-- CJK Unified Ideographs - Very large! -->
<CharacterRegion>
  <Start>&#19968;</Start>
  <End>&#40959;</End>
</CharacterRegion>
```

## Reference
- Unicode Character Table: https://unicode-table.com/
- MonoGame Sprite Font Documentation: https://docs.monogame.net/articles/content/using_fonts.html
