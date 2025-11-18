# Unicode Quick Reference for Game Development

## Common Symbols Used in NoPasaranFC

### Navigation Arrows
| Symbol | Unicode | HTML Entity | Usage |
|--------|---------|-------------|-------|
| ← | U+2190 | `&#8592;` | Left arrow |
| → | U+2192 | `&#8594;` | Right arrow |
| ↑ | U+2191 | `&#8593;` | Up arrow |
| ↓ | U+2193 | `&#8595;` | Down arrow |
| ⇐ | U+21D0 | `&#8656;` | Double left |
| ⇒ | U+21D2 | `&#8658;` | Double right |
| ⇑ | U+21D1 | `&#8657;` | Double up |
| ⇓ | U+21D3 | `&#8659;` | Double down |

### Geometric Shapes (Scroll Indicators)
| Symbol | Unicode | HTML Entity | Usage |
|--------|---------|-------------|-------|
| ▲ | U+25B2 | `&#9650;` | Up triangle (MORE indicator) |
| ▼ | U+25BC | `&#9660;` | Down triangle (MORE indicator) |
| ◄ | U+25C4 | `&#9668;` | Left triangle |
| ► | U+25BA | `&#9658;` | Right triangle |
| ◀ | U+25C0 | `&#9664;` | Left black triangle |
| ▶ | U+25B6 | `&#9654;` | Right black triangle |
| ▀ | U+2580 | `&#9600;` | Upper half block |
| ▄ | U+2584 | `&#9604;` | Lower half block |
| █ | U+2588 | `&#9608;` | Full block |

### Boxes and Borders
| Symbol | Unicode | HTML Entity | Usage |
|--------|---------|-------------|-------|
| ■ | U+25A0 | `&#9632;` | Black square |
| □ | U+25A1 | `&#9633;` | White square |
| ▪ | U+25AA | `&#9642;` | Small black square |
| ▫ | U+25AB | `&#9643;` | Small white square |
| ● | U+25CF | `&#9679;` | Black circle |
| ○ | U+25CB | `&#9675;` | White circle |

### Game Symbols
| Symbol | Unicode | HTML Entity | Usage |
|--------|---------|-------------|-------|
| ★ | U+2605 | `&#9733;` | Black star (ratings) |
| ☆ | U+2606 | `&#9734;` | White star (ratings) |
| ⚽ | U+26BD | `&#9917;` | Soccer ball |
| ⚠ | U+26A0 | `&#9888;` | Warning sign |
| ✓ | U+2713 | `&#10003;` | Check mark |
| ✗ | U+2717 | `&#10007;` | X mark |
| ⏵ | U+23F5 | `&#9205;` | Play symbol |
| ⏸ | U+23F8 | `&#9208;` | Pause symbol |

### Greek Letters (Team Names, UI)
| Symbol | Unicode | HTML Entity | Description |
|--------|---------|-------------|-------------|
| Α | U+0391 | `&#913;` | Alpha (uppercase) |
| α | U+03B1 | `&#945;` | alpha (lowercase) |
| Β | U+0392 | `&#914;` | Beta (uppercase) |
| β | U+03B2 | `&#946;` | beta (lowercase) |
| Γ | U+0393 | `&#915;` | Gamma (uppercase) |
| γ | U+03B3 | `&#947;` | gamma (lowercase) |
| Δ | U+0394 | `&#916;` | Delta (uppercase) |
| δ | U+03B4 | `&#948;` | delta (lowercase) |
| ... | ... | ... | (Full range: 902-974) |
| Ω | U+03A9 | `&#937;` | Omega (uppercase) |
| ω | U+03C9 | `&#969;` | omega (lowercase) |

### Mathematical Symbols
| Symbol | Unicode | HTML Entity | Usage |
|--------|---------|-------------|-------|
| × | U+00D7 | `&#215;` | Multiplication (e.g., 2.0x) |
| ÷ | U+00F7 | `&#247;` | Division |
| ± | U+00B1 | `&#177;` | Plus-minus |
| ° | U+00B0 | `&#176;` | Degree sign |
| % | U+0025 | `&#37;` | Percent (100%) |
| ‰ | U+2030 | `&#8240;` | Per mille |

### Punctuation and Formatting
| Symbol | Unicode | HTML Entity | Usage |
|--------|---------|-------------|-------|
| – | U+2013 | `&#8211;` | En dash |
| — | U+2014 | `&#8212;` | Em dash |
| … | U+2026 | `&#8230;` | Ellipsis |
| • | U+2022 | `&#8226;` | Bullet point |
| ‣ | U+2023 | `&#8227;` | Triangular bullet |
| · | U+00B7 | `&#183;` | Middle dot |

### Currency Symbols
| Symbol | Unicode | HTML Entity | Currency |
|--------|---------|-------------|----------|
| $ | U+0024 | `&#36;` | Dollar |
| € | U+20AC | `&#8364;` | Euro |
| £ | U+00A3 | `&#163;` | Pound |
| ¥ | U+00A5 | `&#165;` | Yen |
| ₽ | U+20BD | `&#8381;` | Ruble |

## Usage in C# Code

### String Literals
```csharp
string upArrow = "▲ MORE";
string downArrow = "▼ MORE";
string leftRight = "← →";
string multiplier = "2.0×";
string percent = "100%";
```

### Unicode Escapes
```csharp
string upArrow = "\u25B2 MORE";
string downArrow = "\u25BC MORE";
string soccerBall = "\u26BD";
```

### Character Constants
```csharp
const char UP_ARROW = '\u25B2';
const char DOWN_ARROW = '\u25BC';
const char MULTIPLY = '\u00D7';
```

## Adding to Font.spritefont

### Decimal Format
```xml
<CharacterRegion>
  <Start>&#9650;</Start>  <!-- ▲ -->
  <End>&#9660;</End>      <!-- ▼ -->
</CharacterRegion>
```

### Hexadecimal Reference
To convert hex Unicode (U+25B2) to decimal for XML:
- U+25B2 = 0x25B2 = 9650 decimal
- Use: `&#9650;`

### Range Calculator
```
Hex: U+25B2
Dec: parseInt("25B2", 16) = 9650
XML: &#9650;
```

## Testing Characters

### In Visual Studio
```csharp
// Test if character is available
try 
{
    spriteBatch.DrawString(font, "▲▼←→", position, Color.White);
}
catch (ArgumentException ex)
{
    // Character not in sprite font
    Console.WriteLine($"Missing character: {ex.Message}");
}
```

### Font Coverage Check
Create a test screen that displays all symbols:
```csharp
string testChars = "▲▼◄►←→↑↓★☆■□●○×÷±%€$";
spriteBatch.DrawString(font, testChars, new Vector2(10, 10), Color.White);
```

## Resources
- **Unicode Explorer**: https://unicode-table.com/
- **Unicode Converter**: https://onlineunicodetools.com/convert-unicode-to-decimal
- **Greek Unicode**: https://unicode-table.com/en/blocks/greek-and-coptic/
- **Emoji Ranges**: https://unicode.org/emoji/charts/full-emoji-list.html

## Common Issues

### Character Appears as □
- Character not in font file (Consolas doesn't support it)
- Solution: Use different font or use alternative character

### Build Fails with Font Error
- Character range too large
- Solution: Split into smaller ranges or reduce size

### Performance Issues
- Too many character ranges (large texture)
- Solution: Only include needed ranges

## Best Practices
1. **Only include what you need** - Reduces memory and load time
2. **Test on target platform** - Font support varies by OS
3. **Provide fallbacks** - Use ASCII alternatives if Unicode fails
4. **Document usage** - Keep track of which symbols are used where
5. **Group related ranges** - Easier to maintain
